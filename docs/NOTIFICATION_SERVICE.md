# NOTIFICATION_SERVICE.md — Notifications Service extraction

> Phase 4 of the [microservices migration](MICROSERVICES_ROADMAP.md). Extracts the
> in-app notification slice out of the monolith (`src/backend/api`) into an
> independently deployable, **Redis-only** `notification-service`. The monolith slice
> is left in place as reference; the gateway flips `/notifications/*` to the new
> service (strangler fig).

## Bounded context

Everything behind the notification bell — plus an opt-in **email** side channel:

- **Consume** the integration events that should notify a user.
- **Store** each user's recent notifications and unread count.
- **Serve** the thin REST surface the frontend bell/panel already calls.
- **Email** the recipient for selected notification types (see
  [EMAIL_NOTIFICATIONS.md](EMAIL_NOTIFICATIONS.md)).

There is **no relational database**. Redis is the primary store (per-user capped
list + unread counter, both with a 30-day TTL; plus a small Redis user replica and the
delayed-email bookkeeping). The TTL replaces the monolith's Hangfire
`NotificationCleanupJob` — expired notifications simply fall out of Redis, so there is
no scheduled cleanup to run.

## Layout

```
src/backend/notification-service/
  Notification/
    Program.cs                         service host wiring (Redis + JWT + Kafka)
    Sellevate.Notification.csproj
    Dockerfile                         build context = src/backend (for building-blocks)
    Common/Constants/                  routes, redis keys, titles, action urls, errors
    Eventing/                          5-topic consumer + event contracts + mapper
    Features/Notifications/
      Endpoints/                       NotificationController (paths preserved)
      Models/                          NotificationRecord, DTOs, CreateNotificationRequest
      Services/Abstract|Implementation NotificationService + Redis/INotificationStore
    Infrastructure/Configuration/      NotificationStorageConfiguration (cap + retention)
  Notification.Tests/                  NUnit unit tests (offline)
```

## Data ownership

| Store | Owns | Notes |
|---|---|---|
| Redis list `org:{orgId}:notifications:inbox:{userId}` | Per-user notification inbox | `LPUSH` newest-first, `LTRIM` to `InboxCapacity` (default 100), `EXPIRE` = `RetentionDays` (default 30). Namespaced per organization since 40.13 (was `notifications:inbox:{userId}`). |
| Redis string `org:{orgId}:notifications:unread:{userId}` | Fast unread counter | Refreshed from the list on every write; same TTL. Namespaced per organization since 40.13. |
| Redis string `org:{orgId}:notifications:chat-email:read:{userId}:{conversationId}` | Chat read watermark | Suppresses a pending unread-chat email once the recipient has actually read the conversation; namespaced per organization since 40.13 — a conversation belongs to one organization, matching social-service's chat isolation in this same block. |
| Redis sorted set `notifications:chat-email:pending` | Delayed unread-chat email queue | **Stays un-prefixed on purpose.** One due-time-ordered work list shaped like an outbox; the organization travels inside each queued item instead (`PendingChatEmail`), the same way it rides in a Kafka envelope. Items queued before 40.13 carry no organization and are dropped with a warning rather than dispatched under a guessed tenant. |
| Redis hash `notifications:user:{userId}` | Cross-org user directory replica | **Deliberately not namespaced** — projects Identity's cross-organization user directory (TENANCY.md §4.2), the same call learning/ai/gamification/social made for their `UserReplicas` tables. |
| Redis (shared) | Kafka idempotency store | `idem:notification-service:{eventId}` via the shared `RedisIdempotencyStore`. |

No Postgres, no Mongo, no EF migrations, no `DatabaseBootstrapper`.

## Multi-tenancy (Phase 40.13)

notification-service has no database and therefore no row-level security to fall back on — the
Redis key **is** the boundary. `INotificationStore` takes the organization explicitly on every
member (it is a singleton, so it has no current organization to read from); `NotificationService` —
scoped, and therefore holding a real `ITenantContext` — is the single place that turns the ambient
tenant into an explicit argument, with no system-mode path at all: an inbox has no platform-global
reading, so an unset tenant raises. The key builders in `Common/Constants/RedisKeys.cs` raise on an
empty organization rather than building `org:00000000-...:notifications:inbox:{user}` — that key
would be one shared bucket collecting every caller whose context was missing, worse than the
un-prefixed key it replaced because it would *look* correctly namespaced.

Platform staff (`Admin`/`SuperAdmin`, the 2026-08-16 role split) are **not** widened here, unlike
every Postgres and Mongo store. A cross-organization read of a prefixed key space means scanning
every prefix, and no platform screen asks for one; building that scan now would add an unbounded
`KEYS`-shaped operation to serve a feature nobody requested. They see an organization's inbox only
while acting inside that organization. Recorded in `docs/DECISIONS.md` so "platform staff see
everything" is not later assumed to include live notification state.

`NotificationController` is `[TenantScoped]` and the tenant middleware is wired into the pipeline —
a request with no `X-Organization-Id` (gateway-set) gets `403`, not a pooled inbox.
`NotificationEventConsumer` keeps `RequiresOrganization` at its inherited `true`, now as a
written-down decision: every topic it handles (`achievement.unlocked`, `streak.milestone`,
`friend.request.received`, `friend.request.accepted`, `chat.message.sent`, `chat.message.read`,
`discuss.reply.created`) produces a notification in exactly one organization's inbox, so an
envelope with no organization is a bug that should dead-letter rather than land somewhere. This is
also why social-service and gamification-service both stamp the organization on every event they
publish (see their own 40.13 notes) — an unstamped envelope reaching this consumer now fails
loudly instead of silently.

Old, un-prefixed Redis keys are never read after the rollout and are left to expire on their own
TTL — see `docs/DONT_FORGET.md` for what that means for a user's notification history across the
deploy.

## Cleanup-job replacement

The monolith ran a Hangfire recurring job (`notification-cleanup`, daily 00:30 UTC,
deletes read notifications older than 30 days). In the service the **30-day Redis TTL
on each inbox key** does the same job passively — there is no Hangfire and no job to
schedule. Capacity capping (`LTRIM`) additionally bounds memory per user.

## Coupling broken during extraction

| Monolith coupling | Resolution in notification-service |
|---|---|
| `FriendService` / `ChatService` / `AchievementService` / `ExerciseService` calling `INotificationService.CreateAsync` in-process | Those producers (Social, Gamification) emit Kafka events; the service consumes them. No in-process call. |
| `Notifications` table + EF `DbContext` + Hangfire cleanup job | Replaced by Redis list/counter + TTL. |
| `NotificationType` enum shared across the monolith | Re-declared locally with the same integer values, so the wire DTO is unchanged. |

## Kafka

- **Produces:** nothing.
- **Consumes (idempotent, dedupe on `eventId`):**
  - `NotificationEventConsumer` — `achievement.unlocked`, `streak.milestone`,
    `friend.request.received`, `friend.request.accepted`, `chat.message.sent`,
    `chat.message.read`, `discuss.reply.created`. Each maps to a
    notification written to the recipient's Redis inbox via `INotificationEventMapper`.
    `discuss.reply.created` also emails immediately; `chat.message.sent`
    schedules a delayed unread-email and `chat.message.read` cancels it. Unmappable or
    malformed payloads are skipped (logged), never crash the consumer.
  - `UserReplicaConsumer` — `user.registered`/`user.updated`/`user.deleted`, projecting a
    minimal `{ email, displayName }` replica into Redis so recipients can be addressed by email.

## Routes (through the gateway, paths preserved)

Flipped to the `notification` cluster:

| Method | Path | Purpose |
|---|---|---|
| GET | `/notifications?limit=&includeRead=` | recent notifications (newest first) |
| GET | `/notifications/unread-count` | `{ count }` |
| PUT | `/notifications/{notificationId}/read` | mark one read (idempotent) |
| PUT | `/notifications/read-all` | mark all read |

The contracts are identical to [API_CONTRACTS.md](API_CONTRACTS.md#notifications), so
the frontend bell/panel is unaffected by the flip.

## Running locally

Infra (`scripts/dev-infra.sh`) then `scripts/dev-notifications.sh` (host, port 5004),
or the full Docker stack `docker compose up --build -d notification gateway`.
Health: `GET /healthz` → `{ "status": "ok", "service": "notification" }`.

See [docs/TESTING/NOTIFICATION_SERVICE.md](TESTING/NOTIFICATION_SERVICE.md) for the
test layout and manual checklist. The original feature spec remains at
[NOTIFICATIONS.md](NOTIFICATIONS.md).
