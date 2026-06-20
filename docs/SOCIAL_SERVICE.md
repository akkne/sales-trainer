# SOCIAL_SERVICE.md — Social Service extraction

> Phase 5 of the [microservices migration](MICROSERVICES_ROADMAP.md). Extracts all
> user-to-user features out of the monolith (`src/backend/api`) into an independently
> deployable `social-service`. The monolith slices are left in place as reference; the
> gateway flips the relevant routes to the new service (strangler fig).

## Bounded context

User-to-user interaction:

- **Friends** — friend requests (send/accept/decline/remove), friend list, pending
  requests, user search, public profile, friend leaderboard, friend activity feed.
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
      Friends/                         friendships CRUD, search, leaderboard, profile
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
| Postgres `social` | `Friendships` | Friend request lifecycle (`Pending`/`Accepted`/`Declined`). |
| Postgres `social` | `DiscussThreads`, `DiscussReplies`, `DiscussVotes`, `DiscussTags`, `DiscussThreadTags`, `DiscussPhotos` | Forum tree + polymorphic votes/photos. `AuthorId`/`UserId` are loose `Guid`s (no cross-DB FK to Identity). |
| Postgres `social` | `UserReplicas` | Local read-model (`UserId`, `Email`, `DisplayName`, `AvatarKey`) fed by `user.*` Kafka events. Used for display names / search instead of joining Identity. |
| Mongo `sallevate` | `chat_conversations` | One-to-one conversations with embedded messages. |
| MinIO/S3 `sellevate-social` | Discuss photo blobs | Keyed `discuss/threads/{ownerId}/{photoId}.ext` or `discuss/replies/...`. |
| Redis | Kafka idempotency store | Dedupe for the `user.*` consumer. |

`DatabaseBootstrapper` creates the `social` database on startup, then EF migrations run
(`InitialSocialSchema`).

## Coupling broken during extraction

| Monolith coupling | Resolution in social-service |
|---|---|
| `FriendService`/`ChatService`/`DiscussService` joining the `Users`/`UserProfiles` tables for display names + avatars | Read from the local `UserReplica` (seeded from `user.*` events); avatar URL is the stable `/avatars/{userId}`. |
| `FriendService` → `INotificationService.CreateAsync` (friend request received/accepted) | Emits the `friend.request.received` / `friend.request.accepted` Kafka events; notification-service writes the inbox entry. |
| `ChatService` → `INotificationService.CreateAsync` (new message) | Emits the `chat.message.sent` Kafka event. |
| Friends leaderboard / public profile / activity feed reading `UserXpRecords`, `UserStreaks`, `UserAchievements`, `UserExerciseAttempts` | Those tables are owned by Gamification/Learning (phases 7 & 8, not extracted yet). Social serves identity fields truthfully and returns the aggregate fields as `0`/empty; the DTO shapes are unchanged. Composed for real once those services exist. (See the `[~]` caveat in the roadmap.) |
| `MongoDbContext` exposing `dialog_sessions` (AI) | Removed — Social owns only `chat_conversations`. |

## Kafka

- **Produces** (partition key = recipient `userId`):
  - `friend.request.received` — `recipientId`, `requesterName`, `requesterId`, `friendshipId`
  - `friend.request.accepted` — `recipientId`, `accepterName`, `accepterId`
  - `chat.message.sent` — `recipientId`, `senderName`, `preview`, `conversationId`

  Payload field names match the notification-service consumer contract exactly
  (`Sellevate.Notification.Eventing` records).
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
