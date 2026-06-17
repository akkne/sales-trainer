# Testing — Product Metrics

Covers the usage-metrics feature: `/metrics` exposure, presence gauge, the
`/tracking/events` endpoint, and the Grafana dashboard.

## Prerequisites
- Infra up (Postgres, Redis, Prometheus, Grafana): `scripts/dev-up.sh`.
- Backend on host at http://localhost:5001, Grafana at http://localhost:3001.
- A valid access token (log in via the app or `/demo/token`), referred to below as `$TOKEN`.

## 1. Metrics are exposed
```bash
curl -s http://localhost:5001/metrics | grep '^app_'
```
Expect to see `app_users_online`, `app_authenticated_requests_total`,
`app_page_views_total`, `app_events_total`, `app_logins_total`,
`app_registrations_total` (counters may start at 0 / be absent until first incremented;
`app_users_online` is always present once the updater has ticked).

## 2. Authenticated request tracking + presence
1. Hit any authenticated endpoint:
   ```bash
   curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5001/auth/me >/dev/null
   ```
2. `app_authenticated_requests_total` increases by ≥1 on the next `/metrics` scrape.
3. A member appears in Redis: `redis-cli ZRANGE presence:online 0 -1 WITHSCORES`.
4. Within ~20s, `app_users_online` ≥ 1 at `/metrics`.
5. Confirm infra noise is excluded: repeated `curl /metrics` does **not** inflate
   `app_authenticated_requests_total`.

## 3. `POST /tracking/events` whitelist validation
Valid event → `204`, counter bumps:
```bash
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5001/tracking/events \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"event":"start_dialog","page":"dialog"}'   # => 204
```
Page view (`event=page_view`) → bumps `app_page_views_total{page="..."}`.
Unknown event or page → `400`:
```bash
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5001/tracking/events \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"event":"definitely_not_real","page":"dialog"}'   # => 400
```
Unauthenticated → `401`.

## 4. Login / registration counters
- Successful email login bumps `app_logins_total{method="password"}`.
- Google login bumps `app_logins_total{method="google"}`.
- Completing email verification bumps `app_registrations_total`.

## 5. Frontend page views
- Open the app, navigate between pages (tree → league → profile).
- `app_page_views_total{page="tree"|"league"|"profile"}` increases.
- Dynamic routes collapse correctly (`/dialog/<id>/...` → `page="dialog"`).

## 6. Grafana dashboard
- Grafana → "Sellevate" folder → **Sellevate Product Metrics**.
- All panels render; "Users Online", "Visits Today/This Week" show live values after
  generating some traffic.

## Notes
- Presence window is 5 min; the gauge updater runs every 20s. Allow up to one tick for
  `app_users_online` to reflect activity.
- Cardinality guard: any new `page`/`event` must be added to
  `Features/Metrics/Constants/TrackedEvents.cs` first, else it is rejected with `400`.
