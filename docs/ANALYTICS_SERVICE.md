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
  (`user.registered`, `exercise.completed`, `xp.granted`, and since 40.25 `assignment.issued` and
  `assignment.progress.changed`) into Prometheus counters.

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
| Redis (`analytics-redis`) | `org:{orgId}:presence:online` sorted set | Member = userId, score = last-seen unix seconds. One set per organization since 40.13 (was the single platform-wide `presence:online`). O(log N) count/prune on one key. |
| Redis (`analytics-redis`) | `presence:organizations` set | Registry of organization ids that have recorded presence — written on every ping, read only in-process by the gauge updater, and forgotten once an organization's online set empties (40.13). Analytics has no database, so this is how the platform-wide gauge finds "every organization" without one. |
| Redis (`analytics-redis`) | Kafka idempotency keys (`idem:analytics-service:*`) | TTL'd dedupe set from the shared `RedisIdempotencyStore`. |

No relational database and no Mongo — this is the first **Redis-only** service. It has its
own Redis instance (`analytics-redis`, host port 6380) so it does not share state with the
monolith's Redis.

## Multi-tenancy (Phase 40.13)

Presence is the one piece of state this service actually stores, and it is per-organization state:
"who is online" is a fact about one customer's team. Until 40.13 it was one shared key,
`presence:online`, for the whole platform — no endpoint exposed the count to customers, so nothing
had leaked yet, but the moment anyone built "how many of my team are online" a shared key would
answer with the platform's headcount, telling customer A how many people customer B employs and how
active they are.

Every member of `IPresenceTracker` names its organization in its signature — there is no method
meaning "the current organization, whichever that is": the tracker is a singleton, so it has no
current organization, and an ambient one read at the wrong moment is exactly how one customer's
count lands on another's screen. `MarkSeenAsync`/`CountOnlineAsync` raise on an empty organization
rather than building `org:00000000-...:presence:online`, which would be a shared bucket pooling
every caller whose context was missing.

`CountOnlineAcrossAllOrganizationsAsync` is the one deliberate system-mode read (TENANCY.md §1.6),
named for what it does instead of being the default. It feeds the operational `app_users_online`
gauge, which stays a platform sum rather than growing an organization label — a customer id in
Prometheus would put identities and unbounded cardinality into the metrics store to answer a
question that belongs in a product report, not the monitoring stack.

`POST /tracking/presence/ping` is `[TenantScoped]`; a ping with no `X-Organization-Id` (gateway-set)
is refused with `403` rather than pooled into a shared bucket. `FunnelEventsConsumer` declares
`RequiresOrganization => false` — it stores nothing (every branch increments a process-local
Prometheus counter), and `user.registered` is a cross-organization identity event that can
legitimately arrive without one; at the inherited default it would dead-letter those messages and
the registration funnel would quietly flatten.

The old, un-prefixed `presence:online` key is never read after this rollout, but — unlike the
notification-service keys, which carry a TTL — it is a sorted set with no expiry, so it will not
disappear on its own; see `docs/DONT_FORGET.md`.

Platform staff (`Admin`/`SuperAdmin`, the 2026-08-16 role split) are **not** widened here, unlike
every Postgres and Mongo store. Reading presence across organizations means scanning every prefix,
and there is no platform screen that asks for it. The irony would be worth noting even if there
were: a platform-wide presence count is exactly the cross-customer headcount number this key was
prefixed to stop leaking, so if such a screen is ever wanted it deserves its own decision rather
than arriving as a side effect of a role change. See `docs/DECISIONS.md` (2026-08-16).

## How learning metrics are counted by lesson version (Phase 40.16)

Read this before adding any accuracy, mastery or readiness number to a dashboard — including one
that looks like it belongs here.

**This service computes none of it, and that is the design.** analytics-service is Redis-only: it
stores no attempts, no scores and no lesson ids. Its `exercise.completed` consumer increments one
platform-wide Prometheus counter with no lesson, no version and no organization in it, on purpose
(`FunnelEventsConsumer` declares `RequiresOrganization => false`). A funnel counter answers "is
anyone using the product"; it cannot answer "how good is this team at objection handling", and it
must not be made to, because a customer id as a Prometheus label puts customer identities and
unbounded cardinality into the monitoring store.

The data those questions need — `UserExerciseAttempts` and `UserLessonProgressRecords` — lives in
learning-db and stays there. So **the accuracy series is computed in learning-service**
(`LessonAccuracyService`, exposed as `GET /admin/lessons/{lessonId}/accuracy`), and what belongs in
this document is the rule any consumer of those numbers has to obey.

### The rule

Since 40.16 every attempt carries `LessonVersionId` — the immutable snapshot of the lesson it was
scored against — instead of only a mutable `ExerciseId`
([CONTENT_MODEL.md](TENANCY/CONTENT_MODEL.md) §2.3, [LEARNING_SERVICE.md](LEARNING_SERVICE.md)).
Metrics are therefore aggregated **per version**, and versions are then grouped into segments:

- a segment starts at the lesson's first published version, and at every version published with
  `is_breaking = true`;
- a cosmetic version (`is_breaking = false`) **extends** the segment before it.

Both halves matter, and they fail in opposite directions. Without the split, an administrator fixing
a wrong correct-answer silently re-scores months of history and accuracy-per-skill — the number sold
to the РОП as a measure of team readiness — moves retroactively. Without the join, the same chart
steps every time somebody fixes a comma. Either way the customer stops believing the dashboard, and
is right to (§2.4 of CONTENT_MODEL.md).

### Three consequences for whoever draws the chart

1. **Never average across a segment boundary.** Two segments are two populations answering two
   different questions; a single mean over both is a number about nothing. Draw them as separate
   runs, with a visible break.
2. **`unversionedAttempts` is not version 1.** Attempts recorded before 40.16 carry no version until
   `docs/TENANCY/sql/40.16_progress_version_backfill.sql` has been run, and attempts whose exercise
   row was later deleted never get one. The endpoint reports them in their own bucket. Folding that
   bucket into the first segment would be the same unprovable claim the phase exists to remove.
3. **`app_exercises_completed_total` is not a learning metric.** It is a funnel counter and has no
   version dimension by design. If a product report needs "completions of this lesson version", it
   comes from learning-service, not from Prometheus.

## The assignment funnel, and what "metrics in analytics-service" means (Phase 40.25)

Roadmap 40.25 says «метрики воронки заданий — в `analytics-service`». Read the previous section
first, because it already decided how that has to be built.

**What landed here is two counters, and nothing is stored.** `FunnelEventsConsumer` gained
`assignment.issued` and `assignment.progress.changed`, and each increments a process-local Prometheus
counter:

- `app_assignments_issued_total` — one per recipient per issue.
- `app_assignment_progress_total{state}` — one per state change, labelled by the state arrived at.

**What did not land here is the funnel the РОП reads.** Assigned → started → completed → met
threshold, with names, per organization, with a repeat series next to it, is computed by
`AssignmentDashboardService` in learning-service and served by
`GET /admin/assignments/{id}/dashboard`. That is the same split the accuracy series got in 40.16 and
for the same three reasons: this service is Redis-only and holds no progress rows; a counter answers
"is anybody using the feature" and cannot answer "who on this team has not started"; and an
organization label in Prometheus puts customer identities and unbounded cardinality into the
monitoring store to answer a question that belongs in a product report.

A Redis projection of assignment progress was considered and rejected. Redis has no row-level
security and no transaction with learning-db, so the projection can lag, double-count on a
redelivery, or miss a wave — and the РОП would be reading a funnel that disagrees with the progress
rows with no way to tell which is right. 40.22's rule (derive from rows, never increment a counter)
applies more strongly to a copy in another service than it does to a column in the same table.
[DECISIONS.md](DECISIONS.md) (2026-08-18) carries the full argument.

### The rule for whoever reads these two counters

1. **`state` is a bounded label and must stay one.** Its four values — `not_started`, `in_progress`,
   `completed`, `failed_threshold` — are compiled into the platform. `FunnelEventRecorder` keeps its
   own copy of the list (this service does not depend on learning-service for four strings) and
   **drops an unrecognised status rather than counting it**, so a producer that grows a fifth state
   cannot grow the metric's cardinality. It is deliberately not bucketed under "other": a count
   nobody can attribute is a count nobody can act on.
2. **`app_assignment_progress_total` counts transitions, not people.** One person who goes
   `not_started → in_progress → failed_threshold → completed` contributes three. It answers "is the
   feature moving" and cannot answer "how many people completed", which is a question about current
   state and lives in learning-db.
3. **Neither counter has an organization dimension, and neither will.** If a product report needs
   "this customer's assignment funnel", it comes from learning-service. Same sentence as
   `app_exercises_completed_total` in the section above, for the same reason.

## Metrics owned

Defined in `Infrastructure/Metrics/AppMetrics.cs` (process-global statics, self-registered
with the default prometheus-net registry, served at `/metrics`).

| Metric | Type | Labels | Fed by |
|---|---|---|---|
| `app_users_online` | Gauge | — | Presence gauge updater (every 20s), via `CountOnlineAcrossAllOrganizationsAsync` — a deliberate platform-wide sum (40.13), not per-organization: an organization label here would put customer identities and unbounded cardinality into Prometheus. |
| `app_authenticated_requests_total` | Counter | — | Presence ping endpoint. |
| `app_page_views_total` | Counter | `page` | `POST /tracking/events` (page_view). |
| `app_events_total` | Counter | `event`, `page` | `POST /tracking/events` (UI events). |
| `app_registrations_total` | Counter | — | `user.registered` Kafka event. |
| `app_exercises_completed_total` | Counter | — | `exercise.completed` Kafka event. |
| `app_experience_points_granted_total` | Counter | — | `xp.granted` Kafka event. |
| `app_assignments_issued_total` | Counter | — | `assignment.issued` Kafka event (Phase 40.25), one per recipient. No organization label — see the section above. |
| `app_assignment_progress_total` | Counter | `state` | `assignment.progress.changed` Kafka event (Phase 40.25). Four bounded values; an unrecognised status is dropped rather than counted. |

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
- **Consumes:** `user.registered`, `exercise.completed`, `xp.granted`, and since 40.25
  `assignment.issued` and `assignment.progress.changed`. The
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
