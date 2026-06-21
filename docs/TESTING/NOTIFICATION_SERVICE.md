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
| `NotificationServiceTests` | event→inbox write; unread count counts only unread; newest-first ordering + `limit`; `includeRead=false` filtering; mark-read drops the unread count + is idempotent for already-read; unknown id throws; mark-all-read; **inbox capping** at configured capacity; **retention applied as the Redis TTL**; **`SendEmail=true` dispatches an email, `false` does not, and a deduped replay does not email twice**. |
| `NotificationEventMapperTests` | All events map to the correct `NotificationType`, body, `actionUrl` and `relatedEntityId`; chat preview is truncated to 160 chars; **friend-request, discuss-reply and league-update set `SendEmail=true`** (self-reply maps to `null`, chat keeps `SendEmail=false`); unknown topic and blank-name payloads map to `null` (so the consumer safely skips them — the idempotency basis). |
| `NotificationEmailRendererTests` | Per-type template selection (friend-request/chat/discuss/league) and generic fallback for unmapped types; relative action paths resolve to absolute frontend URLs (and already-absolute URLs pass through); untrusted body is HTML-encoded; the CTA button is omitted when there is no action URL. |
| `NotificationRouteFlipTests` (gateway project) | `/notifications` and `/notifications/{**catch-all}` route to the `notification` cluster, not the monolith; the cluster has a destination. |

> The Redis-backed delayed-chat scheduler (`RedisDelayedChatEmailScheduler`) and the
> `DelayedChatEmailDispatcherService` background loop require a live Redis and are exercised
> via the manual checklist below rather than offline unit tests.

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

## Manual checklist — email (requires infra + a real MailerSend token)

Set `MailerSend__ApiToken` / `MailerSend__FromEmail` in the environment, and ensure the
recipient has been replicated (a `user.registered` event seeded `notifications:user:{userId}`).

1. **Friend request** — publish `friend.request.received` (or send a friend request via the
   social service). The recipient receives an email "You have a new friend request" with a
   `View request` button linking to `{Frontend:Url}/friends?tab=requests`.
2. **Discuss reply** — publish `discuss.reply.created` (or reply to a thread via the social
   service). The thread author receives an email "New reply to your discussion" with a
   `View discussion` button linking to `{Frontend:Url}/discuss/{threadId}`.
3. **League update** — publish `league.updated` (or trigger the weekly rollover). The member
   receives "Your Sellevate league was updated" with the promoted/demoted/new-week wording.
4. **Unread chat (delayed)** — set `NotificationEmail__ChatUnreadDelayMinutes` low (e.g. 1) and
   `DispatcherPollIntervalSeconds` to a few seconds. Publish `chat.message.sent`; wait past the
   delay → an email is sent. Repeat, but publish `chat.message.read` (or call
   `POST /chat/conversations/{id}/read`) before the delay → **no** email is sent.
