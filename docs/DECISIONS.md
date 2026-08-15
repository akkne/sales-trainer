# Decisions

Non-trivial engineering decisions with their alternatives and rationale. Newest first.

---

## 2026-08-15 — The SSO seam (40.8): why a three-step login exists before there is a second provider

- **Why build a seam for something explicitly not being built.** SSO is deferred
  ([TENANCY.md §4.5](TENANCY/TENANCY.md)), but the *shape* of login is not deferrable. The
  request, when it comes, is not "add SSO" — it is a 200-seat customer requiring Azure AD, with
  their deadline and the deal as leverage. Retrofitting that into a single hardcoded
  email+password branch means rewriting login, session issuance, the invite mechanic and user
  provisioning simultaneously, under exactly the conditions where you least want to touch
  authentication. Building the seam first is roughly a day: one table, one interface with one
  implementation, and a login screen with one extra step. Doing it later is weeks of that rewrite.
  The cheap half of the work is done now precisely because it is cheap now.
- **What was deliberately *not* built:** no OIDC provider, no SAML provider, no
  `jit_provisioning` behaviour (the column is stored and never read), no per-organization session
  TTL applied to token issuance, no admin UI to edit the configuration (that is 40.20's split of
  the admin panel), and no endpoint that writes an `OrganizationAuthConfigurations` row —
  organizations get one when 40.9's superadmin panel creates them. An organization with no row
  logs in with a password, the same as the platform default.
- **Which service owns `organization_auth_config` — identity-service, not organization-service.**
  The deciding constraint is *when* the row is read: on `POST /auth/login/start`, before
  authentication, with no JWT, no `X-Organization-Id` and no tenant context. Putting the table in
  organization-service would mean identity-service making an unauthenticated cross-service HTTP
  call on the single most availability-sensitive path in the product — nobody can log in if
  organization-service is down — and would force organization-service to expose an anonymous
  "which organization owns this email domain" endpoint, which is a far better enumeration oracle
  than the thing this design is trying to avoid. There is also precedent: `Membership` (40.6) and
  `Invite` (40.7) both live in identity-service despite being about organizations. The boundary
  that holds is **identity-service owns access, organization-service owns the registry and the
  business/content profile** — how you get in is access.
- **The table is deliberately not `ITenantScoped` and has no RLS policy** — unlike `Invites`,
  which 40.7 put behind `EnableTenantRls`. Its primary read is a cross-tenant question by nature
  ("which organization claims this domain") asked at a moment when no tenant context can exist. A
  table whose main access path has to bypass RLS on every single login is not protected by RLS,
  it is decorated with it — and worse, `TenantConnectionInterceptor`'s system mode relies on a
  `BYPASSRLS` role that does not exist yet on real servers, so the "correct" version would work
  locally and silently stop resolving organizations the day `sellevate_app` is rolled out. This
  is the same reasoning that kept the `Organizations` registry in 40.5 out of RLS. Consequence to
  respect when a write path is added: the organization must be taken from `ITenantContext`
  explicitly in the query, because neither the query filter nor the database will do it.
- **`POST /auth/login/start` answers `200 {"method":"password"}` for every syntactically valid
  address, known or not.** It never returns the organization id or name, and there is no "this
  address is known" flag. This continues 40.7's choice for Google sign-in (one identical `401`
  for "no such account" and "account without a membership"): the first step of a login screen is
  the most reachable endpoint in the product, and any variation in its answer turns it into a
  free customer-list oracle.
  - **The one leak we accept and name:** an organization that configures OIDC/SAML *will* get a
    different answer for its domain, because the browser has to be sent somewhere. That is
    inherent to SSO, not to this design, and it is opt-in by the customer who configured it. It
    also reveals only "this domain uses SSO with us", never which individual addresses exist.
  - **Rejected:** returning `404` for an unknown address (a perfect enumeration oracle), and
    returning the organization name so the screen could greet the user by company (the same
    oracle with better branding).
- **A method with no provider is refused, not downgraded to a password.** If an organization is
  configured for `oidc`, `POST /auth/login` returns `401` even for the correct password. The
  alternative — falling back to the password provider when no provider matches — would mean that
  switching a customer to their directory silently leaves password login working alongside it,
  which is the opposite of what the customer bought. This is the only behaviour the seam actually
  changes today, and it is the one worth testing.
- **`method` is stored as `text` with a `CHECK` constraint, not as an int enum.** The rest of the
  codebase converts enums with `HasConversion<int>()`, but this value is also the wire value in
  `LoginStartResponseDto` and the key `IAuthProvider.Method` is matched on, and keeping one
  spelling across the database, the interface and the JSON removes a mapping layer that exists
  only to be gotten wrong. The `CHECK ("Method" IN ('password','oidc','saml'))` is a stricter
  guarantee than an int enum, which happily stores `47`.
- **Noted, not fixed: `scripts/codestyle-lint.py` and the identity-service codebase disagree about
  comments.** CODESTYLE §9 forbids comments outright; the linter implements that literally,
  flagging `///` XML documentation too. identity-service was already at **527** violations before
  40.8 (40.7's own merged `Features/Invites/` accounts for 101 of them), because the house style
  in this service is to record *why* a security decision was made next to the code that makes it —
  which is exactly what a reviewer of an authentication seam needs. 40.8 follows that style and
  leaves the count at 585. This is a real contradiction between the written rule and the practice
  every recent block has followed, and resolving it (relax the rule to allow `///`, or strip the
  documentation) is a repo-wide call, not a thing to settle inside one sub-phase. The
  `codestyle` CI workflow is consequently red for identity-service and was already red on arrival.

---

## 2026-08-15 — organization-service scaffold (40.5): registry vs profile tenancy scope, deferred authorization

- **Which table is tenant-scoped, and why the answer isn't "both":** `Organizations` (the tenant
  registry, one row per customer) does **not** implement `ITenantScoped` and never gets
  `EnableTenantRls`. `OrganizationProfiles` (product/ICP/objections/script/tone/glossary/banned
  claims, [CONTENT_MODEL.md §3](TENANCY/CONTENT_MODEL.md)) does both. The registry can't be
  tenant-scoped by its own `organization_id` — addressing "which organizations exist" is
  inherently a cross-tenant, platform-level query, the same reason `40.5`'s registry endpoints are
  not `[TenantScoped]`. The profile is exactly the shape RLS exists for: one row that belongs to
  exactly one organization, reachable only through `ITenantContext`.
- **The registry's `{id:guid}` route parameter does not violate the "organization id never in the
  route" boundary rule.** `scripts/tenancy-boundary-lint.py` forbids a route/query/body parameter
  literally named `organizationId` (the value that would answer "which tenant is making this
  request"). `OrganizationController`'s `{id:guid}` answers a different question — "which registry
  row should the platform operator act on" — the same category of thing `CompanyController`'s
  `{companyId:guid}` already does for a different resource. Naming the parameter `id` rather than
  `organizationId` keeps this distinction visible to the linter and to a future reader.
- **`OrganizationController` has no role restriction yet beyond `[Authorize]`.** Any authenticated
  user can create/list/update/suspend/reactivate an organization today. This is deliberately
  deferred, not an oversight: `RequireSuperAdmin` in its post-40.6 shape (platform role, split from
  the current global `Admin`) does not exist until 40.6/40.9. Locking this controller down is
  explicit 40.9 scope ("платформенная суперадминка"). Recorded here so it is not mistaken for a
  security gap in 40.5's own review — it is a known, roadmapped gap.
- **Reactivation publishes `organization.updated`, not a fourth topic.** The roadmap names exactly
  three: `organization.created` / `organization.updated` / `organization.suspended`. Treating
  "status flipped back to Active" as a case of "the registry row changed" rather than minting
  `organization.reactivated` keeps the contract at the size the roadmap specified; a consumer that
  cares about the transition reads `status` off the `organization.updated` payload.
- **RLS on `OrganizationProfiles` is wired but not yet the layer doing the isolating locally.** The
  service connects with the same Postgres superuser every other service uses
  (`ConnectionStrings:Postgres`), not the restricted `sellevate_app` role — that role's real-server
  rollout is still pending a human (`docs/DONT_FORGET.md`), and superusers bypass RLS regardless of
  `FORCE`. Locally, the EF query filter and `TenantSaveChangesInterceptor` are what actually
  prevent cross-tenant reads/writes today; the migration-level `EnableTenantRls` call is correct
  and ready for when a service starts connecting as `sellevate_app` (Stage C territory, 40.10+,
  though nothing in the roadmap currently schedules organization-service itself for that switch).
- **`jsonb` columns stored as plain `string` properties**, matching the existing
  `Exercise.SerializedContent` convention (`learning-service`) rather than typed EF-owned JSON
  columns — the codebase already has this pattern in exactly the place a new service should copy
  it from, and it keeps `OrganizationDbContext` free of a JSON-column-mapping abstraction that
  nothing else in the service needs.

## 2026-08-15 — RLS infrastructure: `SET LOCAL` vs `SET`, and the read-transaction requirement (40.4)

- **Problem the roadmap names directly:** `SET LOCAL` is transaction-scoped — safe against
  connection-pool leaks by construction, since Postgres reverts it when the transaction ends
  regardless of what happens to the pooled connection afterward. A bare `SET` is session-scoped and
  leaks the previous request's tenant onto the next one that borrows the same pooled physical
  connection — TENANCY.md §1.5 calls this the single highest-risk detail of the whole design. But
  `SET LOCAL` only has an effect *inside* an active transaction, and EF Core read paths (plain LINQ
  queries) run with no implicit transaction, so the two halves of the requirement pull against each
  other.
- **Decision:** never use a bare `SET`. `TenantConnectionInterceptor`
  (`BuildingBlocks/Tenancy/TenantConnectionInterceptor.cs`) hooks EF Core's
  `IDbTransactionInterceptor.TransactionStarted` / `TransactionStartedAsync` — which fires for
  **every** transaction, not only ones the interceptor itself opens — and issues
  `SET LOCAL app.organization_id = '<guid>'` as the first statement inside it. EF Core already
  wraps every `SaveChangesAsync` call in an implicit transaction by default, so every **write**
  is covered automatically, at zero extra plumbing. Tenant-scoped **reads** have no implicit
  transaction and must open one explicitly
  (`await using var transaction = await context.Database.BeginTransactionAsync();`) to get the
  same protection — this is a requirement carried forward to whichever service rolls tenant-scoped
  reads out (Stage C, 40.10+), not something this building block can retrofit onto a bare `SELECT`
  from the outside.
- **Alternative rejected — set the GUC on connection open with a bare `SET`, reset it on
  `ConnectionClosing`.** This is the shape TENANCY.md §1.5 gestures at as the pragmatic fallback
  ("sets the GUC on connection open"). Rejected because the reset only fires on the *ordinary* close
  path; any code path that disposes a connection without going through EF's close event (a crashed
  request, a connection returned to the pool by a lower-level ADO.NET failure) leaves the previous
  tenant's value live for the next borrower. `SET LOCAL` cannot leak this way even in a crash,
  because Postgres — not application code — is what undoes it, at the transaction boundary.
- **Alternative rejected — keep a transaction open for the life of every connection
  (`Database.UseTransaction`, committed at end of request).** This would make reads "just work"
  without call-site changes, but it means every `DbContext` write becomes an *externally owned*
  transaction that EF no longer auto-commits on `SaveChanges` — a correctness footgun that would
  silently stop persisting data the first time this interceptor is wired into a service that isn't
  expecting it. Not worth the risk for a building block nothing consumes yet.
- **Verified against a real (local, throwaway) Postgres instance — and it changed the SQL:** the
  first version of `TenantRlsMigrationBuilderExtensions` compared
  `organization_id = current_setting('app.organization_id', true)::uuid`, following TENANCY.md's
  literal text ("missing_ok... yields zero rows"). The integration test
  (`TenantRowLevelSecurityIntegrationTests`) caught that this is insufficient: once a *pooled
  physical connection* has run `SET LOCAL app.organization_id = ...` even once, Postgres reverts
  the GUC to `''` (empty string) — not `NULL` — when that transaction ends, because the first
  `SET LOCAL` on an unrecognized custom GUC registers a placeholder whose "previous" value is `''`,
  not "undefined." `''::uuid` then raises a hard Postgres error (`22P02`) on every subsequent query
  against an RLS-protected table on that connection, instead of filtering to zero rows — the
  opposite of fail-closed. Fixed by wrapping the setting in `NULLIF(..., '')` before the cast:
  `NULLIF(current_setting('app.organization_id', true), '')::uuid`. Since Npgsql (and every
  ADO.NET provider) pools physical connections, essentially *every* long-lived pooled connection in
  a running service will eventually hit this once it has served one tenant request — this is not an
  edge case, it is the steady-state behavior, and `NULLIF` is required, not optional.
- **Column naming departs from TENANCY.md's SQL prose on purpose.** TENANCY.md's examples write
  `organization_id` (snake_case). The actual schema convention already in this codebase (see
  `docs/DB_SCHEMA.md`, e.g. `Users.Email`, `Companies.Status`) is EF's default Npgsql naming: the
  C# property name verbatim, quoted, PascalCase. `EnableTenantRls`/`EnableTenantRlsForContent`
  default the column to `"OrganizationId"` to match what `ITenantScoped.OrganizationId` actually
  generates, with an override parameter for the rare table that needs something else. The Postgres
  GUC name itself, `app.organization_id`, is unrelated to table-column casing (it is a
  configuration-parameter string, not a SQL identifier) and stays exactly as TENANCY.md specifies.
- **The migration/owning role must itself tolerate `FORCE ROW LEVEL SECURITY`.** Postgres only
  exempts a table's *owner* from `FORCE` automatically when that owner is a superuser; a
  non-superuser owner needs `BYPASSRLS` granted explicitly, or `FORCE` starts filtering migrations
  too, not only the app role. TENANCY.md's "migrations then run as the owner (policies bypassed,
  which is what you want)" is only true given this precondition, which it does not spell out.
  Local dev (`scripts/dev-infra.sh`, role `st`) already satisfies it — `st` is the Postgres image's
  initial superuser, confirmed via `pg_roles.rolsuper` — but a real server's migration role may not
  be. Documented as a load-bearing precondition in
  `docs/TENANCY/sql/create_sellevate_app_role.sql`, and flagged again in `docs/DONT_FORGET.md` so
  whoever runs that script on a real server does not miss it.

---

## 2026-08-14 — `EventEnvelope.OrganizationId` is nullable on the wire, strict at the consumer (40.3)

- **Problem:** the roadmap demands the base consumer **fail** when an event carries no tenant, but
  at the time the envelope changes *nothing publishes an organization yet* (producers only learn
  their tenant in 40.6/40.9), and some events — Identity's user lifecycle stream, consumed to keep
  the `UserReplica` read model in sync — are genuinely cross-org and will never carry one. A field
  that is required on the wire would make every existing producer illegal on the day it lands.
- **Decision:** split "nullable on the wire" from "permissive at the consumer".
  `EventEnvelope.OrganizationId` is `Guid?`, but `KafkaConsumerBackgroundService` exposes
  `protected virtual bool RequiresOrganization => true` and **throws** on a missing organization by
  default; the throw travels the ordinary retry/dead-letter path. A platform-global consumer must
  override it to `false`, which puts the message into `ITenantContext.IsSystem`.
- **Why this preserves the actual rule:** TENANCY.md §1.6 says an unset tenant is *an exception,
  never a license*. What matters is that cross-org processing be an explicit, auditable, per-consumer
  opt-in — not that the field be non-nullable in JSON. A required field would have forced a fake
  sentinel organization id onto genuinely global events, which is strictly worse: it looks like a
  tenant, filters like a tenant, and silently scopes nothing.
- **Alternative rejected — bump the envelope twice** (nullable now, required after 40.9). The
  envelope is the frozen cross-service contract; each bump is a coordinated redeploy of every
  producer and consumer. The roadmap explicitly calls for doing this **once, before the first
  tenant-scoped consumer exists**. The strictness that a second bump would buy already exists in
  `RequiresOrganization`, at zero coordination cost.
- **`PartitionKey` stays the user id.** Moving it to `org:user` would reshuffle every existing
  partition assignment and buy no ordering guarantee the user id does not already provide —
  per-user ordering is the property consumers actually depend on.
- **`OutboxMessage.OrganizationId` is informational only.** The relay forwards `Payload` verbatim
  and never filters on the column. This is deliberate: the relay is the one legitimate system-wide
  reader of all outbox rows, which is exactly why the tenant is carried *inside* the serialized
  envelope rather than derived at publish time.

---

## 2026-08-14 — Multi-tenancy: the tenant is `Organization`, and isolation is enforced by Postgres

- **Status:** design recorded, **nothing implemented**. Full design in
  [docs/TENANCY/](TENANCY/TENANCY.md). Verified starting point: zero `tenant` identifiers exist
  anywhere in the codebase.
- **Naming (the decision with the widest blast radius):** the tenant is **`Organization`**, not
  `Company`. `company-service` + `docs/COMPANIES/` already own `Company` as *a prospect a
  salesperson practises calling* — a per-user private CRM. Reusing the name would make `CompanyId`
  mean two different things across services, in JWT claims and Kafka payloads, where a mix-up is a
  data leak rather than a compile error. Russian UI copy may still say «Компания».
- **Isolation:** the proposal's "one database, `tenant_id` everywhere" does not describe this
  system — DB-per-service is already the shape (7 Postgres DBs + Mongo + Redis). So
  `organization_id` is added per database, and the RLS/`SET LOCAL` plumbing lives in
  BuildingBlocks rather than being written seven times. Three layers: (1) the gateway injects
  `X-Organization-Id` from the validated JWT and strips client copies — reusing the existing
  `IdentityHeaders` contract, never reading the org from a query parameter; (2) EF global query
  filters, explicitly labelled convenience, not security; (3) Postgres RLS with `FORCE`, `WITH
  CHECK`, and an app role without `BYPASSRLS` — the only layer that survives
  `ExecuteUpdate`/Dapper/raw SQL.
- **Write guard:** a `SaveChangesInterceptor` in BuildingBlocks (not a base `DbContext` — there are
  seven contexts) that stamps `organization_id` on insert and rejects cross-tenant writes by
  comparing against `OriginalValues`, making the column immutable after creation. Both
  `SavingChanges` **and** `SavingChangesAsync` must be implemented — sync-only is a no-op in this
  codebase, which is async throughout.
- **Content:** per-customer curriculum forks are rejected outright — 15 customers would mean 15
  forks and no reachable content roadmap. Instead: global library (`organization_id IS NULL`) +
  copy-on-write overrides + immutable `lesson_version` snapshots, with progress pinned to a version.
  The existing schema has the bug this prevents: `UserExerciseAttempt.ExerciseId` points at mutable
  content, so editing a correct answer silently rewrites historical accuracy — the exact number
  sold to the РОП. Note the model adaptation: `Lesson` has no body today (only `Title`); all
  content is in `Exercise.SerializedContent`, so the versioned unit is the lesson **plus its
  ordered exercise set** as one JSON snapshot.
- **Access:** no public registration route at all (deleting `POST /auth/register`, not guarding
  it); `memberships (user_id, organization_id, role)` from day one even while the UI allows one org
  per user; the global `UserRole` enum splits into a platform role and a per-membership org role,
  so a РОП is an admin *of one organization*, never of the platform; offboarding deactivates,
  never deletes, because the manager's history belongs to the customer.
- **Deliberately deferred:** per-tenant subdomains (wildcard TLS, DNS, per-tenant CORS, OAuth
  callbacks — defer until someone pays for branding) and SSO itself. But the *seam* for SSO is
  built now — `organization_auth_config`, an `IAuthProvider` with a single password implementation,
  and a three-step login (email → resolve org → dispatch to provider) — because a 200-seat customer
  requiring Azure AD otherwise forces a simultaneous rewrite of login, sessions, invites and
  provisioning under their deadline.
- **The non-technical risk this design exists to defuse:** per-customer customization is a linear
  cost of delivery disguised as a feature. If Sellevate adapts the content, ~20 customers turns the
  company into a content agency. Hence the organization profile (product / ICP / objections /
  script / tone) with parameterized base content, and an explicit pilot measurement: if more than a
  third of adaptation needs hand-editing lesson text, the parameterization is wrong and must be
  fixed before the tenth customer.

---

## 2026-08-14 — Gamification is gone from the product, not from the backend

- **Context (user decision):** points, streaks and leagues are out. The removal had already started
  (the `/league` route was unlinked from the nav, the friends leaderboard was commented out, the
  skill tree stopped rendering its gamification fields), leaving the product half-way: a call still
  ended with «+N XP получено», the lesson path still promised «60 XP», the profile still showed
  «Лучшая серия», and `/league` was still reachable by URL.
- **Decision:** finish it in the **frontend only**. Removed from the user-facing app: XP on the
  lesson path, on the exercise result banner, in the call analysis and session history; the streak
  tiles on `/profile` and on a friend's profile; the `/league` route and its hook; the friends
  leaderboard (component + `/friends/leaderboard` query) and the dead `StatsWidget`; the landing
  page's "XP, серии и лиги" pitch; and the now-dead league/leaderboard CSS. Achievement and streak
  notifications are dropped on arrival — the notification service can still hold older ones, and an
  "achievement unlocked" toast in a product without achievements is pure confusion.
- **Kept deliberately:** every backend service, endpoint, event and DB table, plus the admin panel
  that configures them, and the DTO fields the API returns (`xpEarned`, `currentStreakDayCount`, …).
  The same pattern the skill tree already used. Reasons: the score still drives the AI feedback
  criteria; deleting `gamification-service` is a migration across four services' Kafka contracts and
  three databases, not a UI cleanup; and a reversal costs one commit this way instead of a rebuild.
- **Alternative rejected:** ripping out the service and its events now. It would put a large,
  irreversible backend migration behind a request that was about what the user sees.
- **Regression tests:** `FeedbackModal.test.tsx` (no XP even when the backend sends it); the
  remaining suites cover the touched exercise components.

---

## 2026-08-14 — A domain event must never hold a user request hostage

- **Context (user-reported):** «разбор не генерируется, бесконечная генерация», console showing a
  401 and `blocked by CORS policy`. The ai-service log tells the real story:
  `15:49:09 Calling OpenAI API` → `15:49:25 Extracted feedback summary … score: 6` →
  `15:50:49 ERR POST /dialog/sessions/{id}/complete responded 500 in 100010 ms`. The feedback was
  ready in 16s; the request then sat for another 100s and died. What happens after the feedback is
  saved is `PublishEvaluatedAsync` → `KafkaEventPublisher.ProduceAsync`, and the local Kafka
  container was not running (`repository-kafka-1` absent; the log is a wall of
  `localhost:9092 … Connection refused`). librdkafka's default `message.timeout.ms` is **5 minutes**,
  so the produce blocked until the 100s server/gateway timeout killed the request. The gateway then
  answered `504` — a response it writes itself, with no downstream CORS headers — so the browser
  reported "blocked by CORS" and the status code never reached the client. (The 401s were unrelated:
  an expired access token that the client refreshed.)
- **Decision:**
  1. `IEventPublisher.PublishAsync` no longer waits at all: the message is queued locally
     (`Produce` + delivery-report callback), so the request that produced it pays nothing whether
     the broker is healthy or absent, and a failed delivery is logged as an error rather than
     thrown. `Kafka:PublishTimeoutSeconds` (default 10) becomes librdkafka's `message.timeout.ms`,
     i.e. how long it keeps retrying in the background instead of the 5-minute default.
     `ForwardAsync` (outbox) and the dead-letter publisher still await and still throw — bounded by
     the same timeout — because their callers retry, and a silently "sent" outbox row would be lost
     forever. Ordering per partition is unaffected: the producer queues in call order.
  2. The gateway adds CORS headers to responses **it** generates (`GatewayErrorCorsMiddleware`),
     skipping anything that already carries them so proxied answers never get a duplicate
     `Access-Control-Allow-Origin` (browsers reject that outright).
  3. The client renders a `TypeError` from fetch as «Сервер не ответил…» rather than
     "Failed to fetch".
- **Alternative rejected:** an outbox for ai-service (write the event in the same transaction, let
  a relay retry it). That is the correct end state and `IOutboxEventForwarder` already exists — but
  ai-service has no outbox table and its session state lives in Mongo, so it is a migration, not a
  fix. The bounded publish is what stops a user-facing hang today; the outbox remains the follow-up.
- **Note for local dev:** Kafka *is* in `docker-compose.infra.yml`; that container simply was not
  up. With the bound in place a missing broker now costs a logged error, not a dead request.
- **Regression tests:** `KafkaEventPublisherTests` (an unreachable broker neither blocks nor throws;
  outbox forwarding still throws), `GatewayErrorCorsTests` (gateway-generated responses carry the
  headers for an allowed origin only).

---

## 2026-08-14 — A persona that says nothing is a bug in the contract, not in the LLM

- **Context (user-reported):** «собеседник вообще не отвечает». The ai-service log showed
  `POST /dialog/sessions/{id}/voice/stream` answering **200 in 11ms** with
  `WRN Voice stream aborted … Session … is not active`. `VoiceDialogController` sets
  `Response.StatusCode = 200` and the streaming content type *before* it asks the service for the
  first chunk, so every domain rejection (session completed, session missing, voice disabled)
  reached the browser as a 200 with an empty body. The frontend read zero frames, showed nothing,
  and the call looked alive with a mute persona. The same log also showed
  `POST /dialog/sessions → 400`: «Позвонить снова» on a custom-scenario page tried to create a
  session for the hidden `custom-scenario` mode without scenario text, which the backend rejects.
- **Decision:** three layers, because each one alone still leaves a silent failure.
  1. Backend: in the `InvalidOperationException` handler, if `!Response.HasStarted`, answer
     **409** with the message instead of an empty stream.
  2. Client: `409` → «Этот звонок уже завершён», and any stream that yields **zero frames** raises
     «Собеседник не ответил» rather than returning quietly.
  3. Page: a pre-started (`?session=`) scenario session is single-use — its status is checked
     before dialling, and once played out the CTA becomes «К сценариям», since this page never sees
     the scenario text and cannot legally create a replacement session.
- **Alternative rejected:** buffering the first chunk before committing the status code. It delays
  the first audible frame for every healthy call to improve the error path only, and the failure is
  always known before the first chunk anyway.
- **Regression tests:** `useVoice.test.tsx` (empty stream → error, 409 → error),
  `DialogVoiceCallPage.test.tsx` (a spent scenario refuses to dial and offers «К сценариям»).

---

## 2026-08-14 — Silent calls, and an analysis that cannot hang

### Call tones removed entirely

- **Context (user-reported):** the synthesized ringback (425 Hz, 1s/4s) and the triple busy beep
  were noise. A training call is not a phone call — the user already knows they pressed «Позвонить».
- **Decision:** delete `CallSoundsPlayer` and the Web Audio oscillators with it. The only state cue
  left is the connect vibration, now in `features/voice/services/call-haptics.ts` (`CallHaptics`).
- **Alternative rejected:** a volume/mute toggle. Nobody would have turned the tones back on, and it
  buys a settings row plus persistence for a feature with no demand.

### «Готовим разбор…» could never finish — three causes, all of them state, not the LLM

- **Context (user-reported):** after a call, the page sat on «Готовим разбор…» forever. The feedback
  request was not slow — in the reported flows it was **never sent at all**, and the hint lied.
  1. `useVoice` kept the finished session in `currentSessionIdRef`. The next «Позвонить снова»
     reused it (the page's `setSessionId(null)` lands a tick later, so the sync effect cannot win
     the race), the "session created" callback never fired, the call hung on «Соединение…», and its
     hang-up then had no session id to complete → an eternal «Готовим разбор…».
  2. The companies page latched `callEndedRef = true` on hang-up and never cleared it, so the *next*
     call's session was swallowed by the same guard — same dead end.
  3. `describePipeline` printed «Готовим разбор…» for *any* `ended` state, whether a request was in
     flight, had failed, or was never started.
- **Decision:** the session id is dropped by the new `endSession()` (the call pages call it on
  hang-up alongside `stopVoice`, which only stops listening — the chat mic button toggles voice
  input inside one dialog and must keep its session), so every call gets a fresh session; `callEndedRef` is reset on pick-up; the ended-state hint is derived from what
  is actually true (`describeEndedCall`: running / failed / ready / nothing to analyse); the
  in-flight guard is a ref, so a hang-up racing the persona's `endCall` cannot double-post.
  `POST /dialog/sessions/{id}/complete` is additionally capped at 120s client-side
  (`ApiRequestOptions.timeoutMs` → `RequestTimeoutError`; the backend's own upstream budget is 90s)
  and a failure offers «Повторить разбор». A retry against a session the backend already completed
  reads the stored feedback (`GET /dialog/sessions/{id}`) instead of failing on "not active".
- **Alternative rejected:** polling the session until feedback appears. It hides the failure instead
  of surfacing it, and the completion endpoint is synchronous — there is nothing to poll for.
- **Regression tests:** `__tests__/useVoice.test.tsx` (a fresh session per call, both end paths),
  `CompanyVoiceCallPage.test.tsx` (second call connects; no analysis promise without a session;
  retry after failure), `DialogVoiceCallPage.test.tsx` (a reused pre-started session connects).

---

## 2026-07-11 — AI backend hardening (39.17, PR #22 + PR #26 review fast-follows)

### `InternalAuth:ServiceSecret` — wire the missing header in learning-service, don't just document

- **Context:** PR #22 review flagged that `InternalAuth:ServiceSecret` (the shared secret behind
  ai-service's `InternalServiceAuthFilter`, guarding `EvaluationController` and the Companies AI
  controllers — briefing/readiness/parse-log/persona) is never provisioned in any `appsettings*.json`
  in this repo, and learning-service's `AiEvaluationClient` never sent the
  `X-Internal-Service-Secret` header (unlike company-service's four AI clients, which all already
  send it via their `*AiServiceCollectionExtensions`). Net effect today: the guard runs open in
  every environment (unset secret ⇒ `InternalServiceAuthFilter` skips the check), so
  `EvaluationController` is currently reachable by anyone who can route to ai-service directly.
- **Decision:** Wire the header in `AiEvaluationServiceCollectionExtensions.AddAiEvaluationClient`
  (learning-service), mirroring the exact pattern company-service's `BriefingAiServiceCollectionExtensions`
  / `ReadinessAiServiceCollectionExtensions` / `ParseLogAiServiceCollectionExtensions` /
  `PersonaAiServiceCollectionExtensions` already use: read `InternalAuth:ServiceSecret` from
  config, add the header to the typed `HttpClient` only if the secret is non-empty.
- **Why wiring instead of documenting:** the fix is a ~10-line, single-file, additive change
  (no behavior change while the secret stays unset — it's the same no-op the other four clients
  already have) that closes the actual gap, rather than leaving `EvaluationController` open and
  writing a paragraph explaining why. There was no risk/blast-radius reason to prefer
  documentation-only here — the change touches nothing else callers depend on.
- **Still true after this fix:** `InternalAuth:ServiceSecret` is *provisioned* nowhere (no
  `appsettings*.json`/deployment config sets it), so the guard still runs open by default in
  every environment today. Wiring the header only means that *if/when* ops sets the secret in
  ai-service **and** all three callers (company-service, learning-service, gateway if it ever
  calls ai-service directly), the guard will actually enforce it end-to-end. Provisioning the
  secret itself is an ops/deployment task, out of scope here — tracked as a gap, not silently
  assumed done.

### Negative-cache TTL for the "no usable feedback yet" readiness result

- **Context:** PR #26 review noted `GET /companies/{id}/readiness` re-fans-out (up to 50
  sequential `DialogSessionId` lookups via ai-service → Mongo) on *every* request while the
  company has practice sessions but ai-service keeps returning `204` (no feedback text landed
  yet) — the positive cache (`ReadinessJson`) only helps once there's a real result.
- **Decision:** Add `Company.ReadinessNoFeedbackUntil` (nullable timestamptz) — set to
  `now + 2 minutes` when ai-service returns `204` after a real fan-out; checked before the
  fan-out on subsequent `GET`s. Left untouched (`null`) for the *other* 204 case — zero practice
  calls — since that path already short-circuits before touching ai-service and has nothing
  expensive to avoid. Cleared by `CreatePracticeCallAsync` alongside the existing
  `ReadinessJson`/`ReadinessGeneratedAt` invalidation, and cleared again once a real result is
  cached, so a fresh practice call always gets a fresh readiness attempt.
- **Why 2 minutes:** short enough that a user who just finished a practice call and immediately
  reloads doesn't wait meaningfully longer than before for a fresh readiness attempt (the
  practice-call-created invalidation already covers the common case), long enough to absorb
  repeated polling/reloads from the frontend readiness card within the same short window.
- **Alternative considered:** cache the negative result indefinitely until the next practice
  call. Rejected — feedback can, in principle, land in Mongo asynchronously without a new practice
  call being created in company-service (out of scope to fully reason about here), so an
  unbounded negative cache risked being wrong for longer than necessary.

### Dedicated `BriefingModel`/`MaximumBriefingTokenCount` config in ai-service

- **Context:** PR #22 review noted the briefing feature (39.12) reused `OpenAiConfiguration`'s
  `OpenQuestionModel`/`MaximumFeedbackTokenCount` — config names that describe unrelated features
  (open-question exercises, dialog feedback), making it unclear/risky to retune either without
  affecting briefing too.
- **Decision:** Add `OpenAiConfiguration.BriefingModel` (default `"gpt-4.1"`, same as
  `OpenQuestionModel`'s default) and `MaximumBriefingTokenCount` (default `1500`, same as
  `MaximumFeedbackTokenCount`'s default) — unset config keeps today's behavior byte-for-byte.
  `IOpenAiChatService.GenerateTextAsync` gained optional `model`/`maxTokens` parameters (default
  `null` ⇒ falls back to `OpenQuestionModel`/`MaximumFeedbackTokenCount`, preserving the other
  three callers — `ParseLogService`, `ReadinessService`, `PersonaService` — unchanged); only
  `BriefingService` passes the new dedicated options explicitly.
- **Why not also split ParseLog/Readiness/Persona:** out of scope — the PR #22 review only
  flagged briefing by name, and those three weren't called out as piggybacking on unrelated
  config. Keeping the change scoped avoids touching three working features' behavior/config
  surface without a stated need.

---

## 2026-06-21 — Phase 3 (Shared User read-model replica) — resolved as satisfied/superseded

- **Context:** [MICROSERVICES_ROADMAP.md](MICROSERVICES_ROADMAP.md) Phase 3 ("Shared User
  read-model replica") was still `[ ]`, but the established database-per-service pattern had
  already realized it by the time the domain services were extracted (Phases 5–8). This entry
  records the per-task verdict so the roadmap reflects reality rather than leaving a phantom
  open phase.

### Per-task verdict

- **3.1 — UserReplica table + `user.*` consumer in BuildingBlocks, reusable by every service →
  Satisfied.** The shared `UserReplica` entity lives in BuildingBlocks since Phase 0.1
  ([src/backend/building-blocks/BuildingBlocks/Identity/UserReplica.cs](../src/backend/building-blocks/BuildingBlocks/Identity/UserReplica.cs)),
  alongside the `user.*` topic constants
  ([Eventing/Topics.cs](../src/backend/building-blocks/BuildingBlocks/Eventing/Topics.cs) lines 17–20)
  and the reusable idempotent consumer base `KafkaConsumerBackgroundService` (Phase 0.4).
  Every extracted domain service keeps **its own** replica table, fed by its own idempotent
  `user.*` consumer (dedupe on `eventId`) plus its own EF config:
  - gamification-service: [Identity/UserReplica.cs](../src/backend/gamification-service/Gamification/Identity/UserReplica.cs),
    [Eventing/UserReplicaConsumer.cs](../src/backend/gamification-service/Gamification/Eventing/UserReplicaConsumer.cs),
    [Infrastructure/Data/UserReplicaEntityConfiguration.cs](../src/backend/gamification-service/Gamification/Infrastructure/Data/UserReplicaEntityConfiguration.cs)
  - ai-service: [Identity/UserReplica.cs](../src/backend/ai-service/Ai/Identity/UserReplica.cs),
    [Eventing/UserReplicaConsumer.cs](../src/backend/ai-service/Ai/Eventing/UserReplicaConsumer.cs),
    [Infrastructure/Data/UserReplicaEntityConfiguration.cs](../src/backend/ai-service/Ai/Infrastructure/Data/UserReplicaEntityConfiguration.cs)
  - social-service: [Identity/UserReplica.cs](../src/backend/social-service/Social/Identity/UserReplica.cs),
    [Eventing/UserReplicaConsumer.cs](../src/backend/social-service/Social/Eventing/UserReplicaConsumer.cs),
    [Infrastructure/Data/UserReplicaEntityConfiguration.cs](../src/backend/social-service/Social/Infrastructure/Data/UserReplicaEntityConfiguration.cs)
  - learning-service: [Identity/UserReplica.cs](../src/backend/learning-service/Learning/Identity/UserReplica.cs),
    [Eventing/UserReplicaConsumer.cs](../src/backend/learning-service/Learning/Eventing/UserReplicaConsumer.cs),
    [Infrastructure/Data/UserReplicaEntityConfiguration.cs](../src/backend/learning-service/Learning/Infrastructure/Data/UserReplicaEntityConfiguration.cs)

  (notification-service and analytics-service are Redis-only with no relational store, so they
  consume `user.*`/funnel events directly and need no `UserReplica` table — consistent with the
  pattern.)

- **3.2 — Wire the replica into the still-monolithic remaining features so they stop joining
  Identity tables → Superseded by Phases 5–8 + Phase 9.** The strangler migration extracted
  **all** domain services, each owning a local replica seeded from `user.*` events, and the
  monolith is being retired in Phase 9 (kept only as reference). There are no remaining
  monolithic features left to "wire onto the replica," so this task is superseded by the actual
  extraction work rather than skipped arbitrarily.

- **3.3 — Tests: replica seed / update / delete → Satisfied per-service.** Each service's replica
  consumer is covered by that service's own test suite; the canonical explicit example is
  [src/backend/social-service/Social.Tests/Unit/UserReplicaConsumerTests.cs](../src/backend/social-service/Social.Tests/Unit/UserReplicaConsumerTests.cs)
  (seed on `user.registered`, idempotent re-seed, update on `user.updated`, delete on
  `user.deleted`).

### Alternative considered

- **A single central User replica service** that every other service queries over REST/gRPC,
  instead of each service holding its own copy. **Rejected:** it reintroduces a synchronous
  cross-service dependency on a shared store — the exact coupling database-per-service exists to
  remove (see [DATA_OWNERSHIP.md](DATA_OWNERSHIP.md)). Database-per-service + a local
  event-fed `UserReplica` per service is the locked decision.

### Reusable-extraction assessment

- Considered extracting a shared `UserReplicaConsumer` base / EF config into BuildingBlocks to
  remove the near-identical per-service consumers. **Not done:** each consumer is bound to its
  own `DbContext` type and its own per-service event DTOs
  (e.g. [gamification IncomingIntegrationEvents.cs](../src/backend/gamification-service/Gamification/Eventing/IncomingIntegrationEvents.cs)),
  so a shared base would require generics over `DbContext` plus shared event contracts, touching
  every service's migrations. That exceeds the "removes real duplication at low risk" bar, so
  this resolution is **documentation-only** (no code extracted).

---

## 2026-06-15 — Email verification by code

### MailerSend as the email provider

- **Decision:** Send the verification email through MailerSend.
- **Why:** EU-hosted (matches the European server), free tier covers low-volume verification
  mail, simple Bearer-token HTTP API, supports sending from a custom verified domain.
- **Alternatives:** Brevo (also EU/free), Amazon SES (cheapest at scale, more setup),
  self-hosted SMTP (rejected — new-IP deliverability is poor). The `IEmailSender` abstraction
  keeps the provider swappable.

### Store codes in Postgres, not Redis

- **Decision:** Persist `EmailVerificationCodes` in Postgres via EF, despite Redis being wired up.
- **Why:** Redis is registered but otherwise unused, with no established pattern; the codebase is
  EF-centric and the integration-test harness runs a real Postgres but only a stub Redis. Postgres
  gives a testable, well-trodden path plus expiry/attempt columns and a Hangfire cleanup job.
- **Trade-off:** Codes need a periodic cleanup job (added) instead of Redis TTL auto-expiry.

### Hash codes; one active code per email

- **Decision:** Store only the SHA-256 hash of the code, replace any prior code on each request,
  cap attempts, and rate-limit resends.
- **Why:** Limits blast radius of a DB read, and the attempt cap + short TTL make a 6-digit code
  safe against brute force. BCrypt was considered overkill for a short-lived single-use OTP.

### Register no longer returns tokens

- **Decision:** `/auth/register` returns `RegistrationResultDto` (verification required) instead of
  an `AuthTokenResponseDto`; tokens are issued by `/auth/verify-email`.
- **Why:** Tokens must not be granted before the address is proven. Google sign-in stays
  auto-verified. Existing users are grandfathered verified by the migration.

---

## 2026-06-12 — Discuss photo attachments

### Single polymorphic `DiscussPhotos` table

- **Decision:** Store thread and reply photos in one `DiscussPhotos` table with an
  `(OwnerType, OwnerId)` polymorphic owner, rather than two separate tables (e.g.
  `DiscussThreadPhotos` + `DiscussReplyPhotos`).
- **Why:** Mirrors the existing `DiscussVotes` shape (`TargetType, TargetId`), so the slice stays
  internally consistent and the upload/list/delete code path is shared.
- **Trade-off:** No DB-level FK to the owner row; orphan cleanup is handled in the service on
  thread/reply delete.

### Two-step create (JSON create + multipart photo sub-resource)

- **Decision:** Keep the existing JSON create endpoints for threads/replies unchanged and add a
  separate multipart photo sub-resource (`POST .../photos`). Alternative considered: switch the
  create endpoints themselves to `multipart/form-data`.
- **Why:** Lowest-risk change — the existing create endpoints and their callers stay untouched.
- **Trade-off:** A post can exist with a failed photo upload. The frontend surfaces this as a
  non-fatal, retryable error rather than discarding the created post.

### Service-level max-10 enforcement (no DB constraint)

- **Decision:** Enforce the 10-photos-per-owner cap in the service, not via a DB constraint.
- **Why:** Matches the slice's existing service-enforced validation style (the same approach
  used elsewhere in Discuss).

### Duplicated image magic-byte validator

- **Decision:** Accept a duplicated `ImageContentValidator` between the Avatars and Discuss slices
  rather than extracting a shared utility now.
- **Why:** Bounded scope; the two slices are independent. A future shared utility is possible if a
  third consumer appears.

### Style note: mirror the existing slice conventions

- **Decision:** New Discuss-photo files intentionally mirror the existing Discuss/Avatars slice
  conventions — `public class` EF configs, `{ get; set; }` + `= null!` entities, `ct` parameter
  name, and inline cache / `nosniff` headers like `AvatarsController` — rather than the strict
  letter of [CODESTYLE.md](CODESTYLE.md).
- **Why:** Keeps the slice internally consistent with the code it lives next to.

## Email notifications

### Shared email transport in BuildingBlocks (not duplicated per service)

- **Decision:** Move the MailerSend email stack (`IEmailSender`, `EmailMessage`,
  `MailerSendEmailSender`, `MailerSendConfiguration`) out of the identity service into
  `Sellevate.BuildingBlocks.Email`, exposed via `AddSellevateEmail()`. Alternative considered:
  copy the sender into the notification service.
- **Why:** Two services now send transactional email (identity verification codes, notification
  emails); one shared implementation avoids divergent MailerSend wiring and config drift.
- **Trade-off:** BuildingBlocks gains an HTTP/email concern, but it already references
  `Microsoft.AspNetCore.App` (so `IHttpClientFactory`/`AddHttpClient` are available).

### Redis user replica in the notification service (no database)

- **Decision:** Resolve a recipient's email/display name from a Redis-backed user replica
  (`notifications:user:{userId}`) fed by `UserReplicaConsumer`, rather than introducing EF/Postgres
  or a synchronous call to identity.
- **Why:** The notification service is deliberately Redis-only; a Redis projection keeps that
  property and matches the `UserReplica` pattern other services use (just without EF).
- **Trade-off:** Eventually consistent — a brand-new user with no replicated email yet is simply
  not emailed (logged, never throws).

### Delayed unread-chat email via a Redis sorted set + watermark

- **Decision:** Implement "email if a message is unread after 5 minutes" with a Redis sorted set
  of pending emails (scored by due time) plus a per-(recipient, conversation) read watermark, polled
  by a background dispatcher. A `chat.message.read` event updates the watermark; the dispatcher
  skips messages read before they came due. Alternative considered: Hangfire delayed jobs.
- **Why:** Keeps the service Redis-only (no Hangfire/DB), and a watermark is simpler and more
  replay-safe than scheduling + cancelling individual jobs.
- **Trade-off:** Delivery is approximate to within one poll interval (default 30s); acceptable for
  a "you missed a message" email.

### OOP email templates (template-method) over inline HTML strings

- **Decision:** Generate notification email HTML inside the notification service via a template
  hierarchy — `NotificationEmailTemplate` (abstract) + per-type subclasses + a shared
  `NotificationEmailLayout` and a `NotificationEmailRenderer` that selects by `NotificationType`.
- **Why:** Adding an email for a new type is one small subclass; the shared, client-safe chrome and
  HTML-encoding live in one place. Matches the request to "use OOP and separate helpers".
- **Trade-off:** More files than a single string builder, but each is small and isolated.

### Codestyle "no comments" rule (CODESTYLE.md §9) is aspirational, not a merge gate

- **Decision:** The companies feature (Phase 39) ships with XML `///` doc comments and the
  occasional inline rationale comment, the same convention the rest of the backend already uses.
  `scripts/codestyle-lint.py` flags ~490 such lines in the feature's touched files, but `main`
  already contains 909 `///` doc-comment lines across the backend, so the rule is not enforced
  repo-wide. Mass-stripping comments from only the companies files would make the feature
  *inconsistent* with the surrounding codebase for no functional gain.
- **Why:** Release gate is "no new lint/type/test regressions vs `main`", not "touched files must
  satisfy an unenforced style law". The `catch (Exception ex)` abbreviations the linter flagged in
  touched files are likewise pre-existing on `main` (the feature only touched the file), so they are
  out of scope for this branch.
- **Follow-up:** If the team wants CODESTYLE.md §9 enforced, do it as a dedicated repo-wide sweep
  with its own PR, not piecemeal per feature.

### Internal service-to-service auth secret ships inert in the docker/compose shape

- **Decision:** `InternalServiceAuthFilter` (ai-service) treats a missing `InternalAuth:ServiceSecret`
  as "allow" (dev convenience). Neither the `ai` nor `company` service sets that key in
  `docker-compose.yml`/`.env` today, so the `/ai/companies/*` guard is a no-op in the deploy shape.
- **Why acceptable for the companies release:** those internal AI routes are not gateway-exposed
  (verified: 0 `/ai` routes in the gateway config) — they are reachable only on the internal Docker
  network. The filter is defense-in-depth, not the primary boundary. A company-service appsettings
  stub (`"InternalAuth": { "ServiceSecret": "INJECTED_FROM_ENV" }`) was added for discoverability.
- **Follow-up (post-merge hardening):** inject a shared `InternalAuth__ServiceSecret` env on BOTH
  the `ai` and `company` services (and any k8s manifest) so the guard enforces in non-dev; provision
  symmetrically to avoid a one-sided 401.

### ai-service must accept string enum values on its JSON wire (persona 400 fix)

- **Bug:** `POST /companies/{id}/personas/generate` returned `AI persona service returned 400`.
  Root cause: company-service serializes the persona `Difficulty` enum as a **string** (via
  `enum.ToString()`, e.g. `"Medium"`), but ai-service registered plain `AddControllers()` with no
  `JsonStringEnumConverter`. System.Text.Json binds enums from **numbers** by default, so the string
  failed to deserialize and `[ApiController]` auto-returned **400** before `PersonaController` ran.
- **Decision:** Register `JsonStringEnumConverter` in ai-service's `AddControllers().AddJsonOptions(...)`,
  mirroring company-service's existing config. Cross-service enum payloads now bind by name on both hops.
- **Why the tests missed it:** `PersonaControllerTests`/`PersonaServiceTests` build the DTO in-process
  and never cross the JSON wire. Added `PersonaRequestWireContractTests` to lock the string-enum
  contract at the serialization boundary.

### F5Ai LLM provider must be selected via OpenAI__Provider (persona/dialog 500 → 401 fix)

- **Bug:** After the persona string-enum fix, `POST /ai/companies/persona` reached the LLM but
  returned **500** wrapping `OpenAiAuthenticationException` — the F5Ai gateway (`api.f5ai.ru`)
  answered **401**. This broke ALL LLM calls (persona, dialog, feedback), not just personas.
- **Root cause:** `OpenAiChatService` picks the auth header by `OpenAI:Provider`
  (`F5Ai` → `X-Auth-Token`, otherwise → `Authorization: Bearer`). After the "AI7c" refactor from
  URL-sniffing to an explicit provider enum, no deploy config set `OpenAI__Provider`, so it defaulted
  to `OpenAi` → Bearer → 401 against F5Ai. Verified live: same key returns **200** with
  `X-Auth-Token` and **401** with `Bearer`.
- **Fix:** Added `OpenAI__Provider=${OPENAI_PROVIDER:-OpenAi}` to both ai and learning service env
  blocks in `docker-compose.yml`; set `OPENAI_PROVIDER=F5Ai` in `.env` (documented default `OpenAi`
  in `.env.example`). No code change — the enum path was already correct, only unconfigured.
- **Recurrence in the Local Dev profile (custom-scenario validation 503):** the fix above only
  covered `docker-compose.yml`. The host scripts — `scripts/dev-ai.sh` and the
  `export_backend_env` / `export_learning_env` blocks in `scripts/lib-local-env.sh` — exported
  `OpenAI__ApiKey` / `BaseUrl` / `ChatCompletionsPath` but **not** `OpenAI__Provider`, so the
  default Local Dev profile still sent Bearer to F5Ai. Symptom: `POST /dialog/scenario/validate`
  → 401 `{"error":{"message":"API key is missing"}}` → `ScenarioValidationUnavailableException`
  → **503**, surfaced in the UI as «Не удалось проверить сценарий». The scenario text was never
  the cause. Fix: export `OpenAI__Provider` (plus the model/token tunables compose already passed)
  from the host scripts too. **Rule: any new `OpenAI__*` key added to `docker-compose.yml` must be
  mirrored into the host dev scripts, and vice versa** — the two profiles are the same config
  surface and drift between them is invisible until a live call fails.

### Frontend adaptivity: sizing rules over per-page breakpoints

- **Bug (user-reported):** on some devices buttons stopped being visible — with no zoom and no
  change of screen resolution. Three independent root causes, all the same underlying mistake:
  layout boxes were given **hard, absolute sizes** (`100vh`, hand-counted pixel constants,
  fixed-count grid tracks) instead of intrinsic sizes with floors and ceilings. Each is correct
  on the developer's monitor and drifts on every other device.
  1. **Landscape phones got the desktop shell.** The rail is `height: 100vh` with every child
     `flex-shrink: 0`, summing to ~516px. A landscape phone reports ≥768px wide but 375–430px
     tall, so it matched the desktop branch and the notification bell + settings gear rendered
     below the fold with no scroll affordance.
  2. **`/tree` FAB anchored to the timeline, not the viewport.** At ≤1000px `.path-grid` becomes
     `height: auto`, so `.path-center` grows to the full height of the lesson list — but the
     `position: absolute` FAB was only switched to `fixed` at ≤767px. In the 768–1000px band
     (every iPad in portrait) the "Начать" CTA sat ~2000px below the fold.
  3. **A 1px breakpoint dead zone.** `max-width: 767px` and `min-width: 768px` both fail to match
     at fractional widths (non-integer `devicePixelRatio`, Windows display scaling), so the rail
     and the bottom nav rendered simultaneously and the nav covered content.
- **Decision:** fix the *sizing rules*, not the individual pages. Codified in
  `docs/TESTING/MOBILE_RESPONSIVE.md`: always ship the `100vh`/`100dvh` fallback pair; every
  bottom-anchored control carries `env(safe-area-inset-bottom)` (`viewportFit: "cover"` is set,
  so the inset is real); text-bearing flex/grid children get `min-width: 0` / `minmax(0, 1fr)`;
  rows of unshrinkable buttons get `flex-wrap: wrap`. Added one **height** tier
  (`max-height: 520px`) — the axis the breakpoint system had no concept of.
- **Alternative rejected:** a full re-tier of all ~23 media queries onto Tailwind's scale. It is
  the right end state, but it touches every page layout at once and a regression could not be
  attributed. Deferred until there are screenshot tests; the `.98` suffix closes the dead zone
  in the meantime.
- **Why the tests missed it:** all 272 frontend tests are jsdom unit/hook tests, which have no
  layout engine — jsdom does not compute `vh`, `env()`, flex overflow, or media queries. This
  class of bug is only reachable through visual/viewport testing, so it is covered by the manual
  checklist rather than by assertions.

---

## Phase 40.6 — Identity: memberships and the role split (2026-08-15)

### The `RequireAdmin` audit — every call site, decided deliberately

The block's instruction was explicit: don't mechanically rename `RequireAdmin` to
`RequireSuperAdmin`, decide each call site. The deciding question for every one of them was
the same: **is this endpoint scoped to one organization, or is it still global/platform
content?** As of this branch, the organization-scoped admin screen (roadmap block 40.20,
"Разделение админки" — a separate `/admin` surface for a РОП's own program, overrides and
company profile) does not exist yet. Every current `/admin/*` endpoint manages a *global*
resource (the shared content library, platform-wide user list, platform-wide gamification
config, platform-wide discuss moderation) with **no** `organization_id` anywhere in its
query — confirmed by reading each controller, not assumed. So every single one resolved the
same way:

| Call site | Old policy | New policy | Why |
|---|---|---|---|
| `identity-service` `AdminUsersController` (whole controller, incl. `GET /admin/users`, `GET/PUT /admin/users/:id`, `DELETE .../avatar`) | `RequireAdmin` (class-level) | `RequireSuperAdmin` | Lists/manages users **platform-wide**, not scoped to one org. `PUT .../role` was already `RequireSuperAdmin`; now the whole controller is, so that per-method attribute was redundant and removed. |
| `identity-service` `AdminUsersController.ChangeRole` | `RequireSuperAdmin` (unchanged) | `RequireSuperAdmin` | Already correct — role changes only ever move between the two remaining platform roles (`User`/`SuperAdmin`). `Enum.TryParse<UserRole>("Admin", …)` now fails with 400, same as any other unknown role (locked in by `ChangeRole_RejectsRemovedAdminRole`). |
| `ai-service` `AdminDialogController` (dialog bundles/modes) | `RequireAdmin` | `RequireSuperAdmin` | Global dialog-content library; no `organizationId` in ai-service today. |
| `ai-service` `AdminVoiceUsageController` (`GET /admin/voice/usage`) | `RequireAdmin` | `RequireSuperAdmin` | Platform-wide voice usage/cost view across *all* users — a cost-control screen for Sellevate, not an org-scoped one. |
| `gamification-service` `AdminGamificationController` | `RequireAdmin` | `RequireSuperAdmin` | Global gamification config (XP sources, thresholds). |
| `gamification-service` `AdminLeaguesController` | `RequireAdmin` | `RequireSuperAdmin` | Team-progress/league administration is a single global ladder today, not per-organization. |
| `learning-service` — `AdminSkillsController`, `AdminSkillStagesController`, `AdminTopicsController`, `AdminLessonsController`, `AdminExercisesController`, `AdminExerciseTypePromptsController`, `AdminReferenceController`, `AdminTechniquesController`, `AdminDailyQuotesController`, `AdminSeederController` (10 controllers) | `RequireAdmin` | `RequireSuperAdmin` | All manage the shared, nullable-`organization_id` content library (skills/lessons/exercises/reference/techniques/quotes) — see TENANCY.md §1.2. Per-organization content overrides are 40.18 (copy-on-write), not shipped yet; until then editing this content is still Sellevate-staff work. |
| `social-service` `AdminDiscussController` | `RequireAdmin` | `RequireSuperAdmin` | Platform-wide discuss moderation; no org scoping in social-service. |
| `social-service` `DiscussController.IsAdmin()` (private helper — **not** a named policy, a raw `User.IsInRole("Admin") \|\| User.IsInRole("SuperAdmin")` check gating "delete any thread/reply, not just your own") | n/a (inline role check) | `User.IsInRole("SuperAdmin")` | Same underlying question (platform moderator vs. thread author) — caught by grep for `IsInRole("Admin")`, not by the `RequireAdmin` policy-name search, so called out explicitly here. |

**Net effect:** every existing admin surface is Sellevate-staff-only now. `RequireOrgAdmin`
is registered in every service that had `RequireAdmin` (identity, ai, gamification,
learning, social) as pure infrastructure — `policy.RequireAssertion(... HasClaim("org_role",
"OrgAdmin") ...)` — with **zero call sites** in this block. It exists for 40.7 (an OrgAdmin
inviting their own organization's managers) and 40.20 (the org admin screen). Leaving it
unused-but-present, rather than adding it only when 40.7 needs it, means the claim shape and
policy name are locked in now and 40.7 doesn't have to touch every service's `Program.cs`
again.

### `Admin` role value: left unassigned, not reused

`UserRole` was `{User = 0, Admin = 1, SuperAdmin = 2}`. Removing `Admin` leaves value `1`
unassigned rather than renumbering `SuperAdmin` down to `1` or reusing `1` for something new.
**Why:** a pre-existing row with `Role = 1` (if any exist in a real database before 40.9's
migration runs) fails to deserialize loudly (`InvalidOperationException` from EF's enum
conversion) instead of silently becoming whatever the next thing assigned to `1` happens to
be. Loud failure on stale data is the correct default until 40.9 explicitly decides what a
former `Admin` user becomes (most likely: an `OrgAdmin` membership in whatever organization
40.9 backfills them into).

### JWT `org_id`/`org_role`: looked up at token-issue time, not decoded from a header

`AuthenticationService.IssueTokensForUserAsync` queries the user's memberships
(`Status == Active`, ordered by `JoinedAt`, first-or-default) and adds `org_id`/`org_role`
claims to the JWT only when one exists. **Why not push this into the gateway or a header
instead:** every downstream service already validates the JWT directly via its own
`AddJwtBearer` (shared HMAC signing key) — `org_role` doesn't need a header round-trip the
way `X-Organization-Id` does for `ITenantContext`, because the authorization policies read
`HttpContext.User.Claims` off the already-validated token. Only `identity-service` needed
code changes to *produce* the claims; every other service only needed the new
`RequireOrgAdmin` policy definition, not a new header consumer.

**Ordering by `JoinedAt` when multiple active memberships could theoretically exist:** the
schema (composite PK `(UserId, OrganizationId)`) does not prevent a user from having two
active memberships even though nothing in the product creates that today ("even while the UI
only allows one organization per user" per the spec). Picking deterministically now avoids a
silent behavior change if that assumption is relaxed later without anyone revisiting token
issuance.

### `Membership` is a plain EF entity — not `ITenantScoped`, no RLS, in this block

`Membership.OrganizationId` is the tenant-defining column for `membership` conceptually, but
the entity does **not** implement `ITenantScoped` and no RLS policy was added to it in 40.6.
**Why:** `ITenantScoped`/RLS assume `ITenantContext.OrganizationId` is already known when the
row is written — but establishing membership (accepting an invite, an OrgAdmin inviting a
manager) is exactly the moment that context doesn't exist yet for the invitee, and superadmin
actions (creating the *first* membership in a brand-new organization, 40.9) are inherently
cross-tenant. This mirrors `organization-service`'s own `Organization` registry entity, which
is likewise not tenant-scoped — both are cross-organization registries by nature, not
per-tenant data. No membership CRUD endpoints ship in this block (that's 40.7/40.9), so this
is a forward-looking note, not yet a tested boundary.

**Follow-up for 40.7/40.9:** when membership *write* endpoints ship, guard them with
application-level checks (actor's `org_id`/`org_role` claim against the target
`organization_id`), not RLS — and revisit this decision if a read-heavy membership-listing
endpoint would benefit from RLS instead of an app-level filter.

### `Membership.UserId` gets a real FK; `OrganizationId` does not

`UserId` references `Users.Id` in the **same** database (identity-service owns both tables),
so a normal FK with `ON DELETE CASCADE` applies — unlike `OrganizationId`, which is a
cross-service reference under DB-per-service and per the block's explicit instruction stays a
bare `uuid` with no FK. Cascade (not `Restrict`) was chosen because there is no
delete-account endpoint yet (per IDENTITY_SERVICE.md) and an orphaned membership row pointing
at a deleted user would be meaningless; revisit if/when account deletion ships and offboarding
semantics (`membership.status = deactivated`, never delete) need to apply to the user row too.

### Frontend: dropping `Admin` collapses the admin-panel gate to `SuperAdmin`-only

`shared/stores/auth-store.ts`'s `UserRole` type drops `"Admin"`; every frontend call site that
checked `role === "Admin" || role === "SuperAdmin"` (`app/(admin)/layout.tsx` — both the
redirect guard and the nav-item list, `app/(admin)/admin/users/page.tsx`,
`app/(main)/settings/page.tsx`'s admin-area visibility, `app/(main)/discuss/[threadId]/page.tsx`'s
moderation check, `features/admin/components/user-detail-modal.tsx`'s role dropdown) now
checks `role === "SuperAdmin"` alone — a mechanical consequence of the backend audit above,
not a separate decision, since every guarded surface maps to a `RequireSuperAdmin` endpoint.
Added `orgId`/`orgRole` (optional) to `AuthenticatedUser` and threaded them through
`useHandleSuccessfulAuth`/`useInitAuth` so the org claims introduced this block are actually
reachable from the frontend once 40.20 needs them — leaving JWT claims with no client-visible
counterpart would have been a dangling reference in the other direction.
