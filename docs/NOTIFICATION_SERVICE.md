# NOTIFICATION_SERVICE.md — Notifications Service extraction

> Phase 4 of the [microservices migration](MICROSERVICES_ROADMAP.md). Extracts the
> in-app notification slice out of the monolith (`src/backend/api`) into an
> independently deployable, **Redis-only** `notification-service`. The monolith slice
> is left in place as reference; the gateway flips `/notifications/*` to the new
> service (strangler fig).

## Bounded context

Everything behind the notification bell:

- **Consume** the five integration events that should notify a user.
- **Store** each user's recent notifications and unread count.
- **Serve** the thin REST surface the frontend bell/panel already calls.

There is **no relational database**. Redis is the primary store (per-user capped
list + unread counter, both with a 30-day TTL). The TTL replaces the monolith's
Hangfire `NotificationCleanupJob` — expired notifications simply fall out of Redis,
so there is no scheduled cleanup to run.

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
| Redis list `notifications:inbox:{userId}` | Per-user notification inbox | `LPUSH` newest-first, `LTRIM` to `InboxCapacity` (default 100), `EXPIRE` = `RetentionDays` (default 30). |
| Redis string `notifications:unread:{userId}` | Fast unread counter | Refreshed from the list on every write; same TTL. |
| Redis (shared) | Kafka idempotency store | `idem:notification-service:{eventId}` via the shared `RedisIdempotencyStore`. |

No Postgres, no Mongo, no EF migrations, no `DatabaseBootstrapper`.

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
  `achievement.unlocked`, `streak.milestone`, `friend.request.received`,
  `friend.request.accepted`, `chat.message.sent`. Each maps to a notification written
  to the recipient's Redis inbox via `INotificationEventMapper`. Unmappable or
  malformed payloads are skipped (logged), never crash the consumer.

Producers ship in later phases (Social = Phase 5, Gamification = Phase 7); until then
the consumers idle with no traffic, which is expected.

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
