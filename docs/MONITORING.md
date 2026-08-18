# Monitoring & Product Metrics

How we observe **product usage** — who's online, what they do, visits over time — on
top of the existing Prometheus + Grafana + Loki stack.

> Infrastructure metrics (HTTP rate, latency, 5xx) and logs were already in place; this
> doc covers the **business/usage** metrics added on top. See the "Sellevate Overview"
> Grafana dashboard for the infra side.

## Stack recap
- The backend, the extracted **analytics-service** and — since Phase 40.33 — **ai-service**
  expose `/metrics` via `prometheus-net.AspNetCore` (`UseHttpMetrics()` + `MapMetrics()`).
- Prometheus scrapes them every 15s, under the jobs `sallevate-backend` (the historical
  misspelling, kept verbatim), `sellevate-analytics` and `sellevate-ai`. Config:
  `infrastructure/prometheus/prometheus.yml` (prod), `prometheus.local.yml` (host dev).
- **As of Phase 1** the product/usage metrics (`app_users_online`,
  `app_page_views_total`, `app_events_total`, `app_authenticated_requests_total`,
  `app_registrations_total`, plus the new `app_exercises_completed_total` /
  `app_experience_points_granted_total`) are owned and exported by the
  **analytics-service** — see [ANALYTICS_SERVICE.md](ANALYTICS_SERVICE.md). `app_logins_total`
  is still emitted by the monolith's `AuthController` until Auth is extracted (Phase 2), so
  dashboard panels query both jobs via `job=~"sallevate-backend|sellevate-analytics"`.
- Grafana auto-provisions dashboards from `infrastructure/grafana/dashboards/*.json`
  into the "Sellevate" folder (reload every 30s).
- **Access.** Local dev: http://localhost:3001. Production: the `docker-compose.prod.yml`
  Traefik overlay publishes it at `https://grafana.sellevate.site` (auto HTTPS, behind
  Grafana's own login). If you don't want it public, drop the route and use an SSH tunnel
  instead: `ssh -L 3001:localhost:3001 user@server` → http://localhost:3001. Login comes
  from `GRAFANA_ADMIN_USER` / `GRAFANA_ADMIN_PASSWORD` in the root `.env`.

## Metric catalog
Product/usage metrics are defined in
`src/backend/analytics-service/Analytics/Infrastructure/Metrics/AppMetrics.cs`
(process-global statics, self-registered with the default registry). The AI-spend metrics added in
Phase 40.33 live in `src/backend/ai-service/Ai/Infrastructure/Metrics/AiSpendMetrics.cs`, same shape. The monolith's
original copy at `src/backend/api/Infrastructure/Metrics/AppMetrics.cs` is left as
reference but is no longer the live source for the flipped metrics.

| Metric | Type | Labels | Owner | Meaning |
|---|---|---|---|---|
| `app_users_online` | Gauge | — | analytics | Distinct users active in the last 5 min. |
| `app_authenticated_requests_total` | Counter | — | analytics | Presence pings — visits/activity proxy. |
| `app_page_views_total` | Counter | `page` | analytics | Frontend page views (bounded page names). |
| `app_events_total` | Counter | `event`, `page` | analytics | UI click/action events (bounded names). |
| `app_logins_total` | Counter | `method` (`password`/`google`) | monolith | Successful logins (until Auth is extracted in Phase 2). |
| `app_registrations_total` | Counter | — | analytics | Completed registrations (from `user.registered`). |
| `app_exercises_completed_total` | Counter | — | analytics | Exercises completed (from `exercise.completed`). |
| `app_experience_points_granted_total` | Counter | — | analytics | XP granted (from `xp.granted`). |
| `app_assignments_issued_total` | Counter | — | analytics | Assignment issues across all organizations, one per resolved recipient (from `assignment.issued`, Phase 40.25). |
| `app_assignment_progress_total` | Counter | `state` | analytics | Assignment progress state transitions across all organizations (from `assignment.progress.changed`, Phase 40.25). |
| `ai_llm_tokens_total` | Counter | `direction` (`prompt`/`completion`) | ai | LLM tokens spent across all organizations and models (Phase 40.33). |
| `ai_llm_calls_total` | Counter | `accounting` (`reported`/`estimated`) | ai | LLM completions across all organizations, split by whether the provider reported the token count or we estimated it (Phase 40.33). |
| `ai_speech_characters_total` | Counter | `kind` (`tts`/`stt`) | ai | Characters synthesized or transcribed across all organizations (Phase 40.33). |
| `ai_quota_refusals_total` | Counter | `resource`, `period` | ai | Calls refused because an organization reached its allowance (Phase 40.33). |

**Neither of the two Phase 40.25 assignment counters carries an organization label, and that is
deliberate — the fourth metric in this file to make that call.** A customer id in a label would put
identities and unbounded cardinality into the monitoring store. `state` is safe precisely because it
is bounded to the four values compiled into the platform (`not_started`, `in_progress`, `completed`,
`failed_threshold`) — `FunnelEventRecorder` drops anything else rather than counting it under a
catch-all. These two counters answer an operational question — "is anybody finishing assignments
platform-wide, and how many are failing the threshold" — and deliberately cannot answer "which
organization" or "which team". The **per-organization assignment funnel** a РОП actually reads is
computed in learning-service from `AssignmentProgressRecords` and served by
`GET /admin/assignments/:assignmentId/dashboard` (see [API_CONTRACTS.md](API_CONTRACTS.md)) — never
from Prometheus.

**None of the four Phase 40.33 AI-spend counters carries an organization label either — the fifth
time this file makes that call, and the first time it makes it about money.** The temptation is
sharper here than anywhere before it: "which customer is burning the budget" is exactly the question
an operator asks, and a `organization` label would answer it in one PromQL query. It is still refused,
for the reasons the paragraph above gives — a customer id in a label puts identities and unbounded
cardinality into the monitoring store — and because the answer already exists somewhere better.
`direction`, `accounting`, `kind`, `resource` and `period` are all closed vocabularies compiled into
the platform; none can grow from data.

So these four answer «сколько платформа сожгла и растёт ли это», and deliberately cannot answer «чей
это расход». The second question is `GET /admin/ai-usage`, computed in ai-service from its
`AiUsageRecords` rows — per organization, per model, with a derived cost estimate. Same split as the
per-organization assignment funnel (40.25): totals in Prometheus, customer numbers from the owning
service's tables, never the other way round. See [AI_QUOTAS.md](AI_QUOTAS.md).

**Why ai-service and not analytics-service.** analytics-service is Redis-only and Kafka-fed by design
(40.16, re-confirmed four times since — 40.25, 40.31 among them). Routing spend through it would mean
a new topic, a new consumer, and a counter that lags the call it counts, in a service whose whole
point is that it owns no relational state. ai-service already holds the number at the instant the
call happens. This is the first metric owner outside analytics since the split, and it is a
deliberate exception rather than a drift.

**"Visits per day/week" are not stored.** They are derived in Prometheus from the
monotonic counters: `increase(app_authenticated_requests_total[1d])` /
`...[7d]`. This is why those are counters, not gauges.

## How each metric is fed
- **`app_authenticated_requests_total` + presence** — the frontend calls
  `POST /tracking/presence/ping` (analytics-service `TrackingController`), which increments
  the counter and marks the caller present in Redis using the gateway-injected `X-User-Id`
  (or the validated JWT subject). Redis failures are swallowed — never break a request.
- **`app_users_online`** — Redis sorted set `presence:online` (member = userId, score =
  last-seen unix sec), managed by `PresenceTracker`. Because Prometheus *pulls* gauges,
  `PresenceGaugeUpdaterService` (a `BackgroundService`, every 20s — faster than the 15s
  scrape) reads the count from Redis and sets the gauge. Pruning of stale set members is
  done on a separate slower cadence (every 5 min); `CountOnlineAsync` already filters by
  the presence window so the gauge value is always accurate without pruning. **Tradeoff:**
  the gauge is eventually-consistent to within one 20s tick.
  **IMPORTANT — horizontal scaling:** every replica reads the same Redis data and will
  produce the same gauge value. Always aggregate `app_users_online` with **`max()`**, not
  `sum()`, across replicas — `sum()` multiplies the count by replica count.
  Correct PromQL: `max(app_users_online)`. Each replica also applies a random startup
  jitter (up to the 20s update interval) to spread Redis load across instances.
- **`app_page_views_total` / `app_events_total`** — frontend posts to
  `POST /tracking/events` (analytics-service `TrackingController`), which validates and
  increments.
- **`app_registrations_total` / `app_exercises_completed_total` /
  `app_experience_points_granted_total`** — the analytics `FunnelEventsConsumer` counts the
  `user.registered` / `exercise.completed` / `xp.granted` Kafka events (idempotent,
  loss-tolerant).
- **`app_assignments_issued_total` / `app_assignment_progress_total`** — the same
  `FunnelEventsConsumer` (`FunnelEventRecorder`), counting `assignment.issued` and
  `assignment.progress.changed`. The `state` label on the second is validated against a copy of
  learning-service's four progress statuses before it is applied; an unrecognised status is dropped
  rather than counted, so a fifth status added later cannot silently open a new Prometheus series.
- **`app_logins_total`** — incremented server-side in the monolith's `AuthController`
  (login/google success), not from the client; moves with Auth in Phase 2.
- **`ai_llm_tokens_total` / `ai_llm_calls_total` / `ai_speech_characters_total`** — incremented by
  `AiSpendMeter.AddToLedgerAsync` in ai-service, on the same call that writes the per-organization
  `AiUsageRecords` row and **before** it, so a Postgres hiccup cannot make the platform total
  silently understate itself. Non-streaming completions carry the provider's own `usage` block
  (`accounting="reported"`); streamed dialog turns have none and are estimated from characters
  (`accounting="estimated"`) — see [AI_QUOTAS.md](AI_QUOTAS.md) §3.
- **`ai_quota_refusals_total`** — incremented at each refusal point in `AiSpendMeter`. **Do not alert
  on it.** An organization reaching a limit somebody sold it is the feature working; the meter logs
  these at `Information` for the same reason. What deserves attention is the *shape*: a `period` of
  `month_batch_reserve` means a customer's content pipeline stopped while their conversations kept
  running, which is the intended degradation order and usually a sales conversation, not an incident.

## Frontend tracking
- `src/frontend/shared/analytics/track.ts` — `trackEvent` / `trackPageView`,
  best-effort (never throws), only fires when an access token is present.
- `use-page-view-tracker.ts` — maps the App Router pathname to a bounded page name and
  fires one page view per navigation. Mounted as `<PageViewTracker />` in
  `app/providers.tsx`.
- Discrete events are sprinkled at a few high-value buttons (e.g. `start_dialog` on
  dialog mode cards).

## Cardinality rules (the central risk — read before adding metrics)
- `app_users_online` stays a **single unlabeled gauge**. Never add a per-user label.
- `page` / `event` label values come from a **server-side whitelist** in
  `src/backend/analytics-service/Analytics/Features/Tracking/Constants/TrackedEvents.cs`
  (the monolith copy under `api/Features/Metrics/Constants/` is reference-only). Unknown values are
  rejected with `400` — a buggy/hostile client cannot inflate the series count. Caps the
  total at `|events| × |pages|`. Keep each list ≤ ~15 entries.
- `method` is a closed enum. **Never** label any metric with raw paths, user IDs, free
  text, or other unbounded values.

## Grafana dashboard
`infrastructure/grafana/dashboards/product-metrics.json` (uid `sellevate-product`).
Panels and their PromQL (all filtered `job=~"sallevate-backend|sellevate-analytics"` so a
panel renders whichever service currently emits the metric):

| Panel | PromQL |
|---|---|
| Users Online | `app_users_online{job=~"sallevate-backend\|sellevate-analytics"}` |
| Visits Today | `increase(app_authenticated_requests_total{job=~"sallevate-backend\|sellevate-analytics"}[1d])` |
| Visits This Week | `increase(app_authenticated_requests_total{job=~"sallevate-backend\|sellevate-analytics"}[7d])` |
| Registrations Today | `increase(app_registrations_total{job=~"sallevate-backend\|sellevate-analytics"}[1d])` |
| Page View Rate | `sum by (page) (rate(app_page_views_total{job=~"sallevate-backend\|sellevate-analytics"}[5m]))` |
| Logins Rate | `sum by (method) (rate(app_logins_total{job=~"sallevate-backend\|sellevate-analytics"}[5m]))` |
| Top Events (24h) | `topk(10, sum by (event) (increase(app_events_total{job=~"sallevate-backend\|sellevate-analytics"}[1d])))` |

## Grafana dashboard — AI spend (Phase 40.33)
`infrastructure/grafana/dashboards/ai-spend.json` (uid `sellevate-ai-spend`). This is the roadmap's
«расход виден в дашборде раньше, чем в счёте от провайдера», platform side.

| Panel | PromQL |
|---|---|
| LLM Tokens (24h) | `sum(increase(ai_llm_tokens_total{job="sellevate-ai"}[1d]))` |
| LLM Calls (24h) | `sum(increase(ai_llm_calls_total{job="sellevate-ai"}[1d]))` |
| Speech Characters (24h) | `sum(increase(ai_speech_characters_total{job="sellevate-ai"}[1d]))` |
| Quota Refusals (24h) | `sum(increase(ai_quota_refusals_total{job="sellevate-ai"}[1d]))` |
| Token Burn Rate by Direction | `sum by (direction) (rate(ai_llm_tokens_total{job="sellevate-ai"}[15m]))` |
| Speech Characters by Kind | `sum by (kind) (rate(ai_speech_characters_total{job="sellevate-ai"}[15m]))` |
| Refusals by Resource and Window | `sum by (resource, period) (increase(ai_quota_refusals_total{job="sellevate-ai"}[1h]))` |
| Estimated vs Reported Accounting | `sum by (accounting) (increase(ai_llm_calls_total{job="sellevate-ai"}[1h]))` |

The last panel is the honesty check: if `estimated` ever dominates `reported`, the spend report is
mostly a guess and `stream_options: {include_usage: true}` becomes worth revisiting with whichever
gateway is in front of the provider.

## Adding a new event/page
1. Add the name to `TrackedEvents.Events` / `TrackedEvents.Pages` (analytics-service).
2. Add it to the `TrackedEvent` / `TrackedPage` union in `track.ts` (frontend).
3. Call `trackEvent("name", "page")` at the relevant UI action.
No new metric needed — it reuses `app_events_total`.

See also testing: [TESTING/METRICS.md](TESTING/METRICS.md).

## Health checks (Phase 10.1)
Every service and the gateway expose two consistently-named endpoints, wired by the
shared `BuildingBlocks.HealthChecks` helpers (`AddSellevateHealthChecks()` +
`MapSellevateHealthChecks()`):

- **`/healthz` — liveness.** Returns `200` with `{ "status": "Healthy", "checks": [] }`
  whenever the process is up. No dependency is probed, so a slow/unreachable dependency
  never makes the process look dead (and never restarts a healthy pod).
- **`/readyz` — readiness.** Runs only the dependency probes tagged `ready` and returns
  `200` when all are healthy, `503` otherwise, with a per-check breakdown:
  `{ "status": "...", "checks": [{ "name": "postgres", "status": "Healthy" }, ...] }`.

Probes per service (each added with the shared check-name constants —
`postgres`, `redis`, `kafka`, `mongo`):

| Service | Liveness | Readiness probes |
|---|---|---|
| identity | yes | postgres, kafka |
| learning | yes | postgres, redis, kafka |
| gamification | yes | postgres, redis, kafka |
| ai | yes | postgres, mongo, redis, kafka |
| social | yes | postgres, mongo, redis, kafka |
| analytics | yes | redis, kafka |
| notification | yes | redis, kafka |
| gateway | yes | (none — it only proxies) |

The Postgres probe reuses EF Core's `AddDbContextCheck`; Redis pings the shared
`IConnectionMultiplexer`; Kafka requests cluster metadata via a short-lived admin client;
Mongo runs the `ping` admin command. These map directly to k8s `livenessProbe` /
`readinessProbe` (see [DEPLOYMENT.md](DEPLOYMENT.md)).

## Kafka consumer-lag dashboard (Phase 10.1)
`infrastructure/grafana/dashboards/kafka-consumer-lag.json` (uid `sellevate-kafka-health`)
visualizes consumer lag and broker/service health. Lag metrics come from a
**kafka-exporter** (`danielqsj/kafka-exporter`) added to both compose stacks and scraped
by Prometheus under the `kafka-exporter` job (port `9308`).

| Panel | PromQL |
|---|---|
| Kafka Brokers Up | `kafka_brokers` |
| Kafka Exporter Up | `up{job="kafka-exporter"}` |
| Total Consumer Lag | `sum(kafka_consumergroup_lag)` |
| Scraped Service Targets Up | `count(up{job=~"sellevate-.*"} == 1)` |
| Consumer Lag by Group | `sum by (consumergroup) (kafka_consumergroup_lag)` |
| Consumer Lag by Topic | `sum by (topic) (kafka_consumergroup_lag)` |
| Topic Message Production Rate | `sum by (topic) (rate(kafka_topic_partition_current_offset[5m]))` |
| Service & Exporter Targets | `up{job=~"sellevate-.*\|kafka-exporter"}` |

Each service's Kafka consumer group id is its service name (`KafkaSettings.ConsumerGroupId`),
so the "by group" panel reads as per-service lag. Rising lag on one group means that
service's consumer is falling behind (or stuck on a poison message — see the DLQ policy in
[ARCHITECTURE.md](ARCHITECTURE.md)).
