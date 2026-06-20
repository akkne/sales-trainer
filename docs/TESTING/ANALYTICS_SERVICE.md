# TESTING — Analytics Service

How to build, test, and manually verify `src/backend/analytics-service`.

## Automated tests

```bash
dotnet test src/backend/analytics-service/Analytics.Tests/Sellevate.Analytics.Tests.csproj
```

Unit suite (NUnit, no external dependencies — runs offline; Redis is mocked with
NSubstitute):

| Test fixture | Covers |
|---|---|
| `PresenceTrackerTests` | Mark-seen writes the current timestamp as the sorted-set score; count and prune use the 5-minute window cutoff; correct Redis sorted-set operations are invoked. |
| `UsageEventRecorderTests` | Unknown event/page rejected; `page_view` increments `app_page_views_total{page}`; UI events increment `app_events_total{event,page}`. |
| `FunnelEventRecorderTests` | `user.registered` → registrations counter; `exercise.completed` → exercises counter; `xp.granted` → xp counter by amount; unrelated event types are ignored. |
| `GatewayRouteFlipConfigurationTests` | The gateway `appsettings.json` routes `/tracking/{**catch-all}` to the `analytics` cluster, defines the cluster, and no longer routes `/tracking` to the monolith. |

Idempotency itself is owned and tested in `BuildingBlocks.Tests` (the shared
`RedisIdempotencyStore` + `KafkaConsumerBackgroundService` dedupe on `eventId`); the
`FunnelEventsConsumer` reuses that base unchanged.

## Build

```bash
dotnet build src/backend/analytics-service/Analytics/Sellevate.Analytics.csproj
```

## Manual checklist (requires infra)

1. `scripts/dev-infra.sh` then `scripts/dev-analytics.sh` (or
   `docker compose up --build -d analytics gateway`).
2. `GET http://localhost:5004/healthz` → `{ "status": "ok", "service": "analytics" }`.
3. `GET http://localhost:5004/metrics` → Prometheus exposition with the `app_*` series.
4. Through the gateway (`http://localhost:5000`) with a valid Bearer token:
   - `POST /tracking/events` `{ "event": "page_view", "page": "tree" }` → `204`;
     `{ "event": "bogus", "page": "tree" }` → `400`.
   - `POST /tracking/presence/ping` → `204`; then confirm `app_users_online` rises within
     ~20s on `/metrics`.
5. Publish `user.registered` / `exercise.completed` / `xp.granted` (Kafka UI on `:8085`)
   and confirm `app_registrations_total` / `app_exercises_completed_total` /
   `app_experience_points_granted_total` increase. Republishing the same `eventId` does not
   double-count (idempotency).
6. In Grafana (`http://localhost:3001`), the "Product Metrics" dashboard panels render from
   the `sellevate-analytics` Prometheus job (and `sallevate-backend` for logins).
