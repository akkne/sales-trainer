# TESTING — Social Service

How to build, test, and manually verify `src/backend/social-service`.

## Automated tests

```bash
dotnet test src/backend/social-service/Social.Tests/Sellevate.Social.Tests.csproj
```

Unit suite (NUnit, no external dependencies — runs offline via the EF Core InMemory
provider, NSubstitute storage, and a recording event publisher):

| Test fixture | Covers |
|---|---|
| `FriendServiceTests` | Friend request lifecycle (send/accept/decline/cancel/remove), self-request + duplicate guards, decline→re-send revival, cancel→re-send fresh request, requester-only + pending-only cancel guards (incl. unknown-request 404), direction reporting, friends list + public profile via `UserReplica`, and `friend.request.received` / `friend.request.accepted` event emission (incl. no accept-event on decline). |
| `DiscussServiceTests` | Thread create (tag resolve + slug reuse), reply count/activity bump, vote toggle + double-upvote idempotency, accepted-reply author-only authorization, view-count increment, `unanswered` filtering, curated-tag duplicate-slug conflict, photo upload (S3 put + metadata), non-author upload forbidden, thread delete cascading votes/photos. |
| `ChatServiceTests` | Friendship guard — chatting with a non-friend throws; an accepted friendship passes the guard. |
| `UserReplicaConsumerTests` | `user.registered` seeds the replica, a second registration is idempotent on the row, `user.updated` updates, `user.deleted` removes. |
| `SocialEventContractTests` | The produced event envelopes serialize to the exact field names the notification-service consumer reads (`recipientId`, `requesterName`/`accepterName`/`senderName`, `friendshipId`/`accepterId`/`conversationId`, `preview`). |

Gateway route-flip is covered in `src/backend/gateway/Gateway.Tests/SocialRouteFlipTests.cs`
(proves `/friends/*`, `/discuss/*`, `/admin/discuss/*`, `/chat/*` target the `social`
cluster and no longer the monolith).

> Full chat send/list flow (Mongo writes) and forum search (`EF.Functions.ILike`) are
> exercised manually — the InMemory provider does not implement them. The chat tests
> cover the friendship guard that runs before any Mongo access.

## Build

```bash
dotnet build src/backend/social-service/Social/Sellevate.Social.csproj
```

## Manual checklist (requires infra)

1. `scripts/dev-infra.sh` then `scripts/dev-social.sh` (or `docker compose up --build -d social gateway`).
2. `GET http://localhost:5006/healthz` → `{ "status": "ok", "service": "social" }`.
3. Through the gateway (`http://localhost:5000`), with two JWT users A and B:
   - A `POST /friends/requests { addresseeId: B }` → `201` + `friendshipId`; confirm a
     `friend.request.received` event on Kafka UI (`:8085`).
   - B `PUT /friends/requests/{id}/accept` → `204`; confirm `friend.request.accepted`.
   - A `GET /friends` → B appears; `GET /friends/profile/{B}` → `friendshipStatus: "friends"`.
   - A `POST /chat/conversations { friendUserId: B }`, then
     `POST /chat/conversations/{id}/messages { content }` → `chat.message.sent` event.
   - A `POST /discuss/threads { title, body, tags }`, `POST .../replies`,
     `POST .../upvote`, `POST .../accepted-reply`, and
     `POST /discuss/threads/{id}/photos` (multipart image) → photo served at
     `GET /discuss/photos/{photoId}/content`.
   - Admin user: `POST /admin/discuss/threads/{id}/pin`, `DELETE /admin/discuss/threads/{id}`.
4. Restart the service and confirm the `social` Postgres database + tables persist
   (migration is idempotent) and the MinIO `sellevate-social` bucket exists.
