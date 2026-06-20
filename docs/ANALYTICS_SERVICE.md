# ANALYTICS_SERVICE.md — Analytics Service extraction

> Phase 1 of the [microservices migration](MICROSERVICES_ROADMAP.md). Extracts the
> product-usage metrics, presence tracking, and conversion funnels out of the monolith
> (`src/backend/api`) into an independently deployable, **Redis-only** `analytics-service`.
> The monolith slices are left in place as reference; the gateway flips the `/tracking/*`
> routes to the new service (strangler fig).

## Bounded context

Everything about observing product usage — who is online, what users click, and how many
complete the key funnel steps:

- **Tracking** — whitelist-validated UI usage events from the frontend, folded into the
  `app_page_views_total` / `app_events_total` Prometheus counters.
- **Presence** — a Redis sorted set of recently-active users, surfaced as the
  `app_users_online` gauge.
- **Funnels** — Kafka consumers that count conversion-relevant integration events
  (`user.registered`, `exercise.completed`, `xp.granted`) into Prometheus counters.

The service is **loss-tolerant**: analytics is best-effort, so a Redis or Kafka hiccup
never breaks a user request, and dropped events only mean a slightly lower count.

## Layout

```
src/backend/analytics-service/
  Analytics/
    Program.cs                         service host wiring (calls AddAnalyticsServices)
    AnalyticsServiceCollectionExtensions.cs
    Sellevate.Analytics.csproj
    Dockerfile                         build context = src/backend (for building-blocks)
    Common/
      Constants/                       routes + error messages
      CurrentUserAccessor.cs           resolves X-User-Id (gateway) / JWT subject
    Features/
      Tracking/                        POST /tracking/events + usage-event recorder
      Presence/                        presence tracker + gauge updater background service
      Funnels/                         idempotent Kafka consumer + funnel recorder
    Infrastructure/
      Metrics/AppMetrics.cs            Prometheus metric catalog
  Analytics.Tests/                     NUnit unit tests
```

## Data ownership

| Store | Owns | Notes |
|---|---|---|
| Redis (`analytics-redis`) | `presence:online` sorted set | Member = userId, score = last-seen unix seconds. O(log N) count/prune on one key. |
| Redis (`analytics-redis`) | Kafka idempotency keys (`idem:analytics-service:*`) | TTL'd dedupe set from the shared `RedisIdempotencyStore`. |

No relational database and no Mongo — this is the first **Redis-only** service. It has its
own Redis instance (`analytics-redis`, host port 6380) so it does not share state with the
monolith's Redis.

## Metrics owned

Defined in `Infrastructure/Metrics/AppMetrics.cs` (process-global statics, self-registered
with the default prometheus-net registry, served at `/metrics`).

| Metric | Type | Labels | Fed by |
|---|---|---|---|
| `app_users_online` | Gauge | — | Presence gauge updater (every 20s). |
| `app_authenticated_requests_total` | Counter | — | Presence ping endpoint. |
| `app_page_views_total` | Counter | `page` | `POST /tracking/events` (page_view). |
| `app_events_total` | Counter | `event`, `page` | `POST /tracking/events` (UI events). |
| `app_registrations_total` | Counter | — | `user.registered` Kafka event. |
| `app_exercises_completed_total` | Counter | — | `exercise.completed` Kafka event. |
| `app_experience_points_granted_total` | Counter | — | `xp.granted` Kafka event. |

`app_logins_total` stays in the monolith's `AuthController` for now (Auth is extracted in
Phase 2), so the product-metrics dashboard queries both the `sallevate-backend` and
`sellevate-analytics` Prometheus jobs.

## Coupling broken during extraction

| Monolith coupling | Resolution in analytics-service |
|---|---|
| `ActivityTrackingMiddleware` marked presence on every authenticated monolith request | Replaced by an explicit `POST /tracking/presence/ping` the frontend calls; the service reads the gateway-injected `X-User-Id` (or the validated JWT subject). |
| `MetricsController` + `AppMetrics` lived inside the monolith process | Moved wholesale; the monolith copies remain as reference but the gateway no longer routes `/tracking/*` to them. |
| Registration/exercise/xp counts came from in-process code paths | Now derived from Kafka integration events, decoupling analytics from the producing services. |

## Kafka

- **Produces:** nothing.
- **Consumes:** `user.registered`, `exercise.completed`, `xp.granted`. The
  `FunnelEventsConsumer` is idempotent (dedupe on `eventId` via the shared Redis store) and
  loss-tolerant.

## Routes (through the gateway, paths preserved)

Flipped to the `analytics` cluster: `/tracking/*`, which covers:

- `POST /tracking/events` — usage events (unchanged payload `{event, page}`, `204`/`400`).
- `POST /tracking/presence/ping` — marks the caller present, `204`.

## Running locally

Infra (`scripts/dev-infra.sh`) then `scripts/dev-analytics.sh` (host, port 5005), or the
full Docker stack `docker compose up --build -d analytics gateway`. Health: `GET /healthz`.

See [docs/TESTING/ANALYTICS_SERVICE.md](TESTING/ANALYTICS_SERVICE.md) for the test layout
and the manual checklist, and [MONITORING.md](MONITORING.md) for the metric catalog and the
Grafana dashboard.
