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

- No service table has `OrganizationId` yet, so there is nothing to point `EnableTenantRls` at in
  a real migration. Add a new row to this document per service the first time it does.
- `TenantConnectionInterceptor` is registered by `AddSellevateTenancy()` but not yet added to any
  service's `DbContext` — that wiring, plus wrapping tenant-scoped reads in an explicit
  transaction (see docs/DECISIONS.md), is Stage C's responsibility, not this block's.
- The role-provisioning SQL (`docs/TENANCY/sql/create_sellevate_app_role.sql`) is written but not
  run anywhere real — see `docs/DONT_FORGET.md`.
