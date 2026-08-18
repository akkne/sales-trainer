# ARCHITECTURE.md

## Stack

```
Next.js 16 (TypeScript, App Router)
  → HTTP REST (JSON) + JWT Bearer
    → ASP.NET Core 9 Web API (C#)
        ├── PostgreSQL 17   (main relational data)
        ├── MongoDB 8       (chat messages, future transcripts)
        ├── Redis 7         (cache, sessions, notifications, team progress views)
        └── OpenAI API      (free-text exercise evaluation, via ai-service only)
```

## Frontend: `src/frontend`

**Libraries:** Next.js 16.2.1, React 19.2.4, TypeScript, Tailwind CSS, Zustand, TanStack Query, Framer Motion

**Layout.** The `lib/` + `components/` split this section used to describe is gone; neither
directory exists any more. The frontend is organised by *feature* — each feature owns its own
components, hooks and API calls — with a single `shared/` tree for what genuinely crosses features:

```
app/                        ← routes only; every page is thin and delegates to a feature
  (auth)/     login, onboarding, verify-email, invite/[token]
  (main)/     tree, skill/[id], reference, reference/[id], guidebook, profile, settings,
              companies, companies/[id], dialog, dialog/[bundleId], dialog-reviews,
              discuss, discuss/[threadId], friends, friends/[userId], friends/chat
  (admin)/    admin/… — skills, skill-stages, topics, lessons, bulk-lessons, import,
              reference, techniques, prompts, quotes, dialog, voice, leagues,
              gamification, discuss, organizations, users
  session/[lessonId]        ← the exercise runner (outside (main), full-screen)
  dialog/[bundleId]/[modeId], companies/[id]/call   ← full-screen voice/dialog surfaces
  api/logs                  ← the one Next route handler (browser log forwarding)
features/
  admin, assignments, auth, companies, devtools, dialog, dialog-reviews, discuss,
  exercise, friends, layout, notifications, profile, skills, voice
shared/
  api/api-client.ts         ← single fetch wrapper (auto JWT + 401 refresh)
  stores/                   ← Zustand: auth-store, selected-skill-store, theme-store,
                               notification-preferences-store
  analytics/, components/, constants/, hooks/, utils/
```

There is no `(main)/league/` route. The weekly league screen this document used to list is not part
of the product any more, even though gamification-service still serves the league endpoints.

## Backend: `src/backend/<service>/`

There is no `src/api/backend` and no single `AppDbContext` — both belonged to the monolith and both
are gone. Each service is its own ASP.NET Core project under `src/backend/<name>-service/<Name>/`,
with its own database and its own context (`LearningDbContext`, `IdentityDbContext`, `AiDbContext`,
`GamificationDbContext`, `SocialDbContext`, `CompanyDbContext`, `OrganizationDbContext`;
notification- and analytics-service have no relational database at all). The vertical-slice shape
survived the split — it just repeats per service instead of once:

```
src/backend/learning-service/Learning/      ← the shape every service follows
  Program.cs                    ← composition root: auth, tenancy, eventing, health
  Features/                     ← one folder per slice, each Controller + Services/ + Dtos/
    Admin/ Assignments/ Content/ ContentAdaptation/ ContentGeneration/ DailyQuotes/
    DialogReviews/ Exercises/ Lessons/ Programs/ Reference/ SkillTree/ TeamInsights/ Techniques/
  Eventing/                     ← Kafka producers/consumers for this service's topics
  Infrastructure/
    Data/
      LearningDbContext.cs
      Migrations/               ← this service's migrations only
      *EntityConfiguration.cs   ← jsonb/array column configs, RLS + tenant filters
    Ai/ Identity/               ← typed HTTP clients to other services
  Common/, DependencyInjection/, Identity/
```

**Rules from RAW.md enforced here:**
- No repository wrappers — services use their own `DbContext` directly
- DTO ≠ Entity — controllers never return EF entities
- **No service calls an AI provider in-process.** ai-service owns every LLM / TTS / Whisper call;
  everyone else reaches it over HTTP (`/ai/evaluate`, `/ai/chat`, `/ai/chat/stream`, `/ai/tts`,
  `/ai/content/*`) behind `X-Internal-Service-Secret`. This is stated as an invariant because it
  had already silently stopped being true once: the monolith split left learning-service holding
  its own `OpenAiChatService` and `YandexTtsService`, and block 40.33 had to remove them. It is now
  checked rather than remembered — `scripts/ai-provider-lint.py` fails CI if any service outside
  ai-service opens a provider client, and allow-lists the metered callers inside ai-service.
- Async/await everywhere, nullable reference types enabled

## Docker

`docker-compose.yml` at root defines 22 services — the nine backend services, the gateway, the
frontend, and infra. There is no `backend` service; the thing on :5000 is the gateway:

- `frontend` on `127.0.0.1:3000`
- `gateway` on `127.0.0.1:5000` (internal :8080) — the only backend entry point
- the nine services: `identity`, `learning`, `ai`, `gamification`, `social`, `analytics`,
  `notification`, `company`, `organization`
- data stores: `postgres` :5432 (healthcheck gates the services), `mongo` :27017, `redis` :6379,
  `analytics-redis`, `minio`
- eventing: `kafka` :9092, plus `kafka-ui` :8085 and `kafka-exporter`
- observability: `loki`, `prometheus`, `grafana`

Each service auto-runs `db.Database.Migrate()` on startup against its own database.

This is the deploy shape, not the default local one — for local iteration see
[LOCAL_DEV.md](LOCAL_DEV.md), which keeps only infra in Docker.

## Microservices migration — platform foundations (Phase 0)

The monolith above has been carved into independently deployable services per
[MICROSERVICES.md](MICROSERVICES.md) (target) and
[MICROSERVICES_ROADMAP.md](MICROSERVICES_ROADMAP.md) (route). **The migration is
complete (Phase 9): the monolith is retired** — every route is owned by a service and
the gateway no longer has a catch-all to the monolith. It is also *gone*: `src/backend/api` and
`src/backend/tests` were deleted from `main` (commit `46c06a8`) and `Sellevate.sln` no longer
contains the monolith project. The code is preserved on the `monolith-legacy` branch if it is ever
needed for archaeology; nothing on `main` references it.

```
src/backend/
  {identity,learning,gamification,ai,social,analytics,notification,company,organization}-service/
                               ← the extracted services, each with its own DB + tests
  building-blocks/BuildingBlocks/   ← shared lib (event envelope, Kafka publisher +
                                       idempotent-consumer base, Redis idempotency
                                       store, UserReplica, identity-header helpers,
                                       tenancy primitives)
  building-blocks/BuildingBlocks.Tests/
  gateway/Gateway/             ← YARP API gateway (per-service routing, no catch-all:
                                  unknown routes 404; central JWT validation,
                                  X-User-* header injection)
  gateway/Gateway.Tests/
  Sellevate.sln                ← backend-wide solution (all of the above)
```

- **Event bus:** Apache Kafka (single-broker KRaft) for backend↔backend events.
  Topic names + the `{ eventId, occurredAt, type, version, data }` envelope live in
  `BuildingBlocks` (`Topics`, `EventEnvelope`). Consumers are idempotent (dedupe on
  `eventId` via a Redis-backed `IIdempotencyStore`). Local broker: `localhost:9092`,
  Kafka UI on `:8085`.
- **Poison-message handling (Phase 10.2):** the shared idempotent consumer base
  (`KafkaConsumerBackgroundService` → `EventMessageProcessor`) retries a failing handler
  a bounded number of times in-process, then — if dead-lettering is enabled — forwards the
  original message to `<topic>.dlt` (e.g. `exercise.completed.dlt`) and commits the offset,
  so a single poison message can never block its partition. The policy is opt-in via the
  strongly-typed `ConsumerResilienceSettings` (config section `Kafka:ConsumerResilience`)
  with safe defaults: 3 retries, 500 ms linear back-off, dead-lettering on. Set
  `DeadLetterEnabled=false` to fall back to the previous redeliver-forever behaviour. The
  dead-letter topic suffix (`.dlt`) is the `Topics.DeadLetterSuffix` constant. DLT messages
  carry `x-dead-letter-reason` / `x-dead-letter-at` headers for diagnostics; replay is a
  manual operator action (re-produce the value onto the source topic).
- **API Gateway (YARP):** single entry point; validates the JWT once and forwards
  `X-User-Id` / `X-User-Role` headers downstream (client-supplied copies are stripped).
  **Authorization source of truth is the JWT itself:** every service independently
  re-validates the bearer token (shared `Jwt:Key`/`Issuer`/`Audience`) and authorizes
  off its claims via `[Authorize]` policies — defense-in-depth, so a service is never
  open even if reached directly. The forwarded headers are a convenience/diagnostic
  signal, **not** a trust boundary; services must not authorize off them. The
  strangler-fig migration is finished: it routes every prefix to its owning service and
  has **no catch-all** (unknown routes return 404).
- **Transactional outbox (Phase 10.3):** to make a state change and its event publish
  atomic, a producer can write an `OutboxMessage` row in the *same* EF transaction as its
  business change (`IOutboxWriter.Enqueue` stages the row; the caller's single
  `SaveChangesAsync` commits both). A per-service `OutboxRelayBackgroundService` then polls
  pending rows (`IOutboxStore`), forwards each stored envelope to Kafka verbatim
  (`IOutboxEventForwarder`), and marks it dispatched — at-least-once with no lost events on
  a crash between DB commit and Kafka produce. Shared building blocks live in
  `BuildingBlocks/Outbox`; **gamification, identity and learning are all fully wired** — each
  has its own `OutboxMessages` table + `AddOutboxMessages` migration, per-service store/writer,
  and relay hosted service, with every outgoing event routed through the outbox. (Gamification
  was the original reference; identity's `user.*` and learning's
  `exercise/lesson/skill.completed` producers were converted in the same way — the enqueue is
  staged before the business `SaveChangesAsync` so state + event commit atomically.) These three
  were the named scope of roadmap 10.3 (the producers whose events drive cross-service state).
  Other producers publish directly and can adopt the same shared building blocks if/when their
  events need the same guarantee: social (`KafkaSocialEventPublisher`), ai
  (`KafkaDialogEventPublisher`), company (`FollowUpReminderService`, see the producer-only note
  below) and organization (`OrganizationService` / `OrganizationProfileService`) all inject
  `IEventPublisher` and publish outside the business transaction.
- **Organization service (Phase 40.5):** new microservice `organization-service` (not an
  extraction — the tenant registry did not exist before), port 5010, database `organization`.
  Owns the tenant registry (`Organizations`, not tenant-scoped, no RLS — see docs/DECISIONS.md)
  and the per-organization content profile (`OrganizationProfiles`, tenant-scoped: `ITenantScoped`,
  `EnableTenantRls`, EF query filter, `[TenantScoped]` on its controller). Producer-only, same
  registration pattern as `company-service`: `KafkaTopicProvisioner` + `KafkaEventPublisher`
  directly, no `AddSellevateEventing` (no consumer, so no need for the Redis idempotency store).
  Publishes `organization.created` / `organization.updated` / `organization.suspended`. See
  [ORGANIZATION_SERVICE.md](ORGANIZATION_SERVICE.md).
- **Producer-only service (Phase 39.11):** `company-service` produces `company.followup.due`
  (a hosted `FollowUpReminderBackgroundService` polling every `FollowUpReminder:PollIntervalMinutes`,
  default 5 min, for companies whose `NextActionAt` is due and not yet notified) but never
  consumes. It registers the Kafka publisher + topic provisioner directly rather than the
  shared `AddSellevateEventing` helper, since that helper also wires the Redis-backed consumer
  idempotency store — an unneeded dependency for a producer-only service. It does **not** use
  the Outbox: the reminder poll already reads from Postgres on its own schedule, so there is no
  separate "business change that must commit atomically with an event" to protect — the poll
  claims a company (sets `FollowUpNotifiedAt`, commits) *before* publishing to Kafka — the whole
  claimed batch (up to `FollowUpReminder:BatchSize`, default 100) is committed in one
  `SaveChangesAsync`, then each company is published individually. This is a deliberate
  at-most-once trade-off for a single-instance service: a single publish failure only drops that
  one company's reminder (each publish is individually try/caught), but a process crash *between*
  the claim commit and the publish loop — or a broker outage that fails every publish in the loop
  — silently drops **up to the whole in-flight batch** (bounded by `BatchSize`, not unbounded)
  for that tick, since every claimed company is already marked notified and will not be
  reconsidered. This favors "never double-notify" over guaranteed delivery. The user can always
  force a fresh reminder for an affected company by rescheduling `NextActionAt`, which resets
  `FollowUpNotifiedAt`. Revisit with the Outbox pattern if guaranteed delivery becomes a
  requirement.
- **Data ownership:** the original single `AppDbContext` (42 entities) is split into a
  database per service per [DATA_OWNERSHIP.md](DATA_OWNERSHIP.md); each service owns its
  own schema + EF migrations.
- **Tenancy primitives (Phase 40.1):** `BuildingBlocks/Tenancy` (namespace
  `Sellevate.BuildingBlocks.Tenancy`) holds the foundation multi-tenancy is built on — see
  [docs/TENANCY/TENANCY.md](TENANCY/TENANCY.md) for the full design. `ITenantScoped`
  marks an entity as carrying an `OrganizationId`. `ITenantContext` (`OrganizationId`,
  `IsPlatformWide`, `IsSystem`) is the read-only view consumed by application code; its scoped
  implementation `TenantContext` exposes explicit `SetOrganization(organizationId)` /
  `EnterPlatformMode()` / `EnterSystemMode()` mutators so only the code that actually establishes the tenant for
  a unit of work (gateway-driven middleware for requests, an explicit per-organization or
  system-mode setup for background jobs — see TENANCY.md §1.6) can populate it; each
  method may only run once per scope (calling either after the context is already
  populated throws `InvalidOperationException`). The three modes and what each one widens are
  TENANCY.md §1.6a: platform-wide (validated Sellevate staff, reads only) is deliberately separate
  from system (a background job with no principal, connecting under a `BYPASSRLS` role), and the
  two are mutually exclusive. `TenantSaveChangesInterceptor :
  SaveChangesInterceptor` is the write-side guard: on `Added` entries it stamps
  `OrganizationId` from the current tenant (or rejects a foreign one already set), and on
  `Modified`/`Deleted` it compares against `OriginalValues` so an entity loaded via
  `IgnoreQueryFilters()` cannot be reassigned to a different organization in-flight;
  `tenant.IsSystem` bypasses the guard entirely. Both `SavingChanges` and
  `SavingChangesAsync` are overridden — this codebase is async-only, so a sync-only
  interceptor would never fire. A mismatch throws `CrossTenantWriteException` (entity
  name + the *expected* organization id only — the foreign id is never included in the
  message). Registration: `services.AddSellevateTenancy()` (new method on
  `BuildingBlocksServiceCollectionExtensions`) registers `TenantContext` scoped as both
  itself and `ITenantContext`, plus `TenantSaveChangesInterceptor` scoped; a consuming
  service still adds the interceptor to its own `DbContext` via `AddInterceptors` and is
  responsible for populating `ITenantContext` per request/job. When 40.1 shipped this building
  block was registered by nothing — it was primitives + tests (`BuildingBlocks.Tests/Tenancy`) and
  the wiring was deferred. **Blocks 40.2–40.13 did that wiring, so that statement is now false:**
  `AddSellevateTenancy()` is called by all seven Postgres-backed services (ai, organization,
  company, gamification, learning, identity, social), and `UseSellevateTenantContext()` by all
  nine — analytics and notification have no relational database but still establish the tenant
  context for their Redis/Mongo keys. This is Layer 2 (EF, convenience) of the three-layer
  isolation model in TENANCY.md §1 — Postgres RLS (Phase 40.4) is the layer that actually
  survives raw SQL / `ExecuteUpdate`.
- **Gateway + context propagation (Phase 40.2):** `IdentityHeaders.OrganizationId` (`X-Organization-Id`)
  and `IdentityHeaders.ResolveOrganizationId(ClaimsPrincipal)` (reads the `org_id` claim) extend the
  same trusted-header pattern `X-User-Id`/`X-User-Role` already use. `Gateway/IdentityForwarding.cs`
  strips any client-supplied `X-Organization-Id` and re-sets it only from the validated token —
  identical treatment to the user-identity headers, in the same method. Downstream,
  `BuildingBlocks/Tenancy/TenantContextMiddleware` reads the header, calls `TenantContext.SetOrganization`
  when it parses as a `Guid`, and returns `403 Forbidden` (not `401` — the caller already has a
  validated identity, it just lacks organization context) when a route carrying `[TenantScoped]`
  (or built with the `.RequireTenantScope()` endpoint-convention extension) has no valid header.
  Because ASP.NET Core opens one DI scope per request and the middleware calls `SetOrganization`
  at most once per request, the 40.1 set-once guard does not conflict with the request pipeline;
  it only requires background jobs to open a fresh scope per unit of work (already the documented
  pattern in TENANCY.md §1.6). The organization-boundary rule — never read `organizationId` from
  body/query/route — is enforced by `scripts/tenancy-boundary-lint.py`, wired into CI as the
  `tenancy-boundary` workflow, scanning the whole backend (not just one service, unlike
  `codestyle-lint.py`). `organization-service` (Phase 40.5) is the first live consumer:
  `UseSellevateTenantContext()` is wired into its pipeline and `OrganizationProfileController` is
  `[TenantScoped]`.
- **Postgres RLS infrastructure (Phase 40.4):** this is Layer 3 of TENANCY.md §1 — the layer that
  survives a forgotten EF filter, Dapper, or `ExecuteUpdate`/`ExecuteDelete`. Two new pieces in
  `BuildingBlocks/Tenancy`, both provider-agnostic (no `Npgsql` package reference in
  `Sellevate.BuildingBlocks` itself — only `Microsoft.EntityFrameworkCore.Relational` for the
  migration-builder types):
  - `TenantConnectionInterceptor : IDbTransactionInterceptor` hooks `TransactionStarted` /
    `TransactionStartedAsync` — which fires for every transaction, including EF's own implicit
    per-`SaveChangesAsync` transaction — and issues `SET LOCAL app.organization_id = '<guid>'`,
    **never** a bare `SET`, so the value cannot outlive the transaction or leak onto the next
    request that borrows the same pooled connection. Since the platform-staff work it emits a
    second statement, `SET LOCAL app.platform_mode = 'on'`, whenever the context is platform-wide
    (`TenantConnectionInterceptor.cs:68,98-99,121`). The two are independent, not either/or — a
    Sellevate administrator who also belongs to an organization gets both — so the interceptor is
    a no-op (returns `null` from the testable `BuildSetLocalCommandText()`) only in system mode,
    which relies on a `BYPASSRLS` role instead of the GUCs, or when there is *neither* an
    organization nor platform-wide mode. Platform-wide with no organization is **not** a no-op.
    Registered
    scoped by `AddSellevateTenancy()`, same as `TenantSaveChangesInterceptor`; a consuming service
    still adds it to its own `DbContext` via `AddInterceptors` when it actually gets tenant-scoped
    tables. `organization-service` (Phase 40.5) is the first to do so, on `OrganizationProfiles` —
    the Stage C rollout (40.10+) repeats the same pattern per service.
  - `TenantRlsMigrationBuilderExtensions.EnableTenantRls(table)` /
    `EnableTenantRlsForContent(table)` — migration-time helpers that emit `ENABLE` + `FORCE ROW
    LEVEL SECURITY` and a policy with both `USING` and `WITH CHECK`. The base comparison is
    `"OrganizationId" = NULLIF(current_setting('app.organization_id', true), '')::uuid` (the
    content variant adds `"OrganizationId" IS NULL OR ...`). The `NULLIF` is load-bearing, not
    decorative — see docs/DECISIONS.md (2026-08-15) for how the real-Postgres integration test
    caught its absence.

    **The two halves are no longer symmetric.** This document used to say the policy applies the
    same comparison to `USING` and `WITH CHECK`; the `RefreshTenantPoliciesForPlatformStaff`
    migration of 2026-08-16 (present in all seven Postgres services) changed that. `USING` now
    additionally admits
    `COALESCE(NULLIF(current_setting('app.platform_mode', true), ''), 'off') = 'on'`, while
    `WITH CHECK` keeps the plain organization comparison
    (`TenantRlsMigrationBuilderExtensions.cs:126-138`). The point is deliberate and worth stating
    plainly: **visibility is widened for validated platform staff, authorship is not.** A Sellevate
    administrator reads across every organization and still cannot write a row into one they did
    not name explicitly. Because `ApplyPolicy` does `DROP POLICY IF EXISTS` first, re-running the
    helper replaces the policy rather than failing, and passing `admitPlatformStaff: false`
    regenerates the old symmetric policy through the same code path — which is exactly how that
    migration's `Down` is written.
  - `TenantRowLevelSecurityIntegrationTests` (`BuildingBlocks.Tests/Tenancy`) exercises both
    against a real, throwaway local Postgres database and a non-superuser, non-owner,
    `NOBYPASSRLS` role — raw SQL, `ExecuteDelete`, and an `INSERT` carrying a foreign
    `OrganizationId` — skipping cleanly (not failing) when no local Postgres is reachable. See
    docs/TESTING/TENANCY.md.
  - `docs/TENANCY/sql/create_sellevate_app_role.sql` is the (unexecuted) script for the real
    `sellevate_app` role; `scripts/tenancy-pool-lint.py` (CI: `tenancy-pool`) forbids
    `AddDbContextPool` anywhere in the backend, per the CODESTYLE.md rule it enforces.

## `TenantTransactionScope` and `[TenantTransaction]` — why reads need a transaction

This is the mechanism the whole RLS layer quietly depends on, and it was missing from this document
for ten blocks while ten other docs described it.

`TenantConnectionInterceptor` hooks `TransactionStarted`. `SET LOCAL` has no effect outside a
transaction. Put those two facts together and the consequence is not obvious but it is severe:
EF opens an implicit transaction for every `SaveChangesAsync`, so a **write** is covered for free,
but a plain **read** outside a transaction runs with `app.organization_id` unset. Under a
fail-closed policy (`current_setting(..., true)`, missing_ok) that read returns *zero* tenant rows —
no error, no log line, just an empty list. The failure mode is an administrator opening a record
they created a minute ago and finding it gone.

Two pieces close that gap:

- **`TenantTransactionScope`** (Phase 40.10) — the one transaction pattern in ai-, company-,
  gamification-, learning- and social-service, each service owning a copy under its own
  `Infrastructure/Data/`. The rule, stated once so it is not re-litigated per call site: every
  service method touching a tenant-scoped table — or any content table, which may now hold
  organization-owned rows — opens exactly one scope as its first statement. `BeginReadAsync` rolls
  back on dispose (it exists to make rows visible, never to persist); `BeginWriteAsync` plus an
  explicit `CommitAsync` for methods that also write. Both are re-entrant — a nested call finds a
  transaction already open and becomes a no-op, so the outermost scope owns the transaction — which
  means a write scope must never be nested inside a read scope, or its commit is swallowed.
- **`[TenantTransaction]`** (Phase 40.18) — an action filter, defined in ai-service
  (`Infrastructure/Data/TenantTransactionAttribute.cs`) and learning-service
  (`Features/Content/TenantTransactionAttribute.cs`), that opens a write scope around the whole
  action and commits it if the action did not throw. It exists for the admin controllers that talk
  to the content tables directly and opened no scope of their own. That was survivable while
  content was global, because the content policy admits `OrganizationId IS NULL` rows even with the
  session variable unset; the moment 40.18 let an organization own content, those endpoints started
  silently losing rows. It is a filter on the *controller* rather than a scope in each of twenty
  actions precisely because the failure mode of the per-action version is somebody adding action
  twenty-one. It is currently applied to 12 controllers: `AdminDialogController`,
  `AdminDialogSessionsController`, `AdminDialogOverridesController`, `AdminAiQuotaController`,
  `AiQuotaPreflightController` (ai) and `AdminContentAdaptationController`,
  `AdminContentGenerationController`, `AdminExercisesController`, `AdminLessonsController`,
  `AdminReferenceController`, `AdminTeamSkillGapsController`, `AdminTechniquesController`
  (learning).

## Service-to-service HTTP

Kafka carries facts that already happened; anything a request needs *now* goes over HTTP. Every hop
below is a typed client authenticated with `X-Internal-Service-Secret` and checked on the receiving
side by that service's `InternalServiceAuthFilter` — these routes are not exposed through the
gateway.

| From → to | Client | Purpose |
|---|---|---|
| learning → ai | `AiEvaluationClient` | `/ai/evaluate` — free-text / rewrite / spot-mistake grading |
| learning → ai | `AiQuotaClient` | per-organization spend preflight before an LLM call |
| learning → ai | `AiContentPipelineClient` | `/ai/content/*` — generation and adaptation steps |
| learning → ai | `AiChatClient`, `AiTtsClient` | exercise dialog and TTS, moved here by block 40.33 |
| learning → identity | `IdentityOrganizationMemberDirectory` | `GET /internal/memberships/active` — who is in the org |
| ai → learning | `AssignmentPracticeContextClient` | `GET /internal/assignments/practice-context` |
| company → ai | `BriefingAiClient`, `ParseLogAiClient`, `PersonaAiClient`, `ReadinessAiClient` | the four company AI surfaces |

The ai → learning hop is deliberately **fail-open**: `AssignmentPracticeContextClient` returns
`null` on any non-success, timeout or exception rather than throwing, so a learning-service outage
degrades the dialog's assignment context instead of taking dialog down with it. The learning → ai
hops are not fail-open — an evaluation that cannot reach ai-service is an error, because silently
grading nothing would be worse than failing loudly.

## EF Column Types

| Property | Column type |
|---|---|
| `Exercise.SerializedContent` | `jsonb` |
| `UserExerciseAttempt.SerializedAnswer` | `jsonb` |
| `UserExerciseAttempt.SerializedAiFeedback` | `jsonb` |
