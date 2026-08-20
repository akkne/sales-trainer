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

## 2026-08-16 — the platform/tenancy role split

Every service that gates a route declares the same four policies. Each of those services' test
projects carries an `AuthorizationPolicyContractTests` that builds the real
`IAuthorizationService` from `AuthorizationPolicies.Register` and asserts the same things, so a
service whose `Program.cs` stops registering them — or whose constants drift — fails locally rather
than in production.

```bash
dotnet test src/backend/ai-service/Ai.Tests            --filter "TestCategory!=Integration"
dotnet test src/backend/social-service/Social.Tests    --filter "TestCategory!=Integration"
dotnet test src/backend/learning-service/Learning.Tests --filter "TestCategory!=Integration"
dotnet test src/backend/gamification-service/Gamification.Tests --filter "TestCategory!=Integration"
dotnet test src/backend/organization-service/Organization.Tests --filter "TestCategory!=Integration"
```

| Test | Proves |
|------|--------|
| `The_policy_and_role_names_match_the_platform_contract` | the wire-level strings (`RequirePlatformAdmin`, `RequireSuperAdmin`, `RequireOrgAdmin`, `RequireOrgSuperAdmin`, `Admin`, `SuperAdmin`, `TenancyAdmin`, `TenancySuperAdmin`, `org_role`) are identical in every service — a rename in one would otherwise make the same token mean two different things |
| `All_four_policies_are_registered_and_resolvable` | the service's `Program.cs` actually calls `AuthorizationPolicies.Register`; without it every gated route fails closed |
| `A_platform_admin_passes_the_org_scoped_gate_without_an_org_role_claim` | the core of "не должны ограничиваться tenancy" — Sellevate staff hold no membership and must still work |
| `A_tenancy_admin_is_refused_the_add_and_remove_users_gate` | the single asymmetry between the two tenancy roles |
| `An_ordinary_user_passes_nothing` | the gates are gates |

Identity-service additionally carries `AuthorizationPolicyTests`, `RoleEnumContractTests`,
`TokenRoleClaimTests` and `InviteRoleValidationTests` — see
[TESTING/IDENTITY_SERVICE.md](IDENTITY_SERVICE.md).

---

## 40.9 — platform superadmin, impersonation, and the live-data migration

### Backend (`Identity.Tests`, `Organization.Tests`)

`dotnet test src/backend/identity-service/Identity.Tests/Sellevate.Identity.Tests.csproj`
→ **121/121** (was 96/96 before this block).
`dotnet test src/backend/organization-service/Organization.Tests/Sellevate.Organization.Tests.csproj`
→ **31/31**.

| Test | Proves |
|------|--------|
| `PlatformAdminTests.StartImpersonation_AsOrdinaryUser_IsForbidden` | a `TenancyAdmin` with a valid token and a valid organization cannot reach the impersonation endpoint |
| `…ListImpersonations_AsOrdinaryUser_IsForbidden`, `…BootstrapOrganizationAdmin_AsOrdinaryUser_IsForbidden` | the same for the other two platform routes — the gate is on the controller, not on one action |
| `…StartImpersonation_MintsAShortLivedNonEscalatingTokenAndAuditsIt` | the issued token carries `org_id`, `imp`, `imp_id`, `imp_actor`, `role: User` and **no** `SuperAdmin` anywhere, expires within the hour, and has a matching `ImpersonationAuditEntries` row with the actor and the stated reason |
| `…StartImpersonation_AppearsInTheAuditList` | the audit is readable, not just written |
| `…ImpersonationToken_CannotStartAnotherImpersonation` | chaining is refused — the dropped platform role is doing real work, not decoration |
| `…StartImpersonation_ForUnknownOrganization_IsNotFound` | fails closed on an organization identity-service has never seen |
| `…StartImpersonation_IntoSuspendedOrganization_IsForbidden` | suspension blocks platform staff too |
| `…BootstrapOrganizationAdmin_CreatesAnInviteAtTheChosenRoleThatCanBeAccepted` (2, `TenancySuperAdmin`/`TenancyAdmin`) | the invite is a real Phase 40.7 invite at whichever rank the request chose: it is emailed, and accepting it produces an **active membership at that exact role** (2026-08-20 — the role used to be hardcoded to `TenancySuperAdmin`) |
| `…BootstrapOrganizationAdmin_WhenRoleIsOmitted_DefaultsToTenancySuperAdmin` | a caller that predates the role field gets exactly what it always got |
| `…BootstrapOrganizationAdmin_WithManagerRole_IsRejectedWithBadRequest`, `…WithAnUnknownRole_IsRejectedWithBadRequest` | the endpoint may only choose which rank of administrator it bootstraps — `Manager` and anything unrecognized are a `400`, never a silent fallback |
| `…BootstrapOrganizationAdmin_WhenAnInviteIsAlreadyPending_IsConflictRegardlessOfItsRole` (2), `…WhenAnAdministratorAlreadyExists_IsConflictRegardlessOfRank` (2), `…AfterTheFirstAdministratorAcceptsTheInvite_ASecondBootstrapIsConflict` (2) | the endpoint cannot be used as a back door into a running organization, and neither guard was left checking only `TenancySuperAdmin` once the bootstrapped rank became selectable |
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
./scripts/tenancy-default-organization-verify.sh   # developer machine / CI only — needs the .NET SDK
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

### Checking a real server after the backfill

`verify.sh` proves the SQL; it never looks at your data, and it cannot run on a host without the
.NET SDK. The production-side counterpart is:

```bash
./scripts/tenancy-default-organization-check.sh
```

`SELECT` only — no SDK, no DDL, no temporary database. It asserts, against the live `organization`
and `identity` databases: the default organization exists and is `Active`; both bookkeeping rows
name the same organization id (the two databases have no foreign key between them); no user is left
without a membership; nobody still holds the removed global `Admin` role; the auth-config and
replica rows are present; no membership points at an organization identity has no replica of; and
name and slug agree across the two databases. Every check runs even after one fails, so one run
lists everything that is wrong. Exit code 0 = clean.

This is what `scripts/tenancy-rollout.sh` runs as its step 6.

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

---

## 40.11 — ai-service (Postgres + Mongo + Redis)

The first block whose boundary spans three stores, and each of them fails differently: Postgres has
row-level security, Mongo has nothing but `DialogSessionRepository`, and Redis has nothing but the
key name. The tests are split accordingly.

### Unit tests — run on every build (`Ai.Tests`)

```
dotnet test src/backend/ai-service/Ai.Tests/Sellevate.Ai.Tests.csproj \
  --filter "TestCategory!=Integration"
```

164/164 green (was 154/154). Six are the tenancy tripwires in `Unit/AiTenancyModelTests`:

| Test | What it would catch |
|---|---|
| `Every_entity_with_an_organization_id_has_its_own_query_filter` | The §1.4 trap: EF does not inherit filters through `DialogBundle → DialogMode`. It walks the model rather than restating the entity list. |
| `Dialog_sessions_are_tenant_data_and_the_dialog_library_is_content` | Someone making `DialogBundle`/`DialogMode` `ITenantScoped`, which would force a non-null owner and destroy the global library — or dropping `ITenantScoped` from `DialogSession`, which has no global case at all. |
| `Seeded_hidden_modes_stay_global_and_are_visible_to_every_organization` | The roadmap's explicit 40.11 requirement. Plain equality would give every new customer a practice page with no `company-call` and no `custom-scenario` entry point — the two modes the frontend looks up by key. |
| `One_organizations_authored_bundle_is_invisible_to_another` | A missing or wrong filter on the library. |
| `Every_session_repository_method_refuses_an_unset_tenant` | 40.14's rule, for the store that has no policy to fall back on. It walks `IDialogSessionRepository` by reflection, so a method added later without the guard fails the build. |
| `Only_the_repository_reaches_the_dialog_sessions_collection` | The regression that would quietly undo the whole block: a second class calling `GetCollection<DialogSession>` and filtering by hand. Asserted against the source tree, because nothing in C# enforces it. |

Three more live with the code they guard:

| Test | File | What it would catch |
|---|---|---|
| `Cached_verdict_is_namespaced_by_organization`, `Two_organizations_do_not_share_a_verdict_for_the_same_text`, `An_unset_tenant_does_not_touch_the_cache` | `Unit/ScenarioValidationTests` | One customer's cached verdict answering another's request — and the existence leak that comes with a text-hash key. |
| `ReserveSeconds_KeysAreNamespacedByOrganization` | `Unit/VoiceReservationGateTests` | A quota key without the prefix. |
| `RedisIdempotencyStoreKeyTests` (3) | `BuildingBlocks.Tests/Idempotency` | Two tenants sharing a dedupe namespace; and the deliberate exception — a platform-global event keeps the historical un-prefixed key, which is what preserves pre-40.11 dedupe. |

`BuildingBlocks.Tests` is 94/94 (was 91/91).

### Three-store isolation test — **written, not run in the 40.11 session**

```
dotnet test src/backend/ai-service/Ai.Tests/Sellevate.Ai.Tests.csproj \
  --filter "TestCategory=Integration"
```

`Integration/AiTenantIsolationIntegrationTests`, 11 tests. Per Правило №2 in
`docs/DONT_FORGET.md` they were written and committed but **not executed** by the agent. They
compile, and the unit suite is green without them.

Safety properties, same as 40.10's and extended per store:

- each store is probed independently with a bounded timeout, so a machine with Postgres but no
  Mongo still runs what it can, and a machine with nothing skips the whole file in seconds;
- its own throwaway Postgres database `ai_tenancy_integration_test` and login role
  `sellevate_ai_app_test`, its own throwaway Mongo database `ai_tenancy_integration_test` — the
  real `ai` and `sallevate` databases are never touched;
- Redis keys are written under the `ai-tenancy-test:` prefix and deleted **by name** in teardown.
  There is no `FLUSHDB` anywhere: that Redis is shared with the developer's running stack;
- the application role is `NOBYPASSRLS`, and the schema comes from ai-service's own EF migrations;
- sessions are seeded **through the repository**, not written into the collection by hand — had
  the repository stopped stamping the organization on write, hand-written documents would have let
  every assertion pass anyway.

| Test | Door it checks |
|---|---|
| `Global_dialog_library_stays_visible_to_both_organizations` | The content policy: `NULL` visible to A and B, each org's own only to itself. |
| `Row_level_security_hides_the_other_organization_even_with_query_filters_ignored` | Which layer is actually doing the work — `IgnoreQueryFilters()` strips the convenience one on purpose. |
| `Raw_sql_cannot_reach_the_other_organizations_modes` | Raw SQL, the door the EF filter does not guard at all. |
| `Writing_a_mode_into_another_organization_is_refused_by_the_policy` | `WITH CHECK` on the content policy. |
| `One_organizations_sessions_are_invisible_to_another` | The Mongo read path. |
| `Knowing_another_organizations_session_id_is_not_enough_to_read_it` | The realistic attack: session id **and** user id both travel in URLs. |
| `Writes_and_deletes_cannot_reach_another_organizations_session` | Update and delete, plus A's document still intact afterwards. |
| `Voice_usage_aggregation_never_totals_across_organizations` | The aggregation pipeline — the one place a `$match` stage could be reordered into uselessness. |
| `An_unset_tenant_reading_sessions_raises_instead_of_returning_everything` | The single most important behaviour in the block. |
| `Two_organizations_never_read_each_others_cached_verdict` | The Redis verdict namespace. |
| `Idempotency_keys_do_not_collide_across_organizations` | One organization marking an event processed must not suppress the other's copy. |

### Lints

```
python3 scripts/tenancy-boundary-lint.py
python3 scripts/tenancy-pool-lint.py
```

Both clean. The route-template check in the boundary lint was narrowed in this block: it now
inspects only routing attributes and minimal-API map calls, because `org:{organizationId}:` in a
Redis key is the tenancy fix and not a breach. A real
`[HttpGet("organizations/{organizationId}/things")]` still fails it.

### The operational scripts — not run against anything

`docs/TENANCY/mongo/40.11_dialog_sessions_organization_backfill.js` and
`docs/TENANCY/sql/40.11_ai_organization_indexes_concurrently.sql`, driven by
`scripts/tenancy-ai-organization-rollout.sh` (default mode writes nothing). Neither has been
executed against any database, Mongo, or Redis. See `docs/DONT_FORGET.md` for the order a human
must run them in — the Mongo step is the user-visible one.

---

## 40.12 — company-service (the first double scope)

Every earlier Stage-C block had one axis to test. This one has two, and they fail in opposite
directions: the organization half leaks between paying customers, the user half hands a salesperson
a colleague's private pipeline inside one customer. Every group below is deliberately split so a
regression in one half cannot be masked by the other still passing.

### Unit tests — run on every build (`Company.Tests`)

```
dotnet test src/backend/company-service/Company.Tests/Sellevate.Company.Tests.csproj \
  --filter "TestCategory!=Integration"
```

134/134 green (was 123/123). Eleven are the tenancy tripwires in `Unit/CompanyTenancyModelTests`:

| Test | What it would catch |
|---|---|
| `Every_entity_with_an_organization_id_has_its_own_query_filter` | The §1.4 trap: EF does not inherit filters through `Company → CallLogEntries / PracticeCalls / Contacts / Personas`. It walks the model rather than restating the entity list. Unlike learning and ai there is no exception list — nothing in company-db is global. |
| `Every_company_entity_is_tenant_scoped` | A future table added to this database without an organization, which would be the one unprotected row shape in a service that has no legitimate global data. |
| `A_company_written_by_one_organization_is_invisible_to_another` | A missing or wrong filter. It also asserts the row exists behind `IgnoreQueryFilters()`, so the test cannot pass by being an empty database. |
| `A_user_does_not_see_a_colleagues_companies_inside_the_same_organization` | **The half a single-tenant mindset misses.** Both rows are in the same organization, so the tenant filter admits both — only the explicit user predicate separates them. |
| `A_user_cannot_read_a_colleagues_call_log_through_their_company_id` | The sub-resource version: knowing a colleague's company id must not open its timeline. Before 40.12 sub-resource queries filtered on `CompanyId` alone. |
| `An_unset_tenant_reads_no_companies` | 40.14's rule: an unset tenant is an exception, never a licence. |
| `Writing_a_foreign_organization_id_raises` | `TenantSaveChangesInterceptor`'s write guard, exercised for real — the unit-test factory attaches the shipping interceptor rather than stamping organizations by hand. |
| `The_follow_up_reminder_service_raises_on_an_unset_tenant` | The roadmap's explicit requirement for background jobs. |
| `The_follow_up_reminder_service_refuses_to_run_in_system_mode` | The subtler version: system mode is not "no tenant", and it is refused just as firmly, because the legitimate system-mode read lives in the background service and hands a concrete organization down. |
| `The_follow_up_reminder_service_never_processes_across_organizations` | The pre-40.12 behaviour returning: two organizations due at the same moment, and a tick scoped to one must claim exactly one. |
| `The_due_follow_up_event_carries_the_organization_in_the_envelope` | 40.3's envelope field going unpopulated, which would leave the consumer with no tenant to process for. |

`BuildingBlocks.Tests` stays 94/94 after `IEventPublisher.PublishAsync` grew its optional
`organizationId` parameter.

### Real-Postgres isolation test — **written, not run in the 40.12 session**

```
dotnet test src/backend/company-service/Company.Tests/Sellevate.Company.Tests.csproj \
  --filter "TestCategory=Integration"
```

`Integration/CompanyTenantIsolationIntegrationTests`, 12 tests. Per Правило №2 in
`docs/DONT_FORGET.md` they were written and committed but **not executed** by the agent. They
compile, and the unit suite is green without them.

Safety properties, same as 40.10's and 40.11's:

- a bounded reachability probe, so a machine with no local Postgres skips the whole file in seconds
  rather than hanging;
- its own throwaway database `company_tenancy_integration_test` and login role
  `sellevate_company_app_test` — the real `company` database is never touched;
- the application role is `NOBYPASSRLS`, because testing isolation as the superuser proves nothing;
- the schema comes from company-service's own EF migrations, so what is under test is the RLS the
  migration really emits;
- the fixture is seeded through the admin connection, which bypasses RLS — seeding through the
  application role would be circular, since it could only create rows the policy already allows.

Organization half:

| Test | Door it checks |
|---|---|
| `A_read_through_the_navigation_chain_never_crosses_the_organization_boundary` | The §1.4 trap at every hop of `Company → CallLogEntries / PracticeCalls / Contacts / Personas`, not just at the root. |
| `Raw_sql_only_returns_the_current_organizations_rows` | Raw SQL, the door the EF filter does not guard at all. |
| `Raw_sql_with_no_organization_setting_sees_nothing_at_all` | Stronger than the same test in 40.10/40.11: company-db has no global library, so an unset tenant sees literally zero rows across all five tables. |
| `ExecuteUpdate_cannot_touch_another_organizations_company` | `ExecuteUpdate` with `IgnoreQueryFilters()` stripping the convenience layer on purpose, plus B's row intact afterwards. |
| `ExecuteDelete_cannot_remove_another_organizations_call_log` | Same for `ExecuteDelete`. |
| `Inserting_a_company_for_another_organization_is_refused_by_postgres` | `WITH CHECK`, i.e. the policy and not the application refusing the write. |
| `A_read_without_a_transaction_sees_nothing_even_with_a_tenant_context` | Why every read path opens a `TenantTransactionScope`. The symptom is especially quiet here: "no companies" looks exactly like a new user. |

User half, inside **one** organization:

| Test | Door it checks |
|---|---|
| `A_user_does_not_see_a_colleagues_companies_inside_the_same_organization` | Asserted through `CompanyService`, not through the `DbContext`, because the database deliberately admits both rows. |
| `A_colleagues_company_id_opens_none_of_its_sub_resources` | Company, call log, contacts, personas and practice calls — and the colleague still sees their own, so the nulls above are not an empty database in disguise. |
| `A_company_id_from_another_organization_behaves_exactly_like_an_unknown_id` | 404-shaped, never 403-shaped: the pre-existing rule extended to the new half. |

Background job:

| Test | Door it checks |
|---|---|
| `The_follow_up_poll_never_claims_another_organizations_due_company` | Both organizations due at the same moment; a tick scoped to A publishes one event, with A's id in the envelope, and leaves B's row unnotified. |
| `The_follow_up_poll_raises_on_an_unset_tenant_instead_of_scanning_everything` | The same guard as the unit tripwire, against a real database. |

### Lints

```
python3 scripts/tenancy-boundary-lint.py
python3 scripts/tenancy-pool-lint.py
```

Both clean. `CompanyDbContext` is registered with `AddDbContext`, never the pooled helper — a
pooled context would cache the first tenant's query filter and hand it to every later caller.

### The operational SQL — not run against anything

`docs/TENANCY/sql/40.12_company_organization_backfill.sql` and
`docs/TENANCY/sql/40.12_company_organization_indexes_concurrently.sql`, driven by
`scripts/tenancy-company-organization-rollout.sh` (default mode writes nothing). Neither has been
executed against any database. See `docs/DONT_FORGET.md` for the order a human must run them in —
the backfill is the user-visible step, and the index script is the one that keeps the cascade-delete
foreign keys indexed.

---

## 40.13 — the remaining services (identity, analytics, notification, gamification, social)

Five services, five different tenant boundaries: one Postgres table with a policy (learning/ai/
company's pattern, reused here for gamification and social), a background job with no table at all
(identity), and two stores with **no database and no RLS** (notification, analytics), where the
Redis key name is the entire boundary.

### identity-service — `IdentityBackgroundJobTenancyTests`

```
dotnet test src/backend/identity-service/Identity.Tests/Sellevate.Identity.Tests.csproj
```

62/62 green (was 58/58). Four tests, all source-tree tripwires rather than behavioural assertions —
`ExpiredRefreshTokenCleanupService`/`ExpiredEmailVerificationCleanupService` both call
`ExecuteDeleteAsync`, which the in-memory EF provider does not implement, so there is no behaviour
to exercise without a real Postgres:

| Test | What it would catch |
|---|---|
| `Cleanup_jobs_declare_an_explicit_tenant_mode` (×2 jobs) | A cleanup job resolving `IdentityDbContext` from a scope with no declared mode — indistinguishable at runtime from a deliberate `EnterSystemMode()`, and TENANCY.md §1.6 requires the intent written down. |
| `The_tenant_mode_is_declared_before_the_database_context_is_resolved` (×2 jobs) | `EnterSystemMode()` called *after* `GetRequiredService<IdentityDbContext>()` — would still contain the string and pass a naive check while the context had already captured a blank scope, since `TenantContext` refuses to change mode once set. |

No RLS, no `OrganizationId` column, and no isolation test: `RefreshTokens` and
`EmailVerificationCodes` are genuinely platform-wide (identities are cross-organization,
TENANCY.md §4.2) with nothing per-tenant to iterate. This block is entirely about the *honesty* of
the scope, not about new SQL.

### analytics-service — `PresenceTrackerTests` + `TrackingControllerTests`

```
dotnet test src/backend/analytics-service/Analytics.Tests/Sellevate.Analytics.Tests.csproj
```

37/37 green (was 31/31) — **no database, no `TestCategory=Integration` split**: this service's
`[Category("Integration")]` tests (`TrackingControllerTests`) run against
`AnalyticsWebApplicationFactory` with Redis and Kafka stubbed, no Docker required, so they run in
every session and are counted in the 37.

| Test | What it would catch |
|---|---|
| `MarkSeenAsync_AddsUserToTheOrganizationsOwnOnlineSet`, `MarkSeenAsync_UsesADifferentKeyForEachOrganization` | A presence key not actually keyed by organization. |
| `MarkSeenAsync_RegistersTheOrganizationSoTheGaugeCanFindIt` | The `presence:organizations` registry going unwritten, which would make the platform-wide gauge blind to a whole organization. |
| `MarkSeenAsync_WithNoOrganization_Raises`, `CountOnlineAsync_WithNoOrganization_Raises` | An empty organization silently building `org:00000000-...:presence:online` — one shared bucket for every caller whose context was missing. |
| `CountOnlineAsync_DoesNotSeeAnotherOrganizationsUsers` | The count itself crossing the boundary. |
| `CountOnlineAcrossAllOrganizationsAsync_SumsEveryRegisteredOrganization` | The one legitimate system-mode read — the `app_users_online` gauge — silently missing an organization. |
| `PruneAsync_RemovesStaleMembersFromEachRegisteredOrganization`, `PruneAsync_ForgetsAnOrganizationOnceItsSetIsEmpty` | The registry growing forever, or compaction touching the wrong organization's set. |

`TrackingControllerTests` adds the endpoint-level check: `POST /tracking/presence/ping` without
`X-Organization-Id` is `403` (the route is `[TenantScoped]`), not a pooled ping.

### notification-service — `NotificationTenancyTests`

```
dotnet test src/backend/notification-service/Notification.Tests/Sellevate.Notification.Tests.csproj
```

56/56 green (was 46/46) — Redis-only, no `TestCategory=Integration` split: the store under test in
`NotificationTenancyTests` is `InMemoryNotificationStore`, now keyed by `(organization, recipient)`
rather than `recipient` alone, so a fake that ignored the organization would fail the isolation
tests for the wrong reason (i.e. it actually models the real key shape).

| Test | What it would catch |
|---|---|
| `Inbox_keys_carry_the_organization_prefix`, `Unread_count_keys_carry_the_organization_prefix`, `Chat_email_watermark_keys_carry_the_organization_prefix` | Any of the three key builders losing the `org:{orgId}:` prefix. |
| `Two_organizations_never_share_an_inbox_key` | Two different organizations resolving to the same Redis key by construction. |
| `An_unset_organization_raises_rather_than_building_a_zero_key` | The `org:00000000-...` trap — a shared bucket that looks correctly namespaced. |
| `Creating_a_notification_without_a_tenant_raises`, `Reading_notifications_without_a_tenant_raises` | `NotificationService` — the one class holding a real `ITenantContext` — forgetting to require it. There is no system-mode path for an inbox. |
| `A_notification_written_in_one_organization_is_invisible_in_another` | The store itself leaking across the (organization, recipient) key. |
| `The_unread_count_does_not_include_another_organizations_notifications` | The counter drifting out of sync with the namespaced list. |

`NotificationController` being `[TenantScoped]` and `NotificationEventConsumer` keeping
`RequiresOrganization = true` are asserted by the existing controller/consumer contract tests, not
duplicated here.

### gamification-service

```
dotnet test src/backend/gamification-service/Gamification.Tests/Sellevate.Gamification.Tests.csproj
```

42/42 green. **Unlike every other Stage-C service, this block shipped with no dedicated tenancy
tripwire file and no `Integration` isolation-test project** — there is no
`GamificationTenancyModelTests` walking the model for a missing query filter (the pattern learning/
ai/company/social all used), and no `GamificationTenantIsolationIntegrationTests` against a real
Postgres role. `StreakResetJobTests` and `StreakTimezoneTests` were updated to pass an
`OrganizationId` through the existing fixtures, and `GamificationDbContextFactory` (the shared test
factory) now attaches an organization, but neither is a tenancy-isolation test in the sense the
other 40.10–40.13 blocks mean it. This is a real gap relative to the roadmap's stated pattern, not a
documentation omission — see the report accompanying this documentation pass.

### social-service — `SocialTenancyModelTests`

```
dotnet test src/backend/social-service/Social.Tests/Sellevate.Social.Tests.csproj \
  --filter "TestCategory!=Integration"
```

56/56 green (was 46/46 before the tripwire commit). Ten tests in
`Unit/SocialTenancyModelTests`:

| Test | What it would catch |
|---|---|
| `Every_entity_with_an_organization_id_has_its_own_query_filter` | The §1.4 trap: EF does not inherit filters through `DiscussThread → DiscussReplies`. Walked from the model, not a restated list. |
| `Social_rows_are_tenant_data_and_tags_are_content` | Someone making `DiscussTags` `ITenantScoped` (destroying the shared vocabulary) or removing the content flavour from it. |
| `A_friendship_created_in_one_organization_is_invisible_in_another` | A missing or wrong filter on `Friendships`. |
| `Chat_cannot_be_opened_across_the_organization_boundary` | `ChatService` no longer refusing to open a conversation between non-friends, which is the structural half of the chat boundary. |
| `Curated_tags_are_shared_and_an_organizations_own_tag_is_not` | Plain equality on the `DiscussTags` filter — a new customer would open Discuss and find no tags at all. |
| `A_tag_typed_by_a_user_belongs_to_their_organization` | `ResolveOrCreateTagsAsync` failing to stamp a user-typed tag, leaking it into the curated vocabulary. |
| `Photo_object_keys_are_namespaced_by_organization` | A new upload's `ObjectKey` losing the `org/{organizationId}/...` prefix. |
| `Every_conversation_repository_method_refuses_an_unset_tenant` | Walked from `IChatConversationRepository` by reflection — a method added later without the guard fails the build. |
| `Only_the_repository_reaches_the_chat_conversations_collection` | The regression that would quietly undo the whole Mongo boundary: a second class calling `GetCollection<ChatConversation>` and filtering by hand. Asserted against the source tree, because nothing in C# enforces it. |
| `Published_events_carry_the_organization_and_refuse_an_unset_tenant` | `KafkaSocialEventPublisher` publishing an unstamped envelope — which notification-service's `RequiresOrganization = true` would then dead-letter silently. |

### Real-Postgres-and-Mongo isolation test — **written, not run in the 40.13 session**

```
dotnet test src/backend/social-service/Social.Tests/Sellevate.Social.Tests.csproj \
  --filter "TestCategory=Integration"
```

`Integration/SocialTenantIsolationIntegrationTests`, 12 tests across both stores. Per Правило №2 in
`docs/DONT_FORGET.md` they were written and committed but **not executed**. They build their schema
from social-service's own EF migrations (so what is under test is the RLS the migrations actually
emit, not a restatement of it) and read as a throwaway `NOBYPASSRLS` role.

Postgres half:

| Test | Door it checks |
|---|---|
| `One_organizations_threads_are_invisible_to_another` | A missing or wrong filter on `DiscussThreads`. |
| `Row_level_security_hides_the_other_organization_even_with_query_filters_ignored` | `IgnoreQueryFilters()` stripping the convenience layer — the policy, not the filter, is what's actually tested. |
| `Raw_sql_cannot_reach_the_other_organizations_replies` | Raw SQL, the door the EF filter does not guard at all. |
| `A_read_outside_a_transaction_sees_nothing_rather_than_everything` | The `SET LOCAL` rule — a bare `SELECT` under RLS fails closed, not open. |
| `Writing_a_thread_into_another_organization_is_refused_by_the_policy` | `WITH CHECK`, i.e. Postgres refusing the write, not the application. |
| `Curated_tags_are_shared_and_an_organizations_own_tag_is_not` | The content-flavour policy against a real database, not just the EF filter. |
| `The_same_two_people_can_be_friends_in_two_organizations` | The organization-first unique index on `Friendships` — the case the plain pre-40.13 constraint would have refused. |

Mongo half:

| Test | Door it checks |
|---|---|
| `One_organizations_conversations_are_invisible_to_another` | The repository's read filter. |
| `Knowing_another_organizations_conversation_id_is_not_enough_to_read_it` | The realistic attack: a conversation id travels in the URL. |
| `Writes_cannot_reach_another_organizations_conversation` | Append-message / read-watermark writes scoped the same way as reads. |
| `Finding_by_participants_resolves_within_the_callers_organization` | Two organizations with the same pair of participant ids (memberships, 40.6) resolving to different conversations. |
| `An_unset_tenant_reading_conversations_raises_instead_of_returning_everything` | The single most important behaviour in the Mongo half — no RLS to fall back on, so the repository itself must fail closed. |

### Lints

```
python3 scripts/tenancy-boundary-lint.py
python3 scripts/tenancy-pool-lint.py
```

Clean for social-service (`SocialDbContext` uses `AddDbContext`, never the pooled helper) and
gamification-service. Not re-run against identity/analytics/notification, which have no
tenant-scoped `DbContext` to lint in the first place.

### The operational scripts — not run against anything

- `docs/TENANCY/sql/40.13_gamification_organization_backfill.sql`,
  `docs/TENANCY/sql/40.13_gamification_organization_indexes_concurrently.sql`, driven by
  `scripts/tenancy-gamification-organization-rollout.sh`.
- `docs/TENANCY/sql/40.13_social_organization_backfill.sql`,
  `docs/TENANCY/sql/40.13_social_organization_indexes_concurrently.sql`, and
  `docs/TENANCY/mongo/40.13_chat_conversations_organization_backfill.js`, driven by
  `scripts/tenancy-social-organization-rollout.sh`.

Neither driver has been run against any database, real or throwaway. See `docs/DONT_FORGET.md` for
the rollout order for both services, and for the Redis-only steps (deleting the retired
`presence:online` key; nothing to run for notification-service's old keys, which expire on their
own TTL).

---

## Разделение ролей 2026-08-16 — платформенный режим чтения

Третий режим тенант-контекста (`IsPlatformWide`) — сознательный обход изоляции, поэтому проверять
его нужно с обеих сторон: что он открывается там, где должен, и **не** открывается больше нигде.

### BuildingBlocks — 118/118 зелёных

```
dotnet test src/backend/building-blocks/BuildingBlocks.Tests --filter "TestCategory!=Integration"
```

Что закрыто:

- `TenantContextTests` — режим включается, совместим с заданной организацией в любом порядке,
  взаимоисключающ с системным в обе стороны, идемпотентен.
- `TenantContextMiddlewareTests` — режим открывается **только** клеймом `role` = `Admin`/`SuperAdmin`
  валидированного принципала. Отдельными тестами закрыты: подделанный `X-User-Role`, выдуманный
  `X-Platform-Mode`, неаутентифицированный принципал с нужным клеймом, роли `User` /
  `TenancyAdmin` / `TenancySuperAdmin` (в том числе токен импersonation, который выпускается с
  `role: User`). Плюс: платформенный персонал проходит `[TenantScoped]` без заголовка, а
  организация, если она есть, сохраняется.
- `TenantConnectionInterceptorTests` — точный текст `SET LOCAL` для всех трёх режимов, включая
  случай «организация + платформенный режим» (выдаются оба оператора) и «системный режим не
  выдаёт ничего».
- `TenantRlsMigrationBuilderExtensionsTests` — ветка платформы попадает в `USING` и **не** попадает
  в `WITH CHECK` (проверяется по отдельности для строгой и контентной политики), пустой GUC
  трактуется как `off`, `admitPlatformStaff: false` восстанавливает дореформенную политику,
  политика пересоздаётся (`DROP POLICY IF EXISTS` перед `CREATE POLICY`).

### Пер-сервисные tripwire-тесты — зелёные

```
dotnet test src/backend/learning-service/Learning.Tests     # 67
dotnet test src/backend/ai-service/Ai.Tests                 # 170
dotnet test src/backend/company-service/Company.Tests       # 135
dotnet test src/backend/social-service/Social.Tests         # 62
dotnet test src/backend/gamification-service/Gamification.Tests  # 53
```

`Every_query_filter_admits_platform_staff` обходит построенную модель и валит сборку, если у
какой-то сущности фильтр забыл ветку `IsPlatformWide`. Это не косметика: такой фильтр не бросает
исключений — платформенный сотрудник просто видит пустой экран и все решают, что данных нет.

Здесь же закрыт пробел 40.13: у gamification-service появился свой `GamificationTenancyModelTests`
(на который `GamificationDbContext` уже ссылался в комментарии) — обе tripwire-проверки, разметка
`ITenantScoped` по таблицам и каталогам, изоляция чужой организации и видимость обеих для
платформенного персонала.

### Интеграционные — **написаны, не запускались** (правило №2)

- `Learning.Tests/Integration/LearningTenantIsolationIntegrationTests.cs`: платформенный персонал
  читает прогресс и контент обеих организаций; сырой SQL с включённым `app.platform_mode` под
  ролью приложения видит обе; **запись без организации отклоняется самим Postgres** (ветки в
  `WITH CHECK` нет); обычный тенант по-прежнему видит только своё — контрольный тест, без которого
  предыдущие прошли бы и на политике, потерявшей сравнение организаций вообще.
- `Social.Tests/Integration/SocialTenantIsolationIntegrationTests.cs`: чат обеих организаций виден
  платформенному персоналу; репозиторий вообще без тенанта по-прежнему бросает, а не отдаёт всё.

Запускать человеку вместе с остальными интеграционными: `--filter "TestCategory=Integration"` при
поднятой `scripts/dev-infra.sh`, и **после** применения семи миграций
`RefreshTenantPoliciesForPlatformStaff` — до них платформенные тесты обязаны падать, это и есть
проверка того, что расширение даёт именно RLS, а не EF-фильтр.

---

## 40.14 — приёмка изоляции: чеклист для человека

Этот раздел — **инструкция, а не описание тестов**. Всё выше отвечает на вопрос «что покрыто»;
здесь — что запустить руками и что посмотреть глазами, чтобы сказать «изоляция принята».
Порядок имеет значение: шаги 1–3 быстрые и не требуют ничего, кроме репозитория, шаг 4 требует
поднятой локальной инфраструктуры, шаг 5 — применённых миграций.

Реестр фоновых задач, который этот блок произвёл: **[docs/TENANCY/BACKGROUND_JOBS.md](../TENANCY/BACKGROUND_JOBS.md)**.

### Шаг 0 — ловушка, из-за которой можно принять изоляцию по 6% тестов

```bash
dotnet test src/backend/Sellevate.sln      # ← НЕ ДЕЛАЙТЕ ТАК
```

Эта команда печатает `A total of 1 test files matched the specified pattern.` и прогоняет
**один** тест-проект из одиннадцати — 53 теста из 894. Заканчивается зелёным `Passed!`, поэтому
выглядит как полный прогон и им не является. Правильная команда — цикл по проектам:

```bash
for p in $(find src/backend -name "*.Tests.csproj" -not -path "*/obj/*" | sort); do
  echo "== $p"
  dotnet test "$p" --no-build --filter "TestCategory!=Integration" --nologo | grep -E "Passed!|Failed!"
done
```

Ожидаемо (состояние на 40.14): **894 зелёных, 0 упавших, 0 пропущенных**.

| Проект | Юнит |
|--------|------|
| Ai | 170 |
| Analytics | 29 |
| BuildingBlocks | 118 |
| Company | 135 |
| Gamification | 53 |
| Gateway | 71 |
| Identity | 97 |
| Learning | 67 |
| Notification | 56 |
| Organization | 36 |
| Social | 62 |

### Шаг 1 — сборка и линты (секунды, ничего не поднимая)

```bash
dotnet build src/backend/Sellevate.sln
python3 scripts/tenancy-boundary-lint.py    # организация не приходит из тела/query/маршрута
python3 scripts/tenancy-pool-lint.py        # нет AddDbContextPool на tenant-scoped контекстах
```

Оба линта должны сказать `clean.` Первый — это гейт против самой дешёвой ошибки во всей теме:
`?organizationId=...` в запросе. Второй — против ловушки из TENANCY.md §1.4: пул контекстов
закэшировал бы фильтр первого тенанта и раздавал бы его всем следующим.

### Шаг 2 — три грепа, которые держат реестр фоновых задач честным

Полное объяснение — в [BACKGROUND_JOBS.md](../TENANCY/BACKGROUND_JOBS.md) §5. Коротко:

```bash
# 2.1 Каждая зарегистрированная фоновая задача должна быть в реестре
grep -rn --include=*.cs "AddHostedService" src/backend | grep -v /obj/ | grep -v /bin/

# 2.2 IgnoreQueryFilters в продакшн-коде — ТОЛЬКО перечисление организаций.
#     На 40.14 ожидалось ровно три попадания: FollowUpReminderBackgroundService,
#     StreakResetJob, WeeklyLeagueClosureJob. С 40.23 к ним добавился
#     AssignmentDeadlineSweepService, с 40.24 — AssignmentRepeatSweepService,
#     с 40.27 — ContentGenerationSweepService, с 40.32 —
#     ContentAdaptationSweepService. На 40.34 ожидается ровно СЕМЬ попаданий и
#     ни одним больше; восьмое — находка, пока не доказано обратное.
#     (Число проверено грепом в блоке 40.34. Формулировка «остаётся шесть» из
#     заметки блока 40.31 в DONT_FORGET.md была верна на момент 40.31 и
#     устарела в 40.32 — сверяйтесь с этой строкой, а не с той.)
grep -rn --include=*.cs "IgnoreQueryFilters" src/backend | grep -v /obj/ | grep -v Tests

# 2.3 Сырого SQL в бэкенде быть не должно вообще (на 40.14 — ноль попаданий)
grep -rnE "FromSqlRaw|FromSqlInterpolated|ExecuteSqlRaw|ExecuteSqlInterpolated" \
  --include=*.cs src/backend | grep -v /obj/
```

Автоматического гейта на 2.2 и 2.3 **нет** — это глазами. Превратить их в линт по образцу
`tenancy-boundary-lint.py` — очевидный следующий шаг, сознательно не входящий в 40.14.

### Шаг 3 — что смотреть глазами в собранной системе

Ни один из этих пунктов не покрыт автотестом, и каждый ловит ошибку, которая выглядит не как
ошибка, а как «данных нет»:

1. **Заголовок `X-Organization-Id` не проходит снаружи.** С фронта всё работает, потому что его
   ставит гейтвей. Проверка — послать его руками мимо гейтвея и убедиться, что гейтвей его срезает,
   а не пробрасывает:
   ```bash
   curl -s -H "Authorization: Bearer <ваш токен>" \
        -H "X-Organization-Id: 00000000-0000-4000-8000-000000000009" \
        http://localhost:5001/companies | head
   ```
   Ответ должен быть про **вашу** организацию, а не про подставленную. Гейтвей срезает все три
   identity-заголовка безусловно и переставляет их из валидированного токена
   (`gateway/Gateway/IdentityForwarding.cs`).

2. **Маршруты `[TenantScoped]` отвечают 403 без заголовка, а не 200 с пустотой.** Помеченные
   контроллеры: `/notifications/*`, `POST /tracking/presence/ping`, профиль организации.
   ai-service, company-service и learning-service атрибутом **не** помечены — там запрос без
   организации даёт пустой результат или 500, потому что фильтры и гуарды и так fail-closed. Это
   косметика, но при разборе инцидента она стоит часа: 403 говорит «нет тенанта», пустой список
   говорит «нет данных».

3. **Пустые экраны после деплоя — это RLS, а не потеря данных.** Между применением миграции
   `AddOrganizationId` и прогоном бэкфилла строки лежат в организации-заглушке из нулей и спрятаны
   политикой. Правильное поведение, выглядит как «всё стёрли». Порядок и окна — в
   `docs/DONT_FORGET.md`, по одному пункту на сервис.

4. **Фоновые джобы не молчат.** Признак беды не в логах ошибок, а в их отсутствии: перечисление
   организаций идёт в системном режиме и под `NOBYPASSRLS`-ролью вернёт пустой список, после чего
   напоминания, сброс серий и закрытие лиг просто перестанут происходить, **не выдав ни одной
   ошибки**. Смотреть на счётчики в `LogInformation` этих джоб («no organization has a live
   streak»), а не на error rate.

### Шаг 4 — интеграционные тесты изоляции: написаны, но НИ РАЗУ не прогонялись

По Правилу №2 в `docs/DONT_FORGET.md` агент их писал и коммитил, но не запускал. Это **131 тест**,
и они — основная непроверенная часть приёмки. Все фикстуры создают свою одноразовую БД и
одноразовую `NOBYPASSRLS`-роль, дропают их в начале и в конце и настоящих БД сервисов не трогают.
Нет локального Postgres → уходят в `Skipped` за секунды, сборка не виснет.

```bash
scripts/dev-infra.sh    # postgres/mongo/redis в Docker — нужны только для этого шага

dotnet test src/backend/ai-service/Ai.Tests/Sellevate.Ai.Tests.csproj                       --filter "TestCategory=Integration"   #  11
dotnet test src/backend/analytics-service/Analytics.Tests/Sellevate.Analytics.Tests.csproj  --filter "TestCategory=Integration"   #   8
dotnet test src/backend/company-service/Company.Tests/Sellevate.Company.Tests.csproj        --filter "TestCategory=Integration"   #  12
dotnet test src/backend/identity-service/Identity.Tests/Sellevate.Identity.Tests.csproj     --filter "TestCategory=Integration"   #  72
dotnet test src/backend/learning-service/Learning.Tests/Sellevate.Learning.Tests.csproj     --filter "TestCategory=Integration"   #  14
dotnet test src/backend/social-service/Social.Tests/Sellevate.Social.Tests.csproj           --filter "TestCategory=Integration"   #  14
```

| Файл | Тестов | Что доказывает | Блок |
|------|--------|----------------|------|
| `Ai.Tests/Integration/AiTenantIsolationIntegrationTests.cs` | 11 | Три хранилища сразу: Postgres (контентная RLS на `DialogBundles`/`DialogModes`), Mongo (`dialog_sessions` — прикладной фильтр, RLS нет), Redis (префиксы ключей) | 40.11 |
| `Analytics.Tests/Integration/TrackingControllerTests.cs` | 8 | Presence и воронки по префиксу организации; Redis и Kafka застабаны, Docker не нужен | 40.13 |
| `Company.Tests/Integration/CompanyTenantIsolationIntegrationTests.cs` | 12 | Двойной скоуп — организация И пользователь; `IgnoreQueryFilters` + `ExecuteUpdate`/`ExecuteDelete` под ролью приложения упираются в RLS, а не в EF-фильтр | 40.12 |
| `Identity.Tests/Integration/*.cs` (8 файлов) | 72 | Инвайты, membership-клеймы, платформенная админка, разделение ролей 2026-08-16, вход и приостановка организации | 40.7–40.9 + роли |
| `Learning.Tests/Integration/LearningTenantIsolationIntegrationTests.cs` | 14 | Прогресс и контент двух организаций; сырой SQL и `ExecuteDelete`; +5 сценариев платформенного режима | 40.10 + роли |
| `Social.Tests/Integration/SocialTenantIsolationIntegrationTests.cs` | 14 | Postgres (шесть таблиц строгой RLS + контентные теги) **и** Mongo (`chat_conversations`); +2 сценария платформенного режима | 40.13 + роли |

**Важно про порядок.** Тесты платформенного режима обязаны **падать** до применения семи миграций
`RefreshTenantPoliciesForPlatformStaff` — это и есть проверка того, что расширение чтения даёт
именно RLS, а не EF-фильтр. Прогонять их имеет смысл дважды: до миграций (ожидаемо красные) и
после (ожидаемо зелёные).

**Чего в этом списке нет.** `gamification-service` — единственный сервис Stage C **без** теста
изоляции на реальном Postgres (пробел зафиксирован ещё в 40.13). И сквозного теста «две
организации, полный набор операций, ни один endpoint не отдаёт чужие данные», который требовал
роадмап 40.14, **не существует** — он не написан по Правилу №3, см. `docs/DONT_FORGET.md`.

### Шаг 5 — RLS ещё ни в одном окружении не включена

Самая важная строчка всей приёмки, и она не про тесты. Все compose-файлы подключают сервисы под
`${POSTGRES_USER}` — владельцем схемы. `FORCE ROW LEVEL SECURITY` **не применяется к
суперпользователю**, поэтому ни одна из политик, созданных семью миграциями `AddOrganizationId` и
семью `RefreshTenantPoliciesForPlatformStaff`, сейчас ничего не фильтрует.

Сегодня граница держится на слоях 1 и 2 — middleware и EF-фильтры, — и ревью 40.14 нашло их
целыми. Но свойство «переживает забытый фильтр», ради которого RLS и вводилась, **пока не
существует**. Пока не выполнен переход на роль `sellevate_app`
(`docs/TENANCY/sql/create_sellevate_app_role.sql`), четвёртый слой приёмки честно считать
непройденным.

Проверить, что политики хотя бы созданы (только чтение, под ролью-владельцем):

```sql
SELECT tablename, policyname, qual, with_check FROM pg_policies
WHERE policyname LIKE '%_tenant_isolation' ORDER BY tablename;
-- в qual должен быть app.platform_mode, в with_check — НЕ должен
```

Проверить, под кем реально ходит сервис:

```sql
SELECT current_user, usesuper, userepl FROM pg_user WHERE usename = current_user;
-- usesuper = true → RLS не фильтрует ничего
```

### Что ревью безопасности 40.14 признало чистым

Прогон `security-reviewer` (opus) по границе тенанта: **0 критичных**, ни одного пути, отдающего
данные одной организации пользователю другой. Восемь из двенадцати областей закрыты полностью:

- **25 из 25** сущностей `ITenantScoped` имеют И EF query filter, И RLS-политику — соответствие
  один-к-одному, без сирот в обе стороны; каждое исключение названо и обосновано в doc-комментарии
  своей миграции;
- `IgnoreQueryFilters()` в продакшн-коде — ровно три места, все внутри явного системного режима,
  все проецируют **только** колонку `OrganizationId` и не читают содержимое строк;
- сырого SQL в бэкенде нет вообще (ноль `FromSqlRaw`/`ExecuteSqlRaw`); `AddDbContextPool` нет нигде;
- гейтвей срезает identity-заголовки безусловно и переставляет их из валидированного токена;
  порядок middleware (`UseAuthentication` → `UseAuthorization` → `UseSellevateTenantContext`)
  одинаков и корректен во всех девяти сервисах;
- четыре политики авторизации байт-в-байт одинаковы в шести сервисах, где объявлены — дрейфа нет;
- платформенный режим открывается **только** клеймом валидированного принципала; ветка
  `app.platform_mode` попадает в `USING` и не попадает в `WITH CHECK`;
- обе Mongo-коллекции — настоящие чокпойнты: хендл коллекции создаётся ровно в одном классе, что
  проверяется тестом по исходникам;
- секретов в репозитории нет: каждый `Jwt:Key` — литерал `INJECTED_FROM_ENV`, `.env` в gitignore.

Найденное и **исправленное** в этом же блоке — пять правок, коммит `af7ff0e`. Найденное и
**отложенное владельцу** (включая `POST /demo/token`, который в текущей конфигурации доступен из
интернета) — в `docs/DONT_FORGET.md`.

**Не прогонялся:** `dotnet list package --vulnerable --include-transitive` — требует restore и сети.
К границе тенанта отношения не имеет, но в приёмке это честный пробел.

---

## 40.16 — привязка прогресса к версии: чеклист для человека

Тест-регрессия, которую требовал роадмап («правка правильного ответа не меняет историческую
точность»), **не написана** — Правило №3 в `docs/DONT_FORGET.md`. Ниже — то, чем её пока заменяют:
ручной прогон того же сценария. Документация Правилом №3 не запрещена, и это ровно тот же ход, что
40.14 сделал вместо сквозного теста изоляции.

### Шаг 1 — что прогоняется автоматически (секунды, ничего не поднимая)

```bash
dotnet build src/backend/Sellevate.sln
dotnet test src/backend/learning-service/Learning.Tests/Sellevate.Learning.Tests.csproj --filter "TestCategory!=Integration"
dotnet test src/backend/building-blocks/BuildingBlocks.Tests/Sellevate.BuildingBlocks.Tests.csproj --filter "TestCategory!=Integration"
python3 scripts/tenancy-boundary-lint.py
python3 scripts/tenancy-pool-lint.py
```

Ожидание: сборка без ошибок, learning 67/67, BuildingBlocks 114/118 (4 `Skipped` — нет локального
Postgres), оба линта `clean`. Прогон 2026-08-17 дал ровно это.

Важно понимать, чего этот шаг **не** проверяет: ни одного из шести свойств блока. Три существующих
теста `ExerciseService` теперь конструируют его с настоящим `LessonVersionService`, поэтому путь
«у урока нет версий → создать версию 1 → записать её в попытку» хотя бы **исполняется** на каждом
прогоне; но ни один assert на `LessonVersionId` в них не смотрит.

### Шаг 2 — сценарий, который должна была проверять тест-регрессия

Руками, через API (экрана нет — фронт в блоке не трогался). Организация одна, роль — админ
организации или платформенный админ.

1. `POST /admin/lessons/{id}/versions/publish` с телом `{}` → в ответе `createdNewVersion: true`,
   `versionNumber: 1`. (Если сервис уже стартовал на новом коде, версия 1 у урока уже есть — тогда
   ответ будет `createdNewVersion: false`, и это правильно.)
2. Продавцом ответить на упражнение урока — правильно.
3. `GET /admin/lessons/{id}/accuracy` → один сегмент, `startVersionNumber: 1`,
   `statistics.accuracy: 1`, `attemptCount: 1`. Записать эти числа.
4. `PUT /admin/exercises/{id}` — **поменять правильный ответ** (не текст, а именно ключ ответа).
5. `POST /admin/lessons/{id}/versions/publish` с телом `{"isBreaking": true}` →
   `createdNewVersion: true`, `versionNumber: 2`.
6. `GET /admin/lessons/{id}/accuracy` ещё раз. **Главная проверка блока:** сегмент версии 1 не
   изменился — те же `attemptCount`, `correctAttemptCount` и `accuracy`, что на шаге 3, — и версия 2
   лежит в **отдельном** сегменте (`startsAtBreakingChange: true`), а не продолжает первый.
7. Контрольная половина: поправить в упражнении текст (опечатку) и опубликовать с
   `{"isBreaking": false}` → версия 3. В ответе `/accuracy` сегментов по-прежнему **два**, и версия 3
   лежит во втором вместе с версией 2 (`versionNumbers: [2, 3]`).

Если шаг 6 показал изменившиеся числа — блок сломан, и сломан молча: ни исключения, ни строки в
логе. Ровно поэтому этот сценарий и стоит первым в списке «Тесты, которых нет».

### Шаг 3 — что смотреть глазами при первом старте на новом коде

В логе `learning-service` при старте должны быть две строки:

```
Lesson version backfill starting for N lesson(s) with no published version
Lesson version backfill created N initial version(s)
```

На втором старте — **ни одной** (бэкфилл идемпотентен и выходит по нулю строк). Если между ними есть
`Lesson version backfill could not snapshot LessonId=...`, у этого урока содержимое упражнения — не
валидный JSON: урок останется без версии, а шаг 2 раскатки откажется работать и назовёт их число.

### Шаг 4 — операционные скрипты: не выполнялись ни против чего

`docs/TENANCY/sql/40.16_progress_version_backfill.sql` и
`docs/TENANCY/sql/40.16_progress_version_indexes_concurrently.sql` написаны и **не запускались** —
ни против настоящей `learning`, ни против одноразовой. Порядок и предусловия — в
`docs/DONT_FORGET.md`, раздел «Блок 40.16».

В отличие от 40.10–40.13, окна с невидимыми данными между шагами **нет**: по `LessonVersionId` не
фильтрует ни query filter, ни RLS-политика. Пока бэкфилл не прогнан, исторические попытки видны в
корзине `unversionedAttempts` — это и есть способ проверить, что он ещё не выполнялся.

---

## 40.19 — параметризация контента и `banned_claims`: чеклист для человека

Как и 40.14/40.16, это чеклист, а не тесты: писать новые тесты запрещено правилом №3
(`docs/DONT_FORGET.md`). Список того, чего не покрыто, — там же, в разделе «Тесты, которых нет»,
и он длинный. Ниже — то, что человек может проверить за один заход.

### Шаг 1 — что прогоняется автоматически (секунды, ничего не поднимая)

```bash
dotnet build src/backend/Sellevate.sln
dotnet test src/backend/learning-service/Learning.Tests/Sellevate.Learning.Tests.csproj --filter "TestCategory!=Integration"
dotnet test src/backend/ai-service/Ai.Tests/Sellevate.Ai.Tests.csproj            --filter "TestCategory!=Integration"
dotnet test src/backend/organization-service/Organization.Tests/Sellevate.Organization.Tests.csproj --filter "TestCategory!=Integration"
python3 scripts/tenancy-boundary-lint.py
python3 scripts/tenancy-pool-lint.py
( cd src/frontend && npx tsc --noEmit )
```

Цифры прогона 2026-08-18: learning 67/67, ai 170/170, organization 36/36, BuildingBlocks 114/118
(4 `Skipped` — интеграционные RLS-тесты без локального Postgres), фронт `vitest` 353/353 в 49 файлах,
оба линта чистые, `tsc --noEmit` без ошибок, сборка решения 0 ошибок. **Ни один из этих тестов не проверяет ничего из 40.19** — они гарантируют только,
что блок ничего не сломал. Это важно не перепутать: зелёный прогон здесь не есть приёмка блока.

Линт границ тенанта заслуживает отдельного внимания: у сидера появилось поле `target`, приходящее из
тела запроса. Он **не** организация и не должен ей стать; `tenancy-boundary-lint.py` ловит именно
попытку прочитать организацию из body/query/route, и его чистый прогон — доказательство, что новое
поле не является таким чтением.

### Шаг 2 — главная проверка блока: `ContentHash` не зависит от организации

Это то же место, что стоит первым в списке «Тесты, которых нет», и проверять его надо руками, потому
что регрессия здесь **абсолютно молчаливая** — она обнаружится через месяцы, когда кто-то заметит,
что улучшения базовых уроков не доходят до заказчиков.

1. Взять базовый урок (`OrganizationId IS NULL`) и вписать в текст упражнения
   `{{organization.product}}`.
2. Организации A задать профиль с `product = "Кредит Плюс"`, организации B — с
   `product = "СтройБаза"` (`PUT /organizations/profile`, каждый раз со своим `X-Organization-Id`).
3. Опубликовать версию урока из контекста A: `POST /admin/lessons/{id}/versions/publish`.
   Запомнить `ContentHash`.
4. Ещё раз опубликовать из контекста B. Ответ должен быть `createdNewVersion: false` — содержимое
   не изменилось, потому что снимок хранит **шаблон**, а не отрендеренный текст.
5. Прочитать `LessonVersions` (или `GET /admin/lessons/{id}/versions`) и убедиться, что версия
   **одна**, и в её `Content` лежит `{{organization.product}}`, а не название продукта.

Если на шаге 4 появилась вторая версия или хеш отличается — рендер уехал на путь записи, и общая
библиотека уже начала форкаться по заказчикам.

Симметричная проверка в ai-service: `DialogMode.BaseContentHash` у режима с `{{organization.*}}` в
`ChatSystemPrompt` должен быть одинаков независимо от того, из какой организации его прочитали.
Иначе очередь stale (40.18) начнёт считать устаревшими все override'ы навсегда.

### Шаг 3 — подстановка и нейтральный fallback

1. Организации без профиля открыть урок с `{{organization.product}}` →
   `GET /lessons/{id}/exercises`. В тексте должно быть **«ваш продукт»**, а не пустое место и не
   `{{organization.product}}`.
2. Заполнить профиль → тот же запрос отдаёт название продукта. (Между сохранением и изменением текста
   проходит время доставки Kafka — секунды. Если текст не поменялся сразу, это не баг.)
3. Вписать в урок заведомую опечатку `{{organization.produkt}}` → в ответе на её месте **ничего**, а
   в логе learning-service — `Unresolved organization placeholders in learning content: organization.produkt`.
4. Проверить, что скрытые режимы диалога не сломались: практика звонка по компании
   (`company-call`) и кастомный сценарий по-прежнему работают. Их промпты дописываются
   плейсхолдерами, которые подставляет код, и рендерер обязан пропускать всё вне пространства
   `organization.` насквозь.

### Шаг 4 — грейдер видит то же, что видел продавец

Единственная проверка блока, у которой регрессия **громкая для пользователя и невидимая в логах**:
правильный ответ помечается неверным.

1. Упражнение `choose_option`, в тексте опции — `{{organization.product}}`, профиль заполнен.
2. Продавцом открыть упражнение и ответить правильной опцией.
3. Ответ должен быть зачтён. Если он помечен неверным — рендер не доехал до
   `SubmitExerciseAnswerAsync`, и стратегия сравнивает текст опции с текстом, которого у неё нет.

### Шаг 5 — `banned_claims` в обоих промптах

Половина, которая реально защищает заказчика, — это грейдер, а не персона. Проверять надо обе.

1. В профиле задать `bannedClaims: ["мы гарантируем доходность 20% в год"]`.
2. Начать ролевой диалог и **самому произнести** эту фразу, попросив персону подтвердить.
   Персона не должна её повторить или подтвердить — она уклоняется или переспрашивает.
3. Завершить диалог и прочитать обратную связь. Она должна **снизить оценку** и прямо назвать
   произнесённое нарушением, а не похвалить за уверенность.
4. То же для learning-service: упражнение с ИИ-оценкой (`free_text`), в ответе — запрещённое
   обещание. Оценка не должна быть высокой, и в фидбеке должно быть названо нарушение.

Пункт 3 — самый важный. Регрессия, при которой блок пропал только из промпта оценки, молчаливая и
активно вредная: персона молчит, а система продолжает учить продавца ровно запрещённому.

5. Контрольная половина: организация с **пустым** профилем. Промпт должен остаться байт-в-байт таким,
   каким был до 40.19, — никакого пустого блока «ЗАПРЕЩЁННЫЕ УТВЕРЖДЕНИЯ» и никакого блока
   «ДАННЫЕ ОБ ОРГАНИЗАЦИИ».

### Шаг 6 — сидер сеет только глобальную библиотеку

1. `POST /admin/seeder/bundle` **без** поля `target` → `400` с текстом про `target must be 'global'`.
   То же с `target=whatever` и с `target=<guid>`.
2. С `target=global` — импорт проходит как раньше.
3. Главная проверка, и она про молчаливый баг, который блок чинил: у организации создать override
   урока (`POST /admin/content/overrides/lesson/{baseId}`), поменять в нём текст, затем **повторно**
   прогнать тот же бандл платформенным админом, у которого в токене есть эта организация. Текст
   override'а обязан остаться изменённым. До 40.19 он молча возвращался к базовому.
4. `GET /admin/seeder/bundle/export` не должен содержать ни одной строки этой организации.

### Шаг 7 — реплики профиля и операционные скрипты

`docs/TENANCY/sql/40.19_organization_profile_verify.sql` — только чтение, **против настоящей БД не
выполнялся**. Один файл на три базы (`organization`, `learning`, `ai`); секции, не относящиеся к
текущей базе, пропускают себя с `NOTICE`.

Что он отвечает:

- секция 2b — политика на `OrganizationProfileReplicas` обычное равенство, а **не**
  `IS NULL OR = current`. В `ai` это первая не-контентная таблица, соседи законно используют
  контентную политику, поэтому ошибка «скопировал соседнюю» — живая;
- секция 3 — как библиотека разбита на глобальную и организационную часть, и есть ли
  организационные уроки без родителя (их быть не должно ни одного);
- секции 4 и 5 — какие профили существуют в `organization` и какие доехали до реплик. **Разница между
  этими двумя списками и есть ручной шаг раскатки:** профиль, сохранённый до выката 40.19, никогда не
  публиковался, поэтому его реплик нет и `banned_claims` у него не применяются вообще. Лечится одним
  пересохранением. Подробности — `docs/DONT_FORGET.md`, раздел «Блок 40.19».

---

## 40.22 — порог завершения задания: чеклист для человека

Как и 40.14/40.16/40.19, это чеклист, а не тесты: писать новые тесты запрещено правилом №3
(`docs/DONT_FORGET.md`). Список непокрытого — там же, в разделе «Тесты, которых нет»; для 40.22 он
длиннее обычного и открывается идемпотентностью `AttemptCount`.

**Особенность приёмки этого блока: проверить его целиком сегодня нельзя.** Строки
`AssignmentProgressRecords` никто не создаёт до 40.23, а оценка порога умеет только **менять**
существующие. Поэтому чеклист разделён: шаги 1–3 проверяются прямо сейчас, шаг 4 — единственный
способ увидеть блок в работе до 40.23, и он требует ручной вставки строки прогресса в тестовой БД.

### Шаг 1 — что прогоняется автоматически (секунды, ничего не поднимая)

```bash
dotnet build src/backend/Sellevate.sln
dotnet test src/backend/learning-service/Learning.Tests/Sellevate.Learning.Tests.csproj      --filter "TestCategory!=Integration"
dotnet test src/backend/ai-service/Ai.Tests/Sellevate.Ai.Tests.csproj                        --filter "TestCategory!=Integration"
dotnet test src/backend/gamification-service/Gamification.Tests/Sellevate.Gamification.Tests.csproj --filter "TestCategory!=Integration"
dotnet test src/backend/building-blocks/BuildingBlocks.Tests/Sellevate.BuildingBlocks.Tests.csproj  --filter "TestCategory!=Integration"
python3 scripts/tenancy-boundary-lint.py
python3 scripts/tenancy-pool-lint.py
```

Цифры прогона 2026-08-18: learning 67/67, ai 170/170, gamification 53/53, BuildingBlocks 114/118
(4 `Skipped` — интеграционные RLS-тесты без локального Postgres), оба линта чистые, сборка решения
0 ошибок. **Ни один из этих тестов не проверяет логику 40.22** — единственный тест, который блока
касается, это расширенный `EventContractCatalogTests.DialogEvaluated_AiProducer_MatchesGamification
Consumer`, и он проверяет только форму JSON. Зелёный прогон здесь не есть приёмка блока.

### Шаг 2 — словарь правила завершения (только API, ничего поднимать сверх сервиса не нужно)

Все запросы — `POST /admin/assignments` под ролью админа организации. Ожидаемый ответ в скобках.

1. `completionRule` отсутствует → **400**.
2. `"completionRule": {"kind":"opened_everything"}` → **400**, в тексте перечислены известные виды.
   Это главный шов блока: неизвестный вид **отклоняется**, а не хранится «на будущее».
3. `{"kind":"dialog_score","minimumScore":0,"requiredCount":3}` → **400**. Нулевой порог проходит и
   CHECK-ограничение БД, и любую проверку «поле есть» — он и есть провал `ASSIGNMENTS.md` §1.1,
   надевший дискриминатор.
4. `{"kind":"dialog_score","minimumScore":101,...}` → 400; `requiredCount: 0` → 400;
   `requiredCount: 21` → 400.
5. `{"kind":"exercise_accuracy","minimumAccuracyPercent":80}` → **200**, и то же для
   `{"kind":"dialog_score","minimumScore":70,"requiredCount":3}`.
6. Создать черновик с правилом `exercise_accuracy`, положить в `content` **только**
   `dialog_scenario`, вызвать `POST /activate` → **409** с сообщением про несовпадение. Это
   последний момент, когда рассогласование ещё можно исправить: выдача замораживает и правило, и
   содержимое.
7. На **активном** задании `PUT` с изменённым `completionRule` → **409** с именем поля (проверка
   40.21, но именно она защищает уже записанные `BestScore` от смены порога задним числом).

### Шаг 3 — схема и данные в БД (read-only)

```bash
psql -v ON_ERROR_STOP=1 -d learning -f docs/TENANCY/sql/40.22_completion_threshold_verify.sql
```

Файл **только читает**, безопасен на проде с поднятым сервисом, **против настоящей БД не
выполнялся**. Что он отвечает:

- секция 1 — таблица `UserDialogScores`, оба индекса, три CHECK; и отдельно то, что индекс
  `IX_UserDialogScores_OrganizationId_UserId_SessionId` **именно уникальный**. Уникальность здесь не
  оптимизация: без неё повторная доставка `dialog.evaluated` пишет вторую строку, `AttemptCount`
  растёт без единой новой попытки, и человек, пробовавший дважды, читается как пробовавший четырежды;
- секция 2 — RLS включена и forced, политика **обычное равенство**, а не `IS NULL OR = current`.
  Соседи по базе — контентные таблицы, поэтому «скопировал соседнюю политику» здесь живая ошибка, и
  она означала бы, что оценки реальных разговоров одного заказчика видны всем остальным;
- секция 3 — **самая ценная**: у каждого ли задания `completion_rule.kind` входит в словарь 40.22;
  нет ли активного задания, чьё правило измеряет содержимое, которого в нём нет; нет ли оценок вне
  шкалы 0–100 (сюда попадёт сырая десятибалльная оценка, если нормализацию у продюсера сломают);
  нет ли строк прогресса, чей статус противоречит их числам;
- секция 4 — инвентарь. Сразу после раскатки все счётчики нулевые, и это норма: строк прогресса не
  существует. Последний запрос — сколько оценённых разговоров вообще доехало; **ноль здесь при
  ненулевом числе завершённых диалогов в ai-service означает, что консьюмер не работает или уходит в
  dead-letter** — проверьте, что конверты несут организацию (находка 40.14).

### Шаг 4 — увидеть оценку в работе до 40.23 (только на тестовой БД)

Единственный способ, потому что раздача аудитории ещё не написана. **Не делать на проде.**

1. Создать задание с правилом `{"kind":"exercise_accuracy","minimumAccuracyPercent":80}` и одним
   `lesson_version` в содержимом, выдать его (`POST /activate`).
2. Руками вставить одну строку в `AssignmentProgressRecords`: `OrganizationId` — та же, что у
   задания, `AssignmentId`, `UserId` тестового менеджера, `Status = 'not_started'`,
   `AttemptCount = 0`.
3. Пройти под этим менеджером **часть** упражнений урока, отвечая верно. Ожидаемо: строка становится
   `in_progress`, `AttemptCount` равен числу отправок, а `BestScore` остаётся **`NULL`** — оценка не
   выставляется, пока не попробовали каждое упражнение набора. Это то самое место, без которого один
   удачный ответ из двадцати закрывал бы восьмидесятипроцентное задание.
4. Пройти оставшиеся упражнения, часть — неверно. Ожидаемо: `BestScore` = процент верных **отправок**
   (а не «в итоге решённых»), и статус — `completed` при ≥80 или `failed_threshold` при меньшем.
   Проверьте руками: ответить неверно, затем верно на одно упражнение должно дать 50%, а не 100%.
5. Ответить ещё раз хуже. Ожидаемо: у завершённой строки `Status`, `BestScore` и `CompletedAt` **не
   меняются** — планка, взятая однажды, взята.
6. Для `dialog_score`: провести диалог по режиму, указанному в `dialog_scenario`, и убедиться, что в
   `UserDialogScores` появилась ровно одна строка с оценкой 0–100 (а не 0–10). Затем — главное:
   повторно доставить то же событие (проще всего перезапустить консьюмер со сброшенным оффсетом на
   локальном Kafka). **`AttemptCount` обязан остаться прежним.**
7. И проверка границы блока: пройти упражнения из того же урока пользователем, у которого строки
   прогресса **нет**. Строка не должна появиться. Сегодня это единственное, что удерживает границу
   с 40.23 — если она появится, «кто ещё не начал» перестанет быть запросом по существующим строкам.

**Задержка — не баг.** Оценка идёт через Kafka, а не внутри запроса на отправку упражнения: статус
меняется мгновением позже ответа `POST /exercises/{id}/submit`.

---

## 40.23 — выдача, экран менеджера, уведомления: чеклист для человека

Как и 40.14/40.16/40.19/40.22, это чеклист, а не тесты: писать новые тесты запрещено правилом №3
(`docs/DONT_FORGET.md`). Список непокрытого — там же, в разделе «Тесты, которых нет»; для 40.23 он
из двенадцати пунктов и открывается фильтрацией чужого `userId`.

**Хорошая новость этой приёмки: блок наконец проверяется целиком.** Чеклист 40.22 приходилось
разрывать («шаг 4 — единственный способ увидеть блок в работе, и он требует ручной вставки строки
прогресса»), потому что строки прогресса никто не создавал. Теперь их создаёт выдача, поэтому вся
цепочка — выдал → человек увидел → сделал → порог засчитался — проходится через API без вставок
руками. Заодно этим и проверяется 40.22.

### Шаг 1 — что прогоняется автоматически (секунды, ничего не поднимая)

```bash
dotnet build src/backend/Sellevate.sln
dotnet test src/backend/learning-service/Learning.Tests/Sellevate.Learning.Tests.csproj           --filter "TestCategory!=Integration"
dotnet test src/backend/ai-service/Ai.Tests/Sellevate.Ai.Tests.csproj                             --filter "TestCategory!=Integration"
dotnet test src/backend/identity-service/Identity.Tests/Sellevate.Identity.Tests.csproj           --filter "TestCategory!=Integration"
dotnet test src/backend/notification-service/Notification.Tests/Sellevate.Notification.Tests.csproj --filter "TestCategory!=Integration"
python3 scripts/tenancy-boundary-lint.py
python3 scripts/tenancy-pool-lint.py
cd src/frontend && npx tsc --noEmit && npx vitest run
```

Цифры прогона 2026-08-18: learning 67/67, ai 170/170, identity 97/97, notification 56/56, оба линта
чистые, сборка решения 0 ошибок; фронт — `tsc --noEmit` чисто, `vitest` 353/353 в 49 файлах.
**Ни один из этих тестов не проверяет логику 40.23.** Единственное, что блок в них задел, — четыре
конструктора `DialogService` в `CompanyContextDialogTests`, которым добавлен новый аргумент. Зелёный
прогон здесь не есть приёмка блока.

### Шаг 2 — аудитория (только API)

Всё под ролью админа организации; в скобках ожидаемый ответ.

1. Черновик с `"audience": {"kind":"whole_team"}` и непустым `content` → `POST /activate` → **200**.
   Затем `GET /admin/assignments/{id}/progress` → **список из всех сотрудников организации**, все в
   `not_started`. До этого блока здесь всегда было `[]` — это и есть главная проверка.
   Сверьте длину списка с числом активных сотрудников в организации: **это единственное, что нельзя
   проверить запросом к learning-db**, потому что членство там намеренно не хранится.
2. `"audience": {"kind":"users","userIds":[<id сотрудника>, <произвольный чужой uuid>]}` → `activate`
   → **200**, и в прогрессе **только свой сотрудник**. Чужой id молча выброшен — это не небрежность,
   а защита: строка прогресса легла бы в правильную организацию, поэтому проверки изоляции такого
   бы не поймали, а уведомление ушло бы человеку из другой компании.
3. `"audience": {"kind":"users","userIds":[<только чужой uuid>]}` → `activate` → **400** с текстом
   про «никого, кто здесь работает». Задание **остаётся черновиком** — проверьте `GET`, статус
   должен быть `draft`.
4. `"audience": {"kind":"group","groupId":"…"}` → `activate` → **400**. Групп в платформе нет, и
   молчаливое «значит, всей команде» здесь было бы худшим из возможных ответов.
5. **Дозаливка.** У активного задания из п.1 сделайте `PUT` с тем же телом (ничего не меняя) →
   **200**, число строк прогресса **не изменилось**, статусы не сброшены. Затем добавьте в
   организацию сотрудника (принять инвайт) и повторите `PUT` → в прогрессе появилась **одна** новая
   строка `not_started`, остальные нетронуты.
6. **Недоступность identity.** Остановите identity-service, попробуйте `activate` черновика →
   **503** с текстом «список людей не удалось прочитать… ничего не изменено». Проверьте `GET`:
   задание всё ещё `draft`. Это тот самый режим отказа, ради которого выбран синхронный вызов
   вместо реплики — он громкий.

### Шаг 3 — экран менеджера

Войти обычным сотрудником, которому задание выдали, и открыть `/tree`.

1. Наверху появилась полоса с карточкой задания: название, цель, срок и **строка «Чтобы засчиталось:
   …»** с настоящим порогом (а не словом «в процессе»).
2. Войти сотрудником, которому **ничего не выдавали** → полосы нет вообще, дерево навыков выглядит
   ровно как до блока. Это отдельная проверка, а не следствие первой: требование роадмапа — задание
   занимает верх экрана, а не заменяет его.
3. `GET /assignments/active` из-под сотрудника A не показывает задание, выданное только B.
4. Пройти задание до порога → карточка исчезает (`completed` отсеивается). Не дотянуть до порога,
   исчерпав работу → карточка остаётся и **подсвечена янтарным** (`failed_threshold`) — это то
   состояние, ради видимости которого написан 40.22.
5. Задание с `opensAt` в будущем не показывается вообще; бессрочное задание стоит **последним**, а не
   первым (проверка сортировки: `NULL` по умолчанию встал бы в начало).

### Шаг 4 — уведомления

1. Сразу после `activate` — колокольчик у каждого получателя показывает «Вам назначено задание»,
   ссылка ведёт на `/tree?assignment={id}`. Письмо приходит (шаблон общий, отдельного нет).
2. `POST /admin/assignments/{id}/remind` → ответ `{"notifiedCount": N}`, где N — все незавершившие,
   **включая** `failed_threshold`. Уведомление «Напоминание о задании» приходит. Нажать ещё раз →
   приходит **второе** (ключ дедупа содержит момент нажатия), а не проглатывается.
3. **Дедлайн.** Поставить активному заданию дедлайн через ~2 часа и подождать один тик джобы
   (по умолчанию 30 минут; `Assignments__SweepIntervalMinutes` можно уменьшить в dev). Приходит
   «Дедлайн задания приближается»; в БД у задания заполнился `DeadlineNoticeSentAt`. Следующий тик
   **ничего не шлёт**.
4. **Продление.** `PUT` с новым дедлайном → `DeadlineNoticeSentAt` обнулился, и следующий тик шлёт
   уведомление **снова**, с новой датой. Если этого не произошло, продление осталось необъявленным —
   это ровно то, ради чего дата входит в ключ дедупа.
5. Уволить одного получателя (`DELETE /memberships/{userId}`) и дождаться следующего тика по другому
   заданию с дедлайном → **уволенному ничего не приходит**, но его строка прогресса на месте и
   продолжает считаться в воронке как «не начал». Второе — известное следствие, записанное в
   `docs/DONT_FORGET.md`, а не баг.

### Шаг 5 — персона практического диалога

1. Создать задание с `content` из одного `dialog_scenario`, где `reference` — ключ существующего
   режима, и заполненной `persona` (`name`, `position`, `personality`, `difficulty`). Выдать.
2. Получателем начать разговор на этом режиме обычным путём (`/dialog`). Собеседник должен вести
   себя как описанная персона.
3. **Проверить, что персоны нет в браузере:** в ответе `GET /assignments/active` поля персоны
   отсутствовать должны полностью. Это единственное, что отделяет порог от «перепиши собеседника и
   получи 90 баллов».
4. Остановить learning-service и начать разговор → экран практики **открывается**, разговор идёт без
   персоны задания. В логах ai-service — предупреждение «the assignment practice context … could not
   be read». Это осознанный fail-open, записанный в `DONT_FORGET.md`.

### Шаг 6 — схема и данные в БД (read-only)

```bash
psql -v ON_ERROR_STOP=1 -d learning -f docs/TENANCY/sql/40.23_assignment_fanout_verify.sql
```

Файл **только читает**, безопасен на проде с поднятым сервисом, **против настоящей БД не
выполнялся**. Что он отвечает:

- секция 1 — колонка `DeadlineNoticeSentAt` на месте (миграция применилась);
- секция 2 — триггер заморозки 40.21 её **не** упоминает. Если бы упоминал, джоба не смогла бы
  отметить объявленный дедлайн и переуведомляла бы каждые полчаса вечно;
- секция 3 — RLS на обеих таблицах строгая (без `IS NULL`). Проверяется здесь, потому что этот блок
  — первый, который вообще кладёт строки в `AssignmentProgressRecords`;
- секция 4 — организация строки прогресса совпадает с организацией её задания. Расхождение означает,
  что строки писал кто-то мимо сервиса;
- секция 5 — **самая ценная: настоящая воронка по каждому заданию.** До этого блока все четыре числа
  были нулями честно. Если после выдачи они **всё ещё** нули — раздача не отработала: смотрите лог
  learning-service на «the organization roster could not be read» и проверьте
  `IdentityService__BaseUrl`. Там же — активные задания вообще без получателей;
- секция 6 — ни у кого не больше одной строки на задание;
- секция 7 — дедлайны внутри окна, которые ещё не объявлены. Несколько — норма (тик раз в полчаса);
  куча, которая не рассасывается, означает, что джоба не работает **или** ходит под ролью
  `NOBYPASSRLS` и её кросс-тенантное перечисление молча возвращает пусто;
- секция 8 — три топика в outbox: сколько застряло и сходится ли число `assignment.issued` с числом
  строк прогресса. Число сильно **меньше** означает, что кого-то попросили и не сказали.


---

## 40.24 — автоповторы: чеклист для человека

Ручная приёмка блока 40.24. Тестов нет по Правилу №3 (`docs/DONT_FORGET.md`), поэтому это
единственная проверка, которая у блока есть. Требуется поднятая локальная инфра и стартовавший на
новом коде learning-service (миграция `20260818001925_AddAssignmentRepeats`).

**Сократите тик и ускорьте волну**, иначе проверка занимает неделю:

```bash
export Assignments__RepeatSweepIntervalMinutes=1
```

и создавайте задания с расписанием `{"kind":"fixed_offsets","offsetDays":[1]}` — минимальное
допустимое смещение один день. Для проверки «в тот же день» придётся сдвинуть `ActivatedAt` задания
в прошлое **вручную в базе** (`UPDATE` по активной строке разрешён триггером: `ActivatedAt` заморожен,
поэтому это единственный шаг чеклиста, который **нельзя** сделать на проде — только на dev-базе).

### Шаг 1 — расписание принимается и отвергается там, где должно

1. `POST /admin/assignments` с `repeatSchedule: {"kind":"fixed_offsets"}` → создаётся, и
   `GET` возвращает его как есть. Отсутствующий `offsetDays` означает ровно `[7, 21]` — проверить
   косвенно на шаге 2, ничего в базу не дописывается.
2. Каждое из этих тел — **400** с внятным текстом: `{"kind":"weekly"}`,
   `{"kind":"fixed_offsets","offsetDays":[]}`, `[21,7]` (не по возрастанию), `[7,7]` (повтор),
   `[0]`, `[365]`, список из пяти чисел.
3. `PUT` на **активном** задании с изменённым `repeatSchedule` → **проходит** (это единственный путь
   отмены повторов). `PUT` на **закрытом** → `409`.

### Шаг 2 — волна выходит, один раз

1. Создать задание с содержимым из `lesson_version` + `reference_material` + `dialog_scenario`,
   правилом `{"kind":"dialog_score","minimumScore":70,"requiredCount":3}`, дедлайном через 5 дней,
   аудиторией `whole_team`, расписанием `[1]`. Выдать (`activate`). Убедиться, что строки прогресса
   созданы.
2. Сдвинуть `ActivatedAt` на сутки назад (dev-база) и дождаться тика.
3. `GET /admin/assignments` → появилась **вторая** строка: заголовок `«… — повтор 1»`,
   `repeatOfAssignmentId` = id оригинала, `repeatWaveIndex` = 1, `createdBy` = `null`,
   `status` = `active`, `hasRepeatSchedule` = `false`.
4. **Ключевое:** дождаться ещё двух тиков → третьей строки **не появляется**. Если появилась,
   идемпотентность сломана, и это самая дорогая регрессия блока — сорок человек получают два
   одинаковых задания в одно утро.
5. Открыть повтор: `reference_material` **отсутствует**, `lesson_version` и `dialog_scenario` на
   месте, `completionRule.requiredCount` = **2** (три пополам вверх), `minimumScore` = **70**
   (не изменился), `deadline` ≈ сейчас + 5 дней, `opensAt` = `null`,
   `audience` = `{"kind":"users","userIds":[…]}` с теми же людьми.

### Шаг 3 — когорта

1. Один из получателей провалил порог (`failed_threshold`), другой не начал → **оба** получают
   повтор. Это осознанное решение, а не недосмотр.
2. Уволить одного получателя (`DELETE /memberships/{userId}`) до волны → повтор ему **не** приходит,
   строка прогресса в оригинале остаётся.
3. Нанять нового человека после выдачи оригинала → в повтор он **не** попадает. Обратная ловушка к
   40.23: там опасно было не дописать, здесь — дописать.
4. Уволить **всех** получателей → волна не создаётся вовсе, в логе `has nobody left who still works here`.

### Шаг 4 — закрытие, отмена и опоздание

1. Закрыть оригинал (`POST /close`) **до** волны → волна всё равно выходит. Это решение
   (`docs/DECISIONS.md`), а не баг.
2. Очистить `repeatSchedule` у **активного** оригинала → волна не выходит вообще. Урезать `[7,21]`
   до `[7]` после первой волны → вторая не выходит.
3. Сдвинуть `ActivatedAt` так, чтобы волна оказалась просрочена на 5 дней (при
   `RepeatCatchUpDays` = 3) → волна **не** выходит никогда, в логе `too long ago to issue now`.
4. Повтор не порождает повтора: у волны `repeatSchedule` = `null`, и сколько бы тиков ни прошло,
   третьей строки от неё не появляется.

### Шаг 5 — уведомления

1. Каждому получателю волны приходит обычное «Вам назначено задание» — **нового типа нет**, и это
   решение: повтор читается получателем так же, как выдача.
2. Ссылка ведёт на `/tree?assignment={id}` **волны**, не оригинала. На экране менеджера видны обе
   карточки, если оригинал ещё не завершён.

### Шаг 6 — схема и данные в БД (read-only)

```bash
psql -v ON_ERROR_STOP=1 -d learning -f docs/TENANCY/sql/40.24_assignment_repeats_verify.sql
```

Файл **только читает**, безопасен на проде с поднятым сервисом, **против настоящей БД не
выполнялся**. Что он отвечает:

- секции 1–3 — обе колонки, уникальный частичный индекс (`indisunique`, `indisvalid`) и три `CHECK`
  на месте. Индекс — не производительность, а единственное, что стоит между гонкой двух тиков и
  двумя одинаковыми повторами;
- секция 4 — триггер заморозки упоминает колонки серии и **не** упоминает `RepeatSchedule`. Второе
  важно: заморозка расписания отняла бы единственный путь отмены;
- секция 5 — серия не глубже одного уровня, повтор и оригинал в одной организации, у каждой волны
  есть получатели;
- секция 6 — **самая ценная: воронка по волнам.** Та же когорта, та же планка, две-три недели
  спустя. Падение доли завершивших между волной 0 и волной 1 — это и есть выветривание, измеренное,
  а не заявленное. Там же — просроченные и невыданные волны: несколько минут просрочки норма (тик
  раз в час), куча, которая не рассасывается, означает, что джоба не работает **или** ходит под
  ролью `NOBYPASSRLS` и её перечисление молча возвращает пусто;
- секция 7 — сходится ли число `assignment.issued` с числом строк прогресса волн.

---

## 40.25 — дашборд РОПа и двусторонняя связь: чеклист для человека

Ручная приёмка блока 40.25. Тестов нет по Правилу №3 (`docs/DONT_FORGET.md`), поэтому это
единственная проверка, которая у блока есть. Требуется поднятая локальная инфра, стартовавший на
новом коде learning-service (миграция `20260818005249_AddDialogReviewNotes`), ai-service и **гейтвей**
— последний важнее обычного, см. шаг 0.

Экрана РОПа нет (это 40.20), поэтому все админские шаги делаются через API: токен с ролью
организации-администратора и заголовок организации, который выставляет гейтвей.

### Шаг 0 — маршруты гейтвея (тридцать секунд, и это самый ценный шаг чеклиста)

`/assignments/*` и `/admin/assignments/*` не были прописаны в гейтвее с 40.21 по 40.23
включительно, то есть экран менеджера из 40.23 отдавал 404 в любом окружении, где запросы идут
через гейтвей. 40.25 это чинит, и убедиться надо именно **через гейтвей** (порт 5001), а не в
learning-service напрямую:

```bash
curl -s -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer $TOKEN" \
  http://localhost:5001/assignments/active
```

Ожидается `200` (пустой массив — нормальный ответ). `404` означает, что маршрут не подхватился.
То же самое для `/admin/assignments`, `/admin/team/skill-map`, `/admin/dialog-reviews`,
`/dialog-reviews` и `/admin/dialog-sessions` — каждый должен отвечать чем угодно, кроме 404.

### Шаг 1 — что прогоняется автоматически (секунды, ничего не поднимая)

```bash
dotnet build src/backend/Sellevate.sln
dotnet test src/backend/learning-service/Learning.Tests/Sellevate.Learning.Tests.csproj --filter "TestCategory!=Integration"
dotnet test src/backend/ai-service/Ai.Tests/Sellevate.Ai.Tests.csproj --filter "TestCategory!=Integration"
dotnet test src/backend/analytics-service/Analytics.Tests/Sellevate.Analytics.Tests.csproj --filter "TestCategory!=Integration"
dotnet test src/backend/notification-service/Notification.Tests/Sellevate.Notification.Tests.csproj --filter "TestCategory!=Integration"
dotnet test src/backend/gateway/Gateway.Tests/Sellevate.Gateway.Tests.csproj --filter "TestCategory!=Integration"
python3 scripts/tenancy-boundary-lint.py
python3 scripts/tenancy-pool-lint.py
cd src/frontend && npx tsc --noEmit && npx vitest run
```

Из этого блока напрямую не проверяется **ничего** — все тесты существовавшие. Единственный, который
действительно что-то говорит о 40.25, — это `AiTenancyModelTests`: он рефлексией обходит все методы
`IDialogSessionRepository` и требует, чтобы каждый бросал при незаданном тенанте, поэтому два новых
метода прошли автоматически; и он же грепом по исходникам проверяет, что держатель коллекции
`dialog_sessions` по-прежнему **один**.

### Шаг 2 — воронка задания

Нужно активное задание с несколькими получателями (как его создать — шаги 1–2 чеклиста 40.23).

1. `GET /admin/assignments/{id}/dashboard` сразу после выдачи → `funnel.assignedCount` равен числу
   получателей, `notStartedCount` равен ему же, остальные нули; `rows` содержит всех, `series`
   содержит **ровно один** элемент с `waveIndex: 0` — само задание.
2. Одним из получателей выполнить работу так, чтобы порог **не** был достигнут (например, для
   `dialog_score` провести нужное число разговоров с низкими оценками) → в дашборде у него
   `failed_threshold`, и он **первый** в `rows`. Это главное продуктовое утверждение блока: экран
   открывается на том, с кем надо что-то делать.
3. Проверить, что `startedCount` вырос, а `completedCount` нет, и что `failedThresholdCount`
   считается **отдельно**, а не входит в `completedCount`.
4. Уволить (деактивировать membership) одного из получателей и повторить запрос →
   `leftOrganizationCount: 1`, `assignedActiveCount` на единицу меньше `assignedCount`, строка
   человека **осталась** с `isActiveMember: false`. Это та дыра, которую 40.23 оставил явно.
5. **Погасить identity-service и повторить запрос.** Дашборд обязан отдаться: `rosterKnown: false`,
   все `isActiveMember`, `leftOrganizationCount` и `assignedActiveCount` — `null`, воронка на месте.
   Если вместо этого прилетел 503 — регрессия: чтение ростера здесь fail-open, в отличие от
   резолвера аудитории при выдаче.
6. Если у задания есть повторы (40.24), открыть **волну** → `series` тот же, что при открытии
   оригинала, с воронкой у каждой волны. Сравнение волн и есть смысл всей затеи.

### Шаг 3 — тепловая карта

1. `GET /admin/team/skill-map` на свежей организации → пустые `stages`/`skills`, `members` из
   ростера, у всех нули. Пустая карта — нормальный ответ, а не ошибка.
2. Дать одному человеку решить **меньше пяти** упражнений одного навыка → ячейка есть,
   `attemptCount` верный, `accuracyPercent: null`. Ноль вместо `null` — регрессия: два верных ответа
   из двух это 100% и это факт ни о ком.
3. Довести до пяти и более → появился процент; `weakestStageKey` указывает на этап с наименьшим
   **непустым** процентом.
4. Сверить `stages[].key` с `GET /admin/skill-stages`: словарь этапов должен быть тем же самым, а не
   вторым. Этап, у которого нет строки в `SkillStages`, показывается под своим ключом, а не
   пропадает.
5. `?days=1` → окно сжалось, старые попытки выпали из чисел.

### Шаг 4 — цитаты из диалогов

1. `GET /admin/dialog-sessions` → только **оценённые** разговоры организации, свежие первыми.
   Брошенная сессия (без обратной связи) в списке появляться не должна.
2. `GET /admin/dialog-sessions?maxScore=4` → остались только те, где оценка ≤ 4. Это тот самый
   список, ради которого экран открывают в понедельник.
3. `GET /admin/dialog-sessions/{id}` → полный транскрипт, у каждого сообщения есть `index`.
4. **Проверка изоляции:** взять `sessionId` из другой организации (из dev-базы) и запросить его →
   404. Mongo не имеет RLS, поэтому этот шаг проверяет прикладной фильтр, а не сеть безопасности.

### Шаг 5 — комментарий РОПа и оспаривание

1. `POST /admin/dialog-reviews` с `sessionId` оценённого разговора, `quotedText` и `comment` → 201/200,
   в ответе `subjectUserId` — **владелец разговора**, а не тот, кого назвали в теле (в теле его и
   назвать негде). У менеджера в `GET /dialog-reviews` появилась строка; пришло уведомление
   «РОП прокомментировал ваш разговор», и в теле уведомления **сама цитата**, а не «у вас есть
   комментарий».
2. Тот же запрос без `quotedText` → 400. Комментарий без реплик — это заметка, которую через месяц
   никто не прочитает.
3. `POST /admin/dialog-reviews` с `sessionId`, которого нет / из другой организации → 400 с текстом
   «нет оценённого разговора с таким идентификатором».
4. Менеджером: `POST /dialog-reviews/disputes` на **свой** разговор → создалось, `disputedScore`
   равен оценке ИИ на этот момент. Повторно тот же → **400** (одно открытое оспаривание на разговор).
5. Менеджером: то же на **чужой** разговор → 400, и текст должен быть **тем же самым**, что для
   несуществующего id. Различимый отказ превращает эндпоинт в зонд для чужих session id.
6. РОПом: `GET /admin/dialog-reviews?kind=score_dispute&status=open` → жалоба в очереди.
   `POST /admin/dialog-reviews/{id}/resolve` с `{"outcome":"rejected"}` без `resolution` → 400;
   с текстом → проходит. С `{"outcome":"upheld","adjustedScore":85}` → проходит; с
   `{"outcome":"rejected","adjustedScore":85}` → 400.
7. После вердикта менеджеру пришло уведомление, и в нём **назван исход**, а не «рассмотрено».
8. **Проверить, что оценка не поменялась:** `UserDialogScores.Score` и статус выполнения задания
   после удовлетворённого оспаривания те же, что были. Это осознанно (см. `docs/DONT_FORGET.md`),
   и если однажды окажется, что поменялись, — это регрессия, а не улучшение.
9. Менеджером: `POST /dialog-reviews/{id}/acknowledge` на свой комментарий → `acknowledged`, второй
   раз — то же самое без ошибки. На чужой — 404.

### Шаг 6 — метрики

`curl http://localhost:5005/metrics | grep app_assignment`

- `app_assignments_issued_total` растёт на число получателей при каждой выдаче;
- `app_assignment_progress_total{state="..."}` растёт **только при смене статуса**. Переоценка,
  меняющая один `attemptCount`, метрику двигать не должна;
- ни у одной из двух метрик **нет** метки организации. Если она там появилась — это регрессия,
  описанная в `docs/ANALYTICS_SERVICE.md` дважды.

### Шаг 7 — схема и данные в БД (read-only)

```bash
psql -v ON_ERROR_STOP=1 -d learning -f docs/TENANCY/sql/40.25_dialog_reviews_verify.sql
```

Файл **только читает**, безопасен на проде с поднятым сервисом, **против настоящей БД не
выполнялся**. Что он отвечает:

- секции 1–2 — таблица есть, RLS включена и `FORCE`, политика — обычное равенство без ветки
  `IS NULL`. Ветка `IS NULL` здесь означала бы, что спор одного заказчика об оценке виден всем
  остальным;
- секция 3 — восемь `CHECK`, и главный из них — `CK_DialogReviewNotes_Status`: комментарий не может
  быть «удовлетворён», а оспаривание не закрывается прочтением;
- секция 4 — частичный уникальный индекс валиден;
- секция 5 — что вообще лежит в таблице, и **сколько времени висят открытые оспаривания**. Это
  число говорит, работает механизм или превратился в место, куда жалобы уходят умирать;
- **секция 6 — размеченный датасет**, ради которого роадмап механизм оспаривания и вводит: одна
  строка на каждый вердикт, плюс сводка «с какими сценариями оценки спорят чаще всего и как часто
  оказываются правы». Транскриптов там намеренно нет — они в Mongo, и решение об их выгрузке
  (согласие, хранение) не должно приниматься побочным эффектом запуска проверочного скрипта;
- секция 7 — сходятся ли строки разбора с оценками: пустой результат означает, что ни одна строка
  не была записана в обход `DialogReviewService`.

---

## 40.26 — непрохождение как рабочий сценарий: чеклист для человека

Ручная приёмка блока 40.26. Тестов нет по Правилу №3 (`docs/DONT_FORGET.md`), поэтому это
единственная проверка блока. Отличие от 40.21–40.25: **проверять нечего в схеме** — миграции у
блока нет вообще. Всё, что можно проверить, — это поведение свипа и двух новых уведомлений.

### Шаг 0 — порядок выката

Поднять **identity-service на новом коде не позже learning-service**. Если наоборот, learning
увидит ответ `/internal/memberships/active` без поля `administratorUserIds` и **пропустит
организацию целиком на один тик** (ни дайджеста, ни напоминаний менеджерам, ничего не отмечено).
Это не поломка, а осознанное поведение — следующий тик через 30 минут подберёт. В `docker compose`
сервисы поднимаются вместе, так что шаг сводится к «не выкатывать learning в одиночку».

### Шаг 1 — расширенный внутренний маршрут

```bash
curl -s -H "X-Internal-Service-Secret: $INTERNAL_SECRET" \
     -H "X-Organization-Id: <ORG_A>" \
     http://localhost:5002/internal/memberships/active | jq
```

- в ответе **два** массива: `userIds` и `administratorUserIds`;
- `administratorUserIds` — строгое подмножество `userIds`;
- в нём нет ни одного обычного менеджера (сверить с `Memberships.Role` в identity-db: 1 и 2 — да,
  0 — нет);
- деактивированного участника нет **ни в одном** из двух списков;
- без заголовка секрета — отказ; без `X-Organization-Id` — отказ. Это тот же маршрут, что и в
  40.23, и он до сих пор не покрыт ни одним тестом.

### Шаг 2 — дайджест РОПу за день до дедлайна

Подготовка: организацией A завести задание с дедлайном **через ~20 часов** (внутри окна
`Assignments__DeadlineNoticeLeadHours`, по умолчанию 24), выдать его команде, и **не открывать его**
хотя бы одним менеджером.

Ждать до тика свипа (`Assignments__SweepIntervalMinutes`, по умолчанию 30 минут) либо перезапустить
learning-service — свип отрабатывает и на старте.

1. У каждого администратора организации A в `GET /notifications` появилось уведомление типа
   `AssignmentDeadlineDigest` с заголовком «Завтра дедлайн, а команда не начала».
2. **В теле перечислены фамилии** тех, кто не начал (до пяти), и настоящий итог рядом («и ещё N»),
   а не только число.
3. `actionUrl` = `/admin/assignments/{id}?action=remind&scope=not_started`. **Сегодня эта ссылка во
   фронте отдаёт 404** — экран РОПа это 40.20. Это ожидаемо и записано в `docs/DONT_FORGET.md`.
4. У менеджеров, не завершивших задание, отдельно пришло `AssignmentDeadlineApproaching` — это
   уведомление 40.23, оно не должно было измениться.
5. Уволенный (деактивированная membership) **не получил ничего** и **не попал в список имён**
   дайджеста.
6. Повторный тик через 30 минут **не присылает второй дайджест**: `Assignments.DeadlineNoticeSentAt`
   уже проставлен.
7. **Перенести дедлайн** (`PUT /admin/assignments/{id}` с новым `deadline`) → колонка сбрасывается,
   и следующий тик присылает и уведомления менеджерам, и дайджест **заново**, с новой датой.

### Шаг 3 — тишина, когда сказать нечего

Задание с дедлайном внутри окна, у которого **все получатели хотя бы начали** (`in_progress`,
`failed_threshold` или `completed`):

- дайджест РОПу **не приходит вообще**;
- `DeadlineNoticeSentAt` тем не менее проставлен (иначе свип возвращался бы к заданию каждые
  полчаса).

Это главная проверка блока. Уведомление «все молодцы» здесь — регрессия, а не улучшение:
`docs/DECISIONS.md` (2026-08-18) объясняет, почему.

### Шаг 4 — напоминание в один клик и его область

```bash
curl -X POST -H "Authorization: Bearer <ROP_TOKEN>" \
  "http://localhost:5001/admin/assignments/<ID>/remind?scope=not_started"
```

1. `notifiedCount` равен числу **не начавших и работающих** — не всем незавершившим и не включая
   уволенных.
2. Без параметра (или `scope=unfinished`) — поведение 40.23: все незавершившие, включая
   `failed_threshold`.
3. `scope=notstarted` (опечатка) → **409**, а не молчаливое расширение адресатов.
4. Уведомления `AssignmentReminder` пришли ровно тем, кого посчитал `notifiedCount`.
5. **Нажать «напомнить» второй раз в течение того же часа** → у менеджера в инбоксе по-прежнему
   **одно** уведомление (ключ дедупликации огрублён до часа в 40.26). API при этом снова вернёт
   ненулевой `notifiedCount` — он считает намерение, а не доставку, и это записано в
   `docs/DONT_FORGET.md`.
6. Остановить identity-service → тот же запрос отдаёт **503** и **ни одного уведомления**. Это
   fail-closed намеренно: письмо бывшему сотруднику отозвать нельзя, а напоминание можно отправить
   через минуту.

### Шаг 5 — пуш об оспаривании (закрытый пункт 40.25)

1. Менеджером оспорить оценку: `POST /dialog-reviews` с `kind=score_dispute`.
2. У каждого администратора организации появилось уведомление `DialogReviewDisputed` с **именем
   менеджера**, оспоренной оценкой и его собственной фразой в теле; `actionUrl` —
   `/admin/dialog-reviews?note={id}` (тоже 404 до 40.20).
3. Если оспаривание подал сам администратор (РОП тоже практикуется) — **себе он уведомления не
   получает**, остальные получают.
4. **Остановить identity-service и подать оспаривание** → оспаривание **создаётся** и видно в
   `GET /admin/dialog-reviews?kind=score_dispute&status=open`, уведомлений нет, ошибки нет. Это
   fail-open намеренно, в отличие от шага 4.6, — и если однажды окажется, что оспаривание в этом
   случае падает, это регрессия.

### Шаг 6 — что видно в БД (read-only)

```bash
psql -v ON_ERROR_STOP=1 -d learning -f docs/TENANCY/sql/40.26_deadline_digest_verify.sql
```

Файл **только читает**, ничего не создаёт, **против настоящей БД не выполнялся**. Он выписывает
предикат самого свипа: секция 1 — какие задания попадут в ближайший тик, секция 2 — кто именно не
начал и сколько их (то есть содержимое будущего дайджеста), секция 3 — что уже объявлено и не
застряло ли что-нибудь, секция 4 — запрос **к identity-db** о том, кому это уйдёт. Пустой результат
секции 4 значит, что у организации нет ни одного администратора: дайджест не уйдёт никому, и это не
ошибка кода, а конфигурация, которую надо чинить руками.

---

## 40.34 — приёмка всей фазы на двух организациях: чеклист для человека

Это финальный чеклист Phase 40, и единственный, который проверяет **границу тенанта**, а не
поведение одного блока. Всё выше — по блокам; здесь фаза проверяется как одна система.

Чеклисты есть не у всех блоков (40.15, 40.17, 40.18, 40.20, 40.21 и весь этап F —
40.27–40.33 — своих разделов не получили). Этот раздел закрывает их тем, что важно на приёмке:
**видит ли организация A хоть что-нибудь, принадлежащее организации B**. Всё остальное — предмет
блочных чеклистов выше и `docs/TENANCY/PHASE_40_SUMMARY.md`.

Ничего из этого не покрыто автотестом. Интеграционные тесты изоляции существуют, но требуют
настоящих Postgres и Mongo и **ни разу не прогонялись** — см. «40.14 → Шаг 4».

### Шаг 0 — подготовка: две организации, четыре человека

Раскатать по `docs/TENANCY/PHASE_40_SUMMARY.md` §2, затем создать через суперадминку платформы:

| | Организация A | Организация B |
|---|---|---|
| Администратор | `admin-a@` | `admin-b@` |
| Менеджер | `user-a@` | `user-b@` |

Дальше в чеклисте `TA` — токен `admin-a@`, `TB` — токен `admin-b@`, `UA` — токен `user-a@`.
Все запросы идут **через гейтвей** (`http://localhost:5001` в профиле Local Dev; в полном Docker-стеке
гейтвей на `5000` — почему они разные, в `docs/LOCAL_DEV.md`), кроме шага 1.2.

**Обе организации должны быть непустыми.** Пустая организация B проходит любой тест изоляции, ничего
не доказывая: «не видно» и «нечего видеть» — разные вещи, и различить их можно, только если у B
есть свои данные. Заполните B тем же набором сущностей, что и A.

### Шаг 1 — заголовок организации нельзя подделать снаружи

Самая дешёвая и самая важная проверка фазы. Занимает минуту.

```bash
# 1.1 Через гейтвей с чужим организационным заголовком — заголовок должен быть срезан,
#     а организация взята из JWT. Ожидается КОНТЕНТ ОРГАНИЗАЦИИ A, не B.
curl -H "Authorization: Bearer $TA" -H "X-Organization-Id: <id организации B>" \
     http://localhost:5001/admin/assignments

# 1.2 Мимо гейтвея, прямо в сервис (learning-service — 5008; полная карта портов
#     в docs/LOCAL_DEV.md, 5002 это identity, а не learning).
#     С машины разработчика ответ придёт: docker-compose биндит сервисные порты на
#     127.0.0.1 намеренно. Проверять надо С ДРУГОЙ МАШИНЫ или из соседнего контейнера —
#     оттуда соединение должно не устанавливаться вообще. Если устанавливается,
#     вся модель изоляции держится на одном гейтвее, а внутренние маршруты
#     (/internal/*, /ai/*) не требуют JWT и берут организацию из заголовка.
curl -H "X-Organization-Id: <id организации B>" http://localhost:5008/admin/assignments
```

1.1 вернуло данные B — **критическая находка, останавливающая приёмку**.
1.2 вернуло что угодно, кроме отказа соединения, — тоже.

### Шаг 2 — этап D: версии, override'ы, профиль

1. **Версия урока не видна чужой организации.** `TA`: `POST /admin/lessons/{id}/versions/draft`,
   затем `POST /admin/lessons/{id}/versions/publish`. `TB`:
   `GET /admin/lessons/{id}/versions` — версия A **не должна** быть в списке.
2. **Прямой запрос по id чужой версии — 404, а не 403 и не тело.** `TB`:
   `GET /admin/lessons/{lessonId}/versions/{versionId-организации-A}`. Ожидается 404. Ответ 403
   тоже приемлем, но **тело версии — находка**: это IDOR.
3. **Опубликованная версия иммутабельна.** Попытаться изменить опубликованную версию A из-под `TA` —
   должна быть отвергнута. Если проходит, ломается вся привязка прогресса из 40.16.
4. **Override — copy-on-write, оригинал цел.** `TA`: `POST /admin/content/overrides/{kind}/{baseId}`,
   изменить текст. Затем `TB`: прочитать тот же базовый урок — он **не изменился**. Это главный тест
   этапа D: если глобальный оригинал поехал, правка одного заказчика уехала всем.
5. **Очередь `stale` не смешивает организации.** Обновить базовый урок, затем `GET
   /admin/content/overrides` из-под `TA` и `TB` — каждая видит только свои расхождения.
6. **Профиль организации подставляется свой.** Заполнить профили A и B **разными** названиями
   компании. Открыть один и тот же урок с плейсхолдером из-под `UA` и из-под менеджера B — текст
   должен отличаться. Одинаковый текст означает, что подстановка не видит профиль.
7. **`banned_claims` организации A не влияет на B.** Запретить в A фразу, которой в B нет; убедиться,
   что упражнения B не изменились.

Read-only сверка в БД:
`docs/TENANCY/sql/40.15_lesson_versioning_verify.sql`,
`40.17_program_versioning_verify.sql`, `40.18_content_overrides_verify.sql`,
`40.19_organization_profile_verify.sql`.

### Шаг 3 — этап E: задания

1. **Аудитория не выходит за организацию.** `TA`: создать задание
   (`POST /admin/assignments`) с аудиторией «вся организация», активировать
   (`POST /admin/assignments/{id}/activate`). Проверить `GET /admin/assignments/{id}/progress`:
   в списке получателей **не должно быть ни одного человека из B**. Это самый вероятный способ
   утечь людьми: аудитория разрешается запросом к identity-service, и её организация приходит из
   контекста вызова.
2. **Чужое задание недоступно по id.** `TB`: `GET /admin/assignments/{id-задания-A}` → 404.
   Тело задания — IDOR.
3. **Дашборд считает только своих.** `GET /admin/assignments/{id}/dashboard` из-под `TA`: числа
   воронки сходятся с числом людей в A и не включают B.
4. **Порог качества, а не факт прохождения.** Пройти задание из-под `UA` **плохо** (оценка ниже
   порога) — статус должен стать `failed_threshold`, **а не** «завершено». Это главное продуктовое
   утверждение этапа E; если оно неверно, задания меряют посещаемость, а не результат.
5. **Напоминание уходит только своим.** `POST /admin/assignments/{id}/remind` из-под `TA` — ни одно
   уведомление не приходит людям B.
6. **Дайджест дедлайна.** Дождаться (или сдвинуть дедлайн) — администраторы A получают дайджест с
   именами не начавших **из A**. Администраторы B не получают ничего.

Read-only сверка: `40.21_assignments_verify.sql`, `40.22_completion_threshold_verify.sql`,
`40.23_assignment_fanout_verify.sql`, `40.24_assignment_repeats_verify.sql`,
`40.25_dialog_reviews_verify.sql`, `40.26_deadline_digest_verify.sql`.

### Шаг 4 — этап F: ИИ в админке

**Это первый живой прогон каждой из этих функций.** Ни один промпт этапа F никогда не отправлялся в
модель, формат ответа проверен только на заглушках. **Первое, что здесь может сломаться, — разбор
ответа модели**, а не изоляция. Ожидайте этого и отличайте одно от другого: ошибка парсинга придёт
как отказ задания с кодом, утечка тенанта — как чужой текст в своём уроке.

> **Прежде чем считать шаг проваленным по изоляции, проверьте таймаут.** Ревью 40.34 нашло
> расхождение, которое нельзя было проверить по коду: общий клиент `OpenAI` в ai-service даёт **30 с
> на попытку и 90 с всего**, тогда как вызывающая сторона объявляет **300 с**
> (`AiService:ContentPipelineTimeoutSeconds`) с комментарием «генерация урока регулярно превышает
> 100 с». Если генерация падает по таймауту, вы увидите отказ задания с сообщением, обвиняющим не тот
> сервис, — а провайдер при этом выставит счёт за три попытки, ни одна из которых не будет учтена
> счётчиком (списание стоит после разбора ответа, до которого путь по таймауту не доходит).
> **Это первое, что стоит воспроизвести на этом шаге**, и подробности — в `docs/DONT_FORGET.md`,
> блок 40.34.

1. **Материал одной организации не попадает в промпт другой.** Загрузить в A и B **разные**
   материалы (`POST /admin/content-generation` → `POST /admin/content-generation/{jobId}/material`).
   Дать свипу отработать. Прочитать структуру и сгенерированный урок каждой организации:
   **в уроке A не должно быть ни одного факта из материала B**. Это критическая проверка этапа F —
   утечка здесь означает, что данные одного клиента ушли в контент другого.
2. **Чекпоинт работает: без утверждения структуры генерации нет.** Создать задание и **не**
   утверждать структуру (`POST /admin/content-generation/{jobId}/approve`). Урок появиться не должен,
   деньги не должны тратиться.
3. **Порог достаточности отвергает мусор.** Загрузить заведомо непригодный материал (пару строк) —
   задание должно быть отвергнуто **с названной причиной** до генерации, а не превратиться в
   бессмысленный урок.
4. **Чужое задание генерации недоступно.** `TB`: `GET /admin/content-generation/{jobId-организации-A}`
   → 404.
5. **Пакетная адаптация ничего не применяет сама.** `POST /admin/content/adaptations` в A, дать
   свипу отработать: элементы должны получить **предложение**, а сам контент — остаться прежним, пока
   человек не нажал `accept`.
6. **Петля «метрика → контент» смотрит на свои метрики.** Провалить упражнение из-под `UA`;
   предложения контента в A должны это отразить, в B — нет.
7. **Квоты считаются по организациям.** `GET /admin/ai-usage` из-под `TA` и `TB` — расход A не
   виден в B и наоборот. Расход A **не нулевой** после шагов 1–6.
8. **Непрайсованная модель приходит с `null`, а не с нулём.** В `GET /admin/ai-usage` строка модели
   без цены в `AiQuotas:PricePerMillionTokens` должна иметь `estimatedCost: null` и
   `hasUnpricedModels: true`. `estimatedCost: 0` — находка: ноль читается как «бесплатно».
9. **Квоту меняет только платформенный администратор.** `PUT /admin/ai-quota` из-под `TA`
   (организационный администратор) должен получить отказ: маршрут объявлен
   `RequirePlatformAdministrator`, в отличие от соседнего `GET /admin/ai-usage`. Если проходит —
   заказчик может поднять себе лимит сам.
10. **Порядок деградации при исчерпании.** Довести организацию до `100% − BatchReservePercent`
    (по умолчанию 90%) — фоновая генерация должна остановиться, **а интерактивный диалог продолжать
    работать** до 100%. Запрос без заголовка `X-Ai-Workload` считается интерактивным.

Read-only сверка: `40.31_skill_gaps_verify.sql`, `40.32_content_adaptation_verify.sql`,
`40.33_ai_quotas_verify.sql`.

### Шаг 5 — новый шов learning → ai (40.33)

Самое молодое место в ветке, и оно на пути обычного пользователя, а не администратора. Проверяется
из-под `UA` в упражнении типа `ai_dialogue`.

1. **Стрим доезжает по частям.** Субтитры должны появляться постепенно. Если реплика возникает разом
   в конце — между сервисами буферизующий прокси. Функционально работает, ощущается сломанным.
2. **Речь играет.** `POST /ai/tts` отдаёт WAV; раньше этот код не отдавался наружу как файл.
3. **Голосовые минуты попали в счётчик.** После разговора `GET /admin/ai-usage` из-под `TA` должен
   показать прирост. **Нулевой прирост — находка блока 40.33 вернулась**: до него речь упражнений
   синтезировалась мимо всех счётчиков.
4. **Таймаут.** Длинный голосовой ход не должен обрываться раньше 90 с
   (`AiService:ChatTimeoutSeconds`).

### Шаг 6 — фоновые задачи не смешивают организации

Единственное место, где утечка выглядит не как чужие данные на экране, а как **чужое имя в письме**.

Дать поработать ночным свипам (или сдвинуть время) и проверить, что ни одно письмо, уведомление и
дайджест организации A не содержит ни одного имени, урока или числа из B. Затрагивает семь задач,
перечисленных в `docs/TENANCY/BACKGROUND_JOBS.md` как `Needs BYPASSRLS = Yes`.

### Шаг 7 — три грепа перед подписью

Те же, что в «40.14 → Шаг 2», с обновлёнными ожиданиями:

```bash
grep -rn --include=*.cs "AddHostedService" src/backend | grep -v /obj/ | grep -v /bin/   # ожидается 30
grep -rn --include=*.cs "IgnoreQueryFilters" src/backend | grep -v /obj/ | grep -v Tests # ожидается 7
grep -rnE "FromSqlRaw|FromSqlInterpolated|ExecuteSqlRaw|ExecuteSqlInterpolated" \
  --include=*.cs src/backend | grep -v /obj/                                             # ожидается 0
```

Плюс три линта, каждый секунду: `scripts/tenancy-boundary-lint.py`, `scripts/tenancy-pool-lint.py`,
`scripts/ai-provider-lint.py`. Все три должны сказать `clean`.

### Что считается провалом приёмки

Останавливают приёмку, а не записываются в бэклог:

- любой ответ, содержащий данные чужой организации (шаги 1, 2.1, 2.2, 3.1, 3.2, 4.1, 4.4, 4.7, 6);
- глобальный урок, изменившийся от правки одной организации (шаг 2.4);
- `PUT /admin/ai-quota`, доступный организационному администратору (шаг 4.9).

Остальное — находки: записывать в `docs/DONT_FORGET.md` и чинить, не откатывая раскатку.
