# Monitoring & Product Metrics

How we observe **product usage** — who's online, what they do, visits over time — on
top of the existing Prometheus + Grafana + Loki stack.

> Infrastructure metrics (HTTP rate, latency, 5xx) and logs were already in place; this
> doc covers the **business/usage** metrics added on top. See the "Sellevate Overview"
> Grafana dashboard for the infra side.

## Stack recap
- Both the backend and the extracted **analytics-service** expose `/metrics` via
  `prometheus-net.AspNetCore` (`UseHttpMetrics()` + `MapMetrics()`).
- Prometheus scrapes both every 15s, under two jobs: `sallevate-backend` (the historical
  misspelling, kept verbatim) and `sellevate-analytics`. Config:
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
(process-global statics, self-registered with the default registry). The monolith's
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
  scrape) prunes stale members and pushes the count into the gauge. **Tradeoff:** the
  gauge is eventually-consistent to within one 20s tick.
- **`app_page_views_total` / `app_events_total`** — frontend posts to
  `POST /tracking/events` (analytics-service `TrackingController`), which validates and
  increments.
- **`app_registrations_total` / `app_exercises_completed_total` /
  `app_experience_points_granted_total`** — the analytics `FunnelEventsConsumer` counts the
  `user.registered` / `exercise.completed` / `xp.granted` Kafka events (idempotent,
  loss-tolerant).
- **`app_logins_total`** — incremented server-side in the monolith's `AuthController`
  (login/google success), not from the client; moves with Auth in Phase 2.

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

## Adding a new event/page
1. Add the name to `TrackedEvents.Events` / `TrackedEvents.Pages` (analytics-service).
2. Add it to the `TrackedEvent` / `TrackedPage` union in `track.ts` (frontend).
3. Call `trackEvent("name", "page")` at the relevant UI action.
No new metric needed — it reuses `app_events_total`.

See also testing: [TESTING/METRICS.md](TESTING/METRICS.md).
