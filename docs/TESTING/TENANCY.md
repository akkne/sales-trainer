# Testing — Multi-tenancy isolation (Phase 40)

Isolation verification checklist for the tenancy primitives shipped so far (Stage A,
40.1–40.4). Nothing in Phase 40 is wired into a live service's routes or tables yet — this
is the mechanism, exercised directly against `BuildingBlocks`. Stage B/C (`organization-service`,
`membership`, per-service `organization_id` rollout) will extend this document with per-service
checks once there is a real tenant-scoped table to point at.

See [docs/TENANCY/TENANCY.md](../TENANCY/TENANCY.md) for the design this verifies, and
[docs/DECISIONS.md](../DECISIONS.md) (2026-08-15, "RLS infrastructure") for why the mechanism is
shaped the way it is.

---

## 40.1 — write guard (`TenantSaveChangesInterceptor`)

`BuildingBlocks.Tests/Tenancy/TenantSaveChangesInterceptorTests.cs` (EF InMemory provider, no
Postgres needed):
- Insert with no tenant context set → throws.
- Insert with tenant context set, entity's `OrganizationId` unset → stamped with the current
  organization.
- Insert carrying a *different* organization id than the current context → throws
  `CrossTenantWriteException`, and the exception message never contains the foreign id.
- An entity loaded via `IgnoreQueryFilters()` from another organization, then reassigned to the
  attacker's own organization and saved → still throws (`OriginalValues` comparison, not the
  in-memory value).
- System mode (`ITenantContext.IsSystem`) → the guard is a no-op.

## 40.2 — gateway header + request-scope middleware

`BuildingBlocks.Tests/Tenancy/TenantContextMiddlewareTests.cs`,
`BuildingBlocks.Tests/Tenancy/TenantContextTests.cs`:
- A route marked `[TenantScoped]` / `.RequireTenantScope()` with no `X-Organization-Id` header →
  `403`.
- A valid header populates `ITenantContext.OrganizationId`; `SetOrganization` / `EnterSystemMode`
  each fire at most once per DI scope.

`scripts/tenancy-boundary-lint.py` (CI: `tenancy-boundary`) — static check, not a test: fails the
build if `OrganizationId` appears in a request DTO, a `[FromQuery]`/`[FromRoute]` binding, or a
route template. Run manually: `python3 scripts/tenancy-boundary-lint.py src/backend`.

## 40.3 — event envelope + outbox

`BuildingBlocks.Tests/EventContractCatalogTests.cs`, `EventEnvelopeTests.cs`,
`KafkaConsumerBackgroundServiceTenancyTests.cs`:
- `EventEnvelope.OrganizationId` round-trips through JSON.
- A consumer with `RequiresOrganization = true` (the default) throws when the envelope carries no
  organization; a consumer that overrides it to `false` runs in system mode instead.

## 40.4 — Postgres RLS infrastructure

### Unit tests (no Postgres required)

`BuildingBlocks.Tests/Tenancy/TenantConnectionInterceptorTests.cs`:
- `BuildSetLocalCommandText` emits `SET LOCAL app.organization_id = '<guid>'` — never a bare `SET`.
- Returns `null` (nothing to run) when the tenant context has no organization, or is in system
  mode — system-mode connections are expected to use a separate `BYPASSRLS` role instead of the
  GUC, not an empty/zero setting.

`BuildingBlocks.Tests/Tenancy/TenantRlsMigrationBuilderExtensionsTests.cs`:
- `EnableTenantRls(table)` emits `ENABLE` + `FORCE ROW LEVEL SECURITY` and a policy with both
  `USING` and `WITH CHECK`.
- The comparison reads `NULLIF(current_setting('app.organization_id', true), '')::uuid` — the
  `NULLIF` matters, see below.
- `EnableTenantRlsForContent(table)` adds `organization_id IS NULL OR ...` so seeded global content
  stays visible to every tenant.

### Real-Postgres integration test

`BuildingBlocks.Tests/Tenancy/TenantRowLevelSecurityIntegrationTests.cs` — the layer no in-memory
provider or unit test can exercise: RLS enforced by the actual Postgres server against the actual
`sellevate_app`-shaped role.

**How it runs:** against the same local Docker Postgres `scripts/dev-infra.sh` already starts
(`localhost:5433` by default), inside its own throwaway database
(`tenancy_rls_integration_test`) and a test-only role (`sellevate_app_test`) — both dropped and
recreated at the start of every run and dropped again at the end. It never touches the
identity/learning/ai/company/gamification/social service databases.

```bash
# Bring up local infra if it is not already running (dev-infra also starts Kafka/Redis/etc. —
# only Postgres is required for this test):
scripts/dev-infra.sh

dotnet test src/backend/building-blocks/BuildingBlocks.Tests/Sellevate.BuildingBlocks.Tests.csproj \
  --filter "FullyQualifiedName~TenantRowLevelSecurityIntegrationTests"
```

Per `CLAUDE.md`, this must never hang the build waiting for infrastructure: `OneTimeSetUp` probes
`localhost:5433` with a 3-second timeout and every test in the fixture calls `Assert.Ignore(...)`
(reported as **Skipped**, not failed) when Postgres is not reachable. Override host/port/superuser
via `TENANCY_RLS_TEST_POSTGRES_HOST`, `LOCAL_POSTGRES_PORT`,
`TENANCY_RLS_TEST_POSTGRES_SUPERUSER(_PASSWORD)` if your local setup differs from
`scripts/lib-local-env.sh`'s defaults.

**What it proves, one test per assertion:**

| Test | Proves |
|------|--------|
| `Raw_sql_under_the_application_role_only_sees_the_current_organizations_rows` | `SET LOCAL app.organization_id = <A>` + raw `SELECT` under the app role returns only organization A's rows — organization B's row is invisible even though no EF query filter is in play. |
| `Raw_sql_with_no_organization_setting_sees_zero_rows_fail_closed` | No `SET LOCAL` at all → the policy's `NULLIF(current_setting(...), '')` comparison is `NULL`, matching nothing → **zero rows returned, not an error**. |
| `ExecuteDelete_under_the_application_role_cannot_delete_another_organizations_row` | EF's `ExecuteDeleteAsync()` — which bypasses the change tracker and the `SaveChanges` write guard entirely — targeting organization B's row by id while scoped to organization A deletes **zero rows**; the row is confirmed still present afterward. |
| `Insert_under_the_application_role_cannot_write_a_foreign_organization_id` | An `INSERT` under organization A's session setting, carrying `OrganizationId = <B>`, is rejected by Postgres with a row-level-security violation (`WITH CHECK`, not just `USING`). |

**The bug this test caught before it shipped:** the first version of the RLS policy compared
`organization_id = current_setting('app.organization_id', true)::uuid` directly. Running the
`Raw_sql_with_no_organization_setting_sees_zero_rows_fail_closed` test against a *pooled* Postgres
connection that a previous test had already run `SET LOCAL` on threw `22P02: invalid input syntax
for type uuid: ""` instead of returning zero rows — Postgres reverts a custom GUC touched by `SET
LOCAL` to `''`, not `NULL`, once the transaction ends. Fixed with `NULLIF(..., '')` before the
cast; see docs/DECISIONS.md for the full writeup. This is exactly why this test needs to run
against a real, connection-pooled Postgres and not a mock.

### Not covered here — deferred to Stage C (40.10+)

- ~~No service table has `OrganizationId` yet~~ — learning-service is the first, see the 40.10
  section below. The remaining services (ai, company, gamification, social, notification) are still
  outstanding; add a section here per service.
- ~~`TenantConnectionInterceptor` is registered but not added to any service's `DbContext`~~ —
  learning-service and identity-service both add it now.
- The role-provisioning SQL (`docs/TENANCY/sql/create_sellevate_app_role.sql`) is written but not
  run anywhere real — see `docs/DONT_FORGET.md`.

---

## 40.9 — platform superadmin, impersonation, and the live-data migration

### Backend (`Identity.Tests`, `Organization.Tests`)

`dotnet test src/backend/identity-service/Identity.Tests/Sellevate.Identity.Tests.csproj`
→ **121/121** (was 96/96 before this block).
`dotnet test src/backend/organization-service/Organization.Tests/Sellevate.Organization.Tests.csproj`
→ **31/31**.

| Test | Proves |
|------|--------|
| `PlatformAdminTests.StartImpersonation_AsOrdinaryUser_IsForbidden` | an `OrgAdmin` with a valid token and a valid organization cannot reach the impersonation endpoint |
| `…ListImpersonations_AsOrdinaryUser_IsForbidden`, `…BootstrapOrganizationAdmin_AsOrdinaryUser_IsForbidden` | the same for the other two platform routes — the gate is on the controller, not on one action |
| `…StartImpersonation_MintsAShortLivedNonEscalatingTokenAndAuditsIt` | the issued token carries `org_id`, `imp`, `imp_id`, `imp_actor`, `role: User` and **no** `SuperAdmin` anywhere, expires within the hour, and has a matching `ImpersonationAuditEntries` row with the actor and the stated reason |
| `…StartImpersonation_AppearsInTheAuditList` | the audit is readable, not just written |
| `…ImpersonationToken_CannotStartAnotherImpersonation` | chaining is refused — the dropped platform role is doing real work, not decoration |
| `…StartImpersonation_ForUnknownOrganization_IsNotFound` | fails closed on an organization identity-service has never seen |
| `…StartImpersonation_IntoSuspendedOrganization_IsForbidden` | suspension blocks platform staff too |
| `…BootstrapOrganizationAdmin_CreatesAnOrgAdminInviteThatCanBeAccepted` | the invite is a real Phase 40.7 invite: it is emailed, and accepting it produces an **active `OrgAdmin` membership** |
| `…BootstrapOrganizationAdmin_WhenAnInviteIsAlreadyPending_IsConflict`, `…WhenAnOrgAdminAlreadyExists_IsConflict` | the endpoint cannot be used as a back door into a running organization |
| `…BootstrapOrganizationAdmin_ForSuspendedOrganization_IsForbidden` | a suspended tenant cannot be staffed |
| `OrganizationSuspensionTests.Login_WhileOrganizationIsSuspended_IsForbidden` | a suspended organization actually blocks its users (`403`, not a silent success) |
| `…Login_AfterTheOrganizationIsReactivated_Succeeds` | and resuming actually unblocks them |
| `…RefreshToken_StopsWorkingOnceTheOrganizationIsSuspended` | an already-issued session does not outlive the suspension by the refresh token's 30-day lifetime |
| `…AcceptInvite_IntoASuspendedOrganization_IsForbidden` | the invite path is covered by the same choke point, not separately guarded |
| `…Login_WhenTheOrganizationIsNotYetReplicated_StillSucceeds` | **the negative of the negative**: a lagging Kafka consumer must never read as a suspension and lock a customer out |
| `OrganizationReplicaProjectorTests` (5) | created / updated / suspended projections, a suspension for an organization whose create event was never seen, and an unrelated topic being ignored |
| `PlatformAdminControllerContractTests` (3) | the platform controller is `RequireSuperAdmin` and **not** `[TenantScoped]`; `InvitesController` is still `RequireOrgAdmin` **and** `[TenantScoped]` — i.e. the bootstrap path did not loosen the ordinary one |
| `OrganizationControllerAuthorizationTests` (2) | the tenant registry requires `RequireSuperAdmin`; the organization profile deliberately does not |

Integration tests run without Kafka, so `TestOrganizationSeeder` writes the `OrganizationReplicas`
rows the consumer would have written, and `TestWebApplicationFactory` removes
`OrganizationReplicaConsumer` from the test host by exact implementation type (it resolves the
Redis-backed idempotency store at startup, and there is no Redis in the test environment). The
outbox relay and topic provisioner keep running exactly as before.

### Frontend

`cd src/frontend && npx vitest run` → **346/346** (was 338/338). `npx tsc --noEmit` clean.

`__tests__/adminOrganizations.test.tsx` covers the hooks and the impersonation session:
creating an organization never sends an organization id; suspend/resume go through the registry's
own routes; the first `OrgAdmin` is invited through `/admin/platform/...` and never through
`/invites`; impersonation always carries a reason; the platform token is parked and restorable; an
elapsed **or unparseable** expiry reads as expired rather than as an endless session.

### Lints

```bash
python3 scripts/tenancy-boundary-lint.py   # clean
python3 scripts/tenancy-pool-lint.py       # clean
```

The boundary lint now carries a two-file allow-list for the superadmin request bodies that name an
organization (the single carve-out in TENANCY.md §1.3) plus a check that a stale allow-list entry
is itself reported as a violation. Response DTOs avoid the exception entirely by nesting
`OrganizationReferenceDto(Id, Name)`.

### The data migration and its rollback

```bash
./scripts/tenancy-default-organization-verify.sh
```

Creates two **throwaway** databases (`tenancy_verify_identity`, `tenancy_verify_organization`),
builds their schema from the services' own EF migrations via
`dotnet ef migrations script --idempotent` — so the assertions run against the real schema, not a
hand-written imitation — seeds an awkward fixture (ordinary user, a user still holding the removed
global `Admin` role, a platform `SuperAdmin`, and someone who already has a membership in another
organization), then checks, in order:

1. the driver's default mode writes nothing;
2. the forward run gives every user a membership, maps the legacy admin and the superadmin to
   `OrgAdmin` and everyone else to `Manager`, leaves the pre-existing membership alone, keeps the
   platform role intact, and seeds the auth-config and replica rows;
3. running it a second time changes nothing (idempotent);
4. the rollback restores the user roles and the membership set **byte-for-byte** (md5 of the
   sorted rows), removes the registry row and the bookkeeping tables, and deletes no user;
5. the rollback **refuses** when someone has joined the organization after the backfill, and that
   person's membership survives the refusal.

24/24 assertions green; both databases are dropped on exit (`trap … EXIT`).

> The migration has been executed **only** against those throwaway databases. Running it on a copy
> of production and then on production is a human step — see `docs/DONT_FORGET.md` and
> [MICROSERVICES_PRODUCTION_MIGRATION.md §7](../MICROSERVICES_PRODUCTION_MIGRATION.md).

---

## 40.10 — learning-service (first Stage-C service)

### Unit tests — run on every build (`Learning.Tests`)

```
dotnet test src/backend/learning-service/Learning.Tests/Sellevate.Learning.Tests.csproj \
  --filter "TestCategory!=Integration"
```

61/61 green. Six of them are the tenancy tripwires in `Unit/LearningTenancyModelTests`:

| Test | What it would catch |
|---|---|
| `Every_entity_with_an_organization_id_has_its_own_query_filter` | The trap TENANCY.md §1.4 names: EF does not inherit filters through `Skill → Topic → Lesson → Exercise`. It walks the model rather than restating the entity list, since restating the list would repeat whichever omission it is meant to catch. `OutboxMessage` is the one asserted exception. |
| `Progress_tables_are_tenant_scoped_and_content_tables_are_not` | Someone making a content entity `ITenantScoped`, which would force a non-null owner and destroy the global library. |
| `Global_content_is_visible_to_every_organization` | Plain equality on a content filter — a new customer would get an empty skill tree on day one. |
| `One_organizations_progress_is_invisible_to_another` | A missing filter on a progress table. |
| `An_unset_tenant_sees_no_progress_and_cannot_write_any` | 40.14's rule: an unset tenant is an exception, never "all data". |
| `Writing_another_organizations_progress_row_is_refused` | The write guard silently accepting a foreign `OrganizationId`. |

The rest of the suite is the pre-existing learning tests, which now run inside a tenant context
(`Unit/LearningDbContextFactory` sets an organization and installs `TenantSaveChangesInterceptor`).

### Real-Postgres isolation test — **written, not run in the 40.10 session**

```
dotnet test src/backend/learning-service/Learning.Tests/Sellevate.Learning.Tests.csproj \
  --filter "TestCategory=Integration"
```

`Integration/LearningTenantIsolationIntegrationTests`, 8 tests. Per Правило №2 in
`docs/DONT_FORGET.md` these were written and committed but **not executed** by the agent — they are
slow, and the human runs them with the command above. They compile, and the unit suite is green
without them.

Same safety properties as `TenantRowLevelSecurityIntegrationTests`:

- no local Postgres → every test `Assert.Ignore`s within seconds (bounded 3-second probe), never
  hangs, never fails the build;
- its own throwaway database `learning_tenancy_integration_test` and its own throwaway login role
  `sellevate_learning_app_test`, dropped and recreated at the start and dropped at the end — the
  real `learning` database is never touched;
- the application role is `NOBYPASSRLS`, because testing isolation as the superuser proves nothing;
- the schema comes from the service's **own EF migrations**, so what is under test is the RLS the
  migration really emits, not a hand-copied restatement of it.

What the 8 tests cover:

| Test | Door it checks |
|---|---|
| `Content_read_through_the_navigation_chain_never_crosses_the_organization_boundary` | Navigation property: `Exercise → Lesson → Topic → Skill` eagerly loaded, asserted at **every** hop, not just the root. |
| `Global_content_stays_visible_to_both_organizations` | `NULL` content readable by A and B; each org's own content readable only by itself. |
| `Raw_sql_only_returns_the_current_organizations_progress` | Raw SQL — the door the EF filter does not guard at all. |
| `Raw_sql_with_no_organization_setting_sees_no_progress_at_all` | Fail-closed: unset tenant → zero rows across all four progress tables. |
| `Raw_sql_with_no_organization_setting_still_sees_the_global_library` | The difference between `EnableTenantRls` and `EnableTenantRlsForContent`. |
| `ExecuteUpdate_cannot_touch_another_organizations_progress` | `ExecuteUpdate` with `IgnoreQueryFilters()` — 0 rows, and B's score is still exactly what was seeded. |
| `ExecuteDelete_cannot_remove_another_organizations_progress` | `ExecuteDelete` with `IgnoreQueryFilters()` — 0 rows, and B's row survives. |
| `Inserting_progress_for_another_organization_is_refused_by_postgres` | `WITH CHECK` — a raw INSERT with a foreign `OrganizationId` raises `row-level security`. |
| `A_read_without_a_transaction_sees_no_progress_even_with_a_tenant_context` | Documents *why* every learning-service read path opens a `TenantTransactionScope`: `SET LOCAL` has no effect outside a transaction. |

### Lints

```
python3 scripts/tenancy-boundary-lint.py
python3 scripts/tenancy-pool-lint.py
```

Both clean. The pool lint matters more from 40.10 on: `LearningDbContext` closes over
`ITenantContext` in its query filters, so registering it with `AddDbContextPool` would leak one
tenant's filter onto another's request.

### The operational SQL — not run against anything

`docs/TENANCY/sql/40.10_learning_organization_backfill.sql` and
`..._indexes_concurrently.sql`, driven by `scripts/tenancy-learning-organization-rollout.sh`
(default mode writes nothing). Neither file has been executed against any database, real or
throwaway. See `docs/DONT_FORGET.md` for the order a human must run them in.
