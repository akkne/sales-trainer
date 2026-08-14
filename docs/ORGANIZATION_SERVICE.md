# ORGANIZATION_SERVICE.md — Organization Service scaffold

> Phase 40.5 of [Phase 40 — Multi-tenancy](ROADMAP.md#phase-40--мультитенантность-организации-multi-tenancy)
> (Stage B). New microservice, not an extraction from an existing one: `organization-service`
> owns the tenant registry that every other service will reference by a bare `organization_id`
> (no FK, learned over Kafka — see [TENANCY.md §1.1](TENANCY/TENANCY.md)).

## Bounded context

- **Own** the tenant registry: one row per paying customer (`Organization`), addressable by id,
  with a lifecycle (`Active` / `Suspended`).
- **Own** the per-organization content-substitution profile (`OrganizationProfile`) from
  [CONTENT_MODEL.md §3](TENANCY/CONTENT_MODEL.md#3-the-organization-profile--the-part-that-removes-most-forks) —
  product, ICP, objections, script, tone, glossary, banned claims.
- **Announce** organization lifecycle changes on Kafka (`organization.created` /
  `organization.updated` / `organization.suspended`) so other services can react once they have a
  reason to (none do yet — Stage C, 40.10+).

Explicitly **not** in scope for 40.5: memberships, users, invites, roles (40.6/40.7), the platform
superadmin panel and impersonation (40.9), SSO (40.8, deliberately deferred). `organization-service`
today only manages the registry row and the profile row; nothing yet reads or writes them except
the two controllers below.

## Layout

```
src/backend/organization-service/
  Organization/
    Program.cs                                 service host wiring (Postgres + JWT + Kafka + tenancy)
    Sellevate.Organization.csproj
    Dockerfile                                 build context = src/backend (for building-blocks)
    Common/Constants/                          routes, error messages, health check names
    Eventing/                                  organization.created/updated/suspended contracts
    Features/Organizations/
      Configurations/                          EF entity configurations
      Endpoints/                                OrganizationController, OrganizationProfileController
      Exceptions/                               OrganizationSlugConflictException
      Models/                                   Organization, OrganizationProfile, DTOs
      Services/Abstract|Implementation          IOrganizationService, IOrganizationProfileService
    Infrastructure/Data/                        OrganizationDbContext, DatabaseBootstrapper, migrations
  Organization.Tests/                            NUnit unit tests (EF InMemory, mocked IEventPublisher)
```

## Data ownership — Postgres `organization` (own database, port **5010**)

| Table | Tenant-scoped? | Notes |
|---|---|---|
| `Organizations` | **No** | The tenant registry itself. `Id`, `Name`, `Slug` (globally unique), `Status` (`Active`/`Suspended`), `CreatedAt`, `UpdatedAt`. Never carries an `OrganizationId` column, never gets `EnableTenantRls` — a row here IS an organization, not something belonging to one (docs/TENANCY/TENANCY.md §1.2). Addressed by its own `Id` in the route, which is why it is exempt from the "organization id never in the route" boundary rule (`scripts/tenancy-boundary-lint.py` only forbids a parameter literally named `organizationId`; `{id:guid}` here means "which registry row", not "which tenant is making the request"). |
| `OrganizationProfiles` | **Yes** | `OrganizationId` is both the primary key and the `ITenantScoped.OrganizationId` column — a 1:1 row per organization. Protected by the Stage A write guard (`TenantSaveChangesInterceptor`), the RLS policy (`EnableTenantRls("OrganizationProfiles")`, applied in the initial migration), and an EF query filter (`profile.OrganizationId == _tenantContext.OrganizationId`). Reads go through `OrganizationProfileService.GetProfileAsync`, which wraps the `SELECT` in an explicit transaction so `SET LOCAL app.organization_id` (from `TenantConnectionInterceptor`) has a transaction to scope to, per docs/TENANCY/TENANCY.md §1.5. `objections`/`script`/`glossary`/`bannedClaims` are stored as `jsonb`-typed `string` columns (same convention as `Exercise.SerializedContent`), serialized/deserialized by the service layer. |

**Why the split this way, not the other way:** the registry is deliberately outside the tenant
boundary because addressing it (create/list/suspend an organization) is inherently a cross-tenant,
platform-level operation — there is no "current organization" to scope it to. The profile is
deliberately inside the boundary because it is exactly the kind of per-tenant data RLS exists to
protect: an OrgAdmin from organization A must never be able to read or overwrite organization B's
product/ICP/script through a forgotten filter, a raw query, or `ExecuteUpdate`.

**Local connection role:** the service currently connects with the same Postgres superuser as
every other service (`ConnectionStrings:Postgres`), not the RLS-restricted `sellevate_app` role —
that role's real-server rollout is still pending a human (see `docs/DONT_FORGET.md`). RLS is
therefore wired correctly (`ENABLE`/`FORCE` + `USING`/`WITH CHECK` policy) but not yet the layer
that actually blocks a cross-tenant read locally; the EF query filter and the write guard are what
enforce isolation today. This is the same situation every Stage A component already documented and
is not new to this block.

Migration: `InitialOrganizationSchema` (2026-08-14) creates both tables, the unique index on
`Slug`, and calls `EnableTenantRls("OrganizationProfiles")`.

## Eventing

Produces only (no consumer — like `company-service`'s `company.followup.due`, this service
registers `KafkaTopicProvisioner` + `KafkaEventPublisher` directly rather than the full
`AddSellevateEventing` helper, since it never needs the Redis-backed consumer idempotency store):

| Topic | Payload | When |
|---|---|---|
| `organization.created` | `{organizationId, name, slug}` | `POST /organizations` succeeds |
| `organization.updated` | `{organizationId, name, slug, status}` | `PUT /organizations/{id}` (rename) or `POST /organizations/{id}/reactivate` |
| `organization.suspended` | `{organizationId, name}` | `POST /organizations/{id}/suspend` |

Reactivation intentionally publishes `organization.updated` rather than a fourth topic — the
roadmap names exactly three topics for this block, and "status flipped back to active" is a
special case of "the registry row changed", not a distinct kind of lifecycle event.

## Gateway

YARP cluster `organization` (`http://organization:8080/` in Docker,
`http://localhost:5010/` on the host) routes `/organizations/{**catch-all}` and the bare
`/organizations` root. See [API_CONTRACTS.md](API_CONTRACTS.md#organization-service-phase-405)
for the endpoint list.

## Local dev

`scripts/dev-organization.sh` (port `LOCAL_ORGANIZATION_PORT`, default **5010**), wired through
`scripts/lib-local-env.sh` (`export_organization_env`, and the gateway's
`export_gateway_env` now also points the `organization` cluster at `localhost:5010`). Not
auto-started by `scripts/dev-up.sh` (which only starts infra + frontend, per the Local Dev
profile) — run it alongside `scripts/dev-gateway.sh` and the other per-service scripts, same as
`scripts/dev-company.sh`. See [LOCAL_DEV.md](LOCAL_DEV.md).

## What 40.6 needs to know

- `Organization.Id` is the value that will become the JWT `org_id` claim and the
  `X-Organization-Id` header. Nothing about its shape changes for 40.6 — it is a plain `Guid`.
- The `membership (user_id, organization_id, ...)` table from 40.6 will hold a bare
  `organization_id uuid` with **no** foreign key into this service's database (DB-per-service —
  see docs/TENANCY/TENANCY.md §1.1), the same pattern `identity-service`'s `UserReplica` already
  uses for users elsewhere.
- `organization.created` is the natural place for identity-service to hook the "mint the first
  OrgAdmin membership" flow in 40.9 — no consumer exists yet, this scaffold only produces the
  event.
- `OrganizationController` today has no role restriction beyond `[Authorize]` (any authenticated
  user can create/suspend/reactivate an organization). Locking it to platform `SuperAdmin` is
  explicitly 40.9 scope, once `RequireSuperAdmin` exists in its post-40.6 shape — tracked in
  [DECISIONS.md](DECISIONS.md).
- `OrganizationProfileController` is already `[TenantScoped]` and reads the organization solely
  from `X-Organization-Id`. It will start being reachable through a real user's JWT the moment
  identity-service issues the `org_id` claim in 40.6 — no changes needed on this service's side.
