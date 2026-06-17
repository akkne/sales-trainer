# Monitoring & Product Metrics

How we observe **product usage** — who's online, what they do, visits over time — on
top of the existing Prometheus + Grafana + Loki stack.

> Infrastructure metrics (HTTP rate, latency, 5xx) and logs were already in place; this
> doc covers the **business/usage** metrics added on top. See the "Sellevate Overview"
> Grafana dashboard for the infra side.

## Stack recap
- Backend exposes `/metrics` via `prometheus-net.AspNetCore` (`Program.cs`:
  `UseHttpMetrics()` + `MapMetrics()`).
- Prometheus scrapes the backend every 15s. **Scrape job name is `sallevate-backend`**
  (note the historical misspelling) — every PromQL query must use it verbatim.
  Config: `infrastructure/prometheus/prometheus.yml` (prod), `prometheus.local.yml` (host dev).
- Grafana auto-provisions dashboards from `infrastructure/grafana/dashboards/*.json`
  into the "Sellevate" folder (reload every 30s). Local: http://localhost:3001.

## Metric catalog
Defined once in `src/backend/api/Infrastructure/Metrics/AppMetrics.cs` (process-global
statics, self-registered with the default registry).

| Metric | Type | Labels | Meaning |
|---|---|---|---|
| `app_users_online` | Gauge | — | Distinct users active in the last 5 min. |
| `app_authenticated_requests_total` | Counter | — | Authenticated backend requests — visits/activity proxy. |
| `app_page_views_total` | Counter | `page` | Frontend page views (bounded page names). |
| `app_events_total` | Counter | `event`, `page` | UI click/action events (bounded names). |
| `app_logins_total` | Counter | `method` (`password`/`google`) | Successful logins. |
| `app_registrations_total` | Counter | — | Completed registrations (email verified). |

**"Visits per day/week" are not stored.** They are derived in Prometheus from the
monotonic counters: `increase(app_authenticated_requests_total[1d])` /
`...[7d]`. This is why those are counters, not gauges.

## How each metric is fed
- **`app_authenticated_requests_total` + presence** — `ActivityTrackingMiddleware`
  (`Infrastructure/Metrics/`) runs after auth, increments the counter, and marks the
  user present in Redis. Infra paths (`/metrics`, `/hangfire`, `/health`, `/swagger`)
  are skipped. Redis failures are swallowed — never break a request.
- **`app_users_online`** — Redis sorted set `presence:online` (member = userId, score =
  last-seen unix sec), managed by `PresenceTracker`. Because Prometheus *pulls* gauges,
  `PresenceGaugeUpdaterService` (a `BackgroundService`, every 20s — faster than the 15s
  scrape) prunes stale members and pushes the count into the gauge. **Tradeoff:** the
  gauge is eventually-consistent to within one 20s tick.
- **`app_page_views_total` / `app_events_total`** — frontend posts to
  `POST /tracking/events` (`MetricsController`), which validates and increments.
- **`app_logins_total` / `app_registrations_total`** — incremented server-side in
  `AuthController` (login/google success; verify-email success), not from the client.

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
  `src/backend/api/Features/Metrics/Constants/TrackedEvents.cs`. Unknown values are
  rejected with `400` — a buggy/hostile client cannot inflate the series count. Caps the
  total at `|events| × |pages|`. Keep each list ≤ ~15 entries.
- `method` is a closed enum. **Never** label any metric with raw paths, user IDs, free
  text, or other unbounded values.

## Grafana dashboard
`infrastructure/grafana/dashboards/product-metrics.json` (uid `sellevate-product`).
Panels and their PromQL (all filtered `job="sallevate-backend"`):

| Panel | PromQL |
|---|---|
| Users Online | `app_users_online{job="sallevate-backend"}` |
| Visits Today | `increase(app_authenticated_requests_total{job="sallevate-backend"}[1d])` |
| Visits This Week | `increase(app_authenticated_requests_total{job="sallevate-backend"}[7d])` |
| Registrations Today | `increase(app_registrations_total{job="sallevate-backend"}[1d])` |
| Page View Rate | `sum by (page) (rate(app_page_views_total{job="sallevate-backend"}[5m]))` |
| Logins Rate | `sum by (method) (rate(app_logins_total{job="sallevate-backend"}[5m]))` |
| Top Events (24h) | `topk(10, sum by (event) (increase(app_events_total{job="sallevate-backend"}[1d])))` |

## Adding a new event/page
1. Add the name to `TrackedEvents.Events` / `TrackedEvents.Pages` (backend).
2. Add it to the `TrackedEvent` / `TrackedPage` union in `track.ts` (frontend).
3. Call `trackEvent("name", "page")` at the relevant UI action.
No new metric needed — it reuses `app_events_total`.

See also testing: [TESTING/METRICS.md](TESTING/METRICS.md).
