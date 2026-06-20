# TESTING — Notifications Service

How to build, test, and manually verify `src/backend/notification-service`.

## Automated tests

```bash
dotnet test src/backend/notification-service/Notification.Tests/Sellevate.Notification.Tests.csproj
```

Unit suite (NUnit, no external dependencies — runs offline against an in-memory
`INotificationStore` fake that mirrors the Redis list/cap semantics):

| Test fixture | Covers |
|---|---|
| `NotificationServiceTests` | event→inbox write; unread count counts only unread; newest-first ordering + `limit`; `includeRead=false` filtering; mark-read drops the unread count + is idempotent for already-read; unknown id throws; mark-all-read; **inbox capping** at configured capacity; **retention applied as the Redis TTL**. |
| `NotificationEventMapperTests` | All five events map to the correct `NotificationType`, body, `actionUrl` and `relatedEntityId`; chat preview is truncated to 160 chars; unknown topic and blank-name payloads map to `null` (so the consumer safely skips them — the idempotency basis). |
| `NotificationRouteFlipTests` (gateway project) | `/notifications` and `/notifications/{**catch-all}` route to the `notification` cluster, not the monolith; the cluster has a destination. |

The gateway route-flip tests live with the gateway suite:

```bash
dotnet test src/backend/gateway/Gateway.Tests/Sellevate.Gateway.Tests.csproj
```

## Build

```bash
dotnet build src/backend/notification-service/Notification/Sellevate.Notification.csproj
```

## Manual checklist (requires infra)

1. `scripts/dev-infra.sh` then `scripts/dev-notifications.sh`
   (or `docker compose up --build -d notification gateway`).
2. `GET http://localhost:5004/healthz` → `{ "status": "ok", "service": "notification" }`.
3. Publish a test event on Kafka (Kafka UI on `:8085`) to one of the consumed topics,
   e.g. `achievement.unlocked` with envelope
   `{ eventId, occurredAt, type:"achievement.unlocked", version:1, data:{ userId, achievementKey, title } }`.
4. Through the gateway (`http://localhost:5000`) with a valid JWT for `userId`:
   - `GET /notifications` → the new notification appears (newest first).
   - `GET /notifications/unread-count` → `{ count: 1 }`.
   - `PUT /notifications/{id}/read` → 204; unread-count drops to 0.
   - `PUT /notifications/read-all` → 204.
5. Re-publish the **same** event (same `eventId`) → no duplicate appears (idempotency).
6. Inspect Redis: `notifications:inbox:{userId}` (list, capped at 100) and
   `notifications:unread:{userId}` both carry a ~30-day TTL (`TTL <key>`).
