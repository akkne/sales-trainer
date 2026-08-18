# SOCIAL_SERVICE.md — Social Service extraction

> Phase 5 of the [microservices migration](MICROSERVICES_ROADMAP.md). Extracts all
> user-to-user features out of the monolith (`src/backend/api`) into an independently
> deployable `social-service`. The monolith slices are left in place as reference; the
> gateway flips the relevant routes to the new service (strangler fig).

## Bounded context

User-to-user interaction:

- **Friends** — friend requests (send/accept/decline/remove), friend list, pending
  requests, user search, public profile, friend progress overview, friend activity feed.
- **Discuss** — the community forum: threads, replies, upvotes, accepted (solved)
  replies, free-form + curated tags, photo attachments (S3/MinIO), stats, and the
  admin moderation surface (`/admin/discuss/*`).
- **Chat** — one-to-one conversations between accepted friends (Mongo-backed).

## Layout

```
src/backend/social-service/
  Social/
    Program.cs                         service host wiring
    Sellevate.Social.csproj
    Dockerfile                         build context = src/backend (for building-blocks)
    Common/Constants/                  AvatarUrls
    Eventing/                          friend/chat event publisher + UserReplica consumer
    Features/
      Friends/                         friendships CRUD, search, progress overview, profile
      Chat/                            Mongo conversations + messages
      Discuss/                         forum threads/replies/votes/tags/photos + admin
    Identity/                          UserReplica entity
    Infrastructure/
      Configuration/                   S3 / Mongo options
      Data/                            SocialDbContext (Postgres) + EF migrations
      Mongo/                           MongoDbContext (chat_conversations)
      Storage/                         S3/MinIO object storage
  Social.Tests/                        NUnit unit tests
```

## Data ownership

| Store | Owns | Notes |
|---|---|---|
| Postgres `social` | `Friendships` | Friend request lifecycle (`Pending`/`Accepted`/`Declined`). Tenancy: `OrganizationId` `NOT NULL`, `ITenantScoped`, strict RLS (40.13) — a friendship cannot cross the organization boundary. |
| Postgres `social` | `DiscussThreads`, `DiscussReplies`, `DiscussVotes`, `DiscussThreadTags`, `DiscussPhotos` | Forum tree + polymorphic votes/photos. `AuthorId`/`UserId` are loose `Guid`s (no cross-DB FK to Identity). Tenancy: `OrganizationId` `NOT NULL`, `ITenantScoped`, strict RLS (40.13). |
| Postgres `social` | `DiscussTags` | The one content table in this database. Tenancy: `OrganizationId` **nullable** — `NULL` is the curated vocabulary every organization shares, non-null is one customer's own tag (40.13); filter is `== null \|\| == current`, never plain equality. |
| Postgres `social` | `UserReplicas` | Local read-model (`UserId`, `Email`, `DisplayName`, `AvatarKey`) fed by `user.*` Kafka events. Used for display names / search instead of joining Identity. **No `OrganizationId`**: it projects Identity's cross-organization user directory (TENANCY.md §4.2), the same call learning (40.10)/ai (40.11)/gamification (40.13) made. |
| Mongo `sallevate` | `chat_conversations` | One-to-one conversations with embedded messages. Tenancy: `organizationId` on every document, enforced by `ChatConversationRepository` alone — Mongo has no RLS (40.13). |
| MinIO/S3 `sellevate-social` | Discuss photo blobs | Keyed `discuss/threads/{ownerId}/{photoId}.ext` or `discuss/replies/...`; new uploads since 40.13 are keyed `org/{organizationId}/discuss/threads/...` (or `.../replies/...`) — old keys keep working because the key is read from the `DiscussPhotos` row, never recomputed. |
| Redis | Kafka idempotency store | Dedupe for the `user.*` consumer. |

`DatabaseBootstrapper` creates the `social` database on startup, then EF migrations run
(`InitialSocialSchema` … `AddOrganizationId`). Index rebuilds and the Postgres/Mongo backfills are
**not** part of startup — they are operational steps, driven by
`scripts/tenancy-social-organization-rollout.sh`.

## Multi-tenancy (Phase 40.13)

Six tables hold one customer's data — `Friendships`, `DiscussThreads`, `DiscussReplies`,
`DiscussVotes`, `DiscussThreadTags`, `DiscussPhotos` — all `OrganizationId NOT NULL`,
`ITenantScoped`, the **strict** RLS flavour (`EnableTenantRls`, plain equality), and an EF query
filter written out per entity: EF does not inherit filters through navigations, so a filter on
`DiscussThread` says nothing about the `DiscussReplies` hanging off it. `SocialTenancyModelTests`
walks the model and fails the build if an entity grows an `OrganizationId` without a matching
filter.

`DiscussTags` is the exception, and deliberately so: a tag is a word, not somebody's content.
`OrganizationId` is nullable, the filter is `tag.OrganizationId == null || tag.OrganizationId ==
current` (content flavour — plain equality would open Discuss to a new customer with no tags at
all), and the stamping is explicit rather than automatic — the write guard only recognizes
`ITenantScoped`, whose `OrganizationId` cannot be nullable. A tag typed by a user is stamped in
`ResolveOrCreateTagsAsync`; a curated tag created through the SuperAdmin controller is left global
on purpose.

Chat lives in Mongo, which has no row-level security, so its boundary is application code:
`MongoDbContext` now exposes only the database handle, not a `ChatConversations` property, and
`ChatConversationRepository` is the only class allowed to call `GetCollection<ChatConversation>` —
asserted against the source tree by a unit test (`Only_the_repository_reaches_the_chat_conversations_collection`),
not just documented. Every repository method takes the tenant from `ITenantContext` and raises on
an unset one; there is no system-mode bypass, the same shape ai-service used for `dialog_sessions`
in 40.11.

Friendship and chat cannot cross the organization boundary, and the second half is structural
rather than a check: `ChatService.GetOrCreateConversationAsync` refuses to open a conversation
between two people who are not `Accepted` friends, and that friendship check reads the
RLS-protected `Friendships` table inside a `TenantTransactionScope`. A conversation that cannot be
started cross-tenant is one that cannot exist.

Two unique constraints move into the `AddOrganizationId` migration itself rather than the
concurrent-rebuild script, on the reasoning gamification-service's 40.13 block used: without them
the platform is broken for the second organization the moment this deploys, and neither table
grows without bound. `UNIQUE(Slug)` on `DiscussTags` becomes `UNIQUE(OrganizationId, Slug)` plus a
partial unique index over the global rows (Postgres treats `NULL`s in a composite unique index as
distinct, so the composite alone would let the curated tag "objections" exist twice at the global
level — same pair learning-service needed for `Skill.IconicName` in 40.10); `Friendships`'
`UNIQUE(RequesterId, AddresseeId)` and its canonical-pair index both gain `OrganizationId` as the
leading column, because memberships (40.6) let one person belong to two customers, and the old
platform-wide pair would have rejected the second organization's friendship between the same two
people as a duplicate. Every read index stays out and is rebuilt concurrently by
`scripts/tenancy-social-organization-rollout.sh --indexes`.

`FriendService.SearchUsersAsync` still searches `UserReplicas` platform-wide — it is the
cross-organization user directory by design (TENANCY.md §4.2) — so it can surface a person from
another organization by name or email; a friend request toward them is what the `Friendships` RLS
policy then refuses. See docs/DECISIONS.md.

**Not run against any database:** migration `20260816081204_AddOrganizationId` adds the columns
and RLS policies only. `docs/TENANCY/sql/40.13_social_organization_backfill.sql` (Postgres),
`docs/TENANCY/mongo/40.13_chat_conversations_organization_backfill.js` (Mongo) and
`docs/TENANCY/sql/40.13_social_organization_indexes_concurrently.sql` are driven by
`scripts/tenancy-social-organization-rollout.sh` — see `docs/DONT_FORGET.md` for the order.

## Coupling broken during extraction

| Monolith coupling | Resolution in social-service |
|---|---|
| `FriendService`/`ChatService`/`DiscussService` joining the `Users`/`UserProfiles` tables for display names + avatars | Read from the local `UserReplica` (seeded from `user.*` events); avatar URL is the stable `/avatars/{userId}`. |
| `FriendService` → `INotificationService.CreateAsync` (friend request received/accepted) | Emits the `friend.request.received` / `friend.request.accepted` Kafka events; notification-service writes the inbox entry. |
| `ChatService` → `INotificationService.CreateAsync` (new message) | Emits the `chat.message.sent` Kafka event. |
| Friends progress overview / public profile / activity feed reading `UserXpRecords`, `UserStreaks`, `UserAchievements`, `UserExerciseAttempts` | Those tables are owned by Progress & Recognition/Learning (phases 7 & 8, not extracted yet). Social serves identity fields truthfully and returns the aggregate fields as `0`/empty; the DTO shapes are unchanged. Composed for real once those services exist. (See the `[~]` caveat in the roadmap.) |
| `MongoDbContext` exposing `dialog_sessions` (AI) | Removed — Social owns only `chat_conversations`. |
| `ChatService` holding a Mongo collection handle directly | Removed (40.13) — the collection lives behind `ChatConversationRepository`, the only class permitted to construct it. |

## Kafka

- **Produces** (partition key = recipient `userId`):
  - `friend.request.received` — `recipientId`, `requesterName`, `requesterId`, `friendshipId`
  - `friend.request.accepted` — `recipientId`, `accepterName`, `accepterId`
  - `chat.message.sent` — `recipientId`, `senderName`, `preview`, `conversationId`

  Payload field names match the notification-service consumer contract exactly
  (`Sellevate.Notification.Eventing` records). Since 40.13 every publish carries the
  organization in the envelope (`KafkaSocialEventPublisher`, sourced from `ITenantContext`); an
  unset tenant raises rather than publishing an unstamped event — notification-service kept
  `RequiresOrganization = true` on its consumer in this same block, so an unstamped envelope would
  otherwise dead-letter instead of notifying anyone.
- **Consumes:** `user.registered` / `user.updated` / `user.deleted` to maintain the
  local `UserReplica`. Idempotent (dedupe on `eventId` via the shared Redis store).

## Routes (through the gateway, paths preserved)

Flipped to the `social` cluster: `/friends/*` (+ root `/friends`), `/discuss/*`,
`/admin/discuss/*`, `/chat/*`. The monolith catch-all keeps everything else; its
Friends/Discuss/Chat code stays in `src/backend/api` as reference.

`GET /discuss/photos/{photoId}/content` is anonymous (image delivery); every other
route requires the JWT, and `/admin/discuss/*` requires the `Admin`/`SuperAdmin` role.

## Running locally

Infra (`scripts/dev-infra.sh`) then `scripts/dev-social.sh` (host, port 5006), or the
full Docker stack `docker compose up --build -d social gateway`. Health: `GET /healthz`
→ `{ "status": "ok", "service": "social" }`.

See [docs/TESTING/SOCIAL_SERVICE.md](TESTING/SOCIAL_SERVICE.md) for the test layout and
the manual checklist. The original feature specs remain at [FRIENDS.md](FRIENDS.md) and
[DISCUSS.md](DISCUSS.md).
