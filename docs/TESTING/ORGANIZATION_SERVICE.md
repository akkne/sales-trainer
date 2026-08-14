# Testing — Organization Service

Covers Phase 40.5 (Stage B of [multi-tenancy](../ROADMAP.md)): the `organization-service`
scaffold and registry. Feature doc: [docs/ORGANIZATION_SERVICE.md](../ORGANIZATION_SERVICE.md).
Design docs: [TENANCY.md](../TENANCY/TENANCY.md), [CONTENT_MODEL.md](../TENANCY/CONTENT_MODEL.md).

## Automated

### Backend — organization-service (NUnit, EF InMemory provider)

```
cd src/backend
dotnet test organization-service/Organization.Tests/Sellevate.Organization.Tests.csproj
```

`Unit/OrganizationServiceTests.cs` — the tenant registry service (`IOrganizationService`):
create persists + publishes `organization.created` with the correct topic/partition-key/payload,
an explicit slug is normalized (lowercased, non-alphanumerics collapsed to hyphens), a duplicate
slug throws `OrganizationSlugConflictException` and does not publish, a blank name throws
`ArgumentException`, list returns newest-created-first, get/update/suspend/reactivate all return
`null` for an unknown id, update renames + publishes `organization.updated`, suspend flips status
+ publishes `organization.suspended`, reactivate flips status back + publishes
`organization.updated` (not a fourth topic — see docs/DECISIONS.md).

`Unit/OrganizationProfileServiceTests.cs` — the tenant-scoped profile service
(`IOrganizationProfileService`): reading with no tenant context set throws
`InvalidOperationException`, reading an organization with no profile yet returns `null`, first
write creates the row and round-trips `objections`/`scriptStages`/`glossary`/`bannedClaims`
through their `jsonb` JSON-text columns correctly, a second write updates the existing row rather
than creating a duplicate, and — the isolation-relevant case — organization B's
`GetProfileAsync()` never returns organization A's profile even against the same InMemory
database, exercising the EF query filter closed over `ITenantContext`.

`Unit/OrganizationControllerTests.cs` / `Unit/OrganizationProfileControllerTests.cs` — HTTP status
mapping with the service layer mocked (NSubstitute): `201` on create, `409` on slug conflict,
`400` on a validation exception, `404` when the service returns `null`, `200` otherwise; the
profile controller additionally asserts (by reflection) that it carries `[TenantScoped]` and that
its route template has no organization-id segment.

`Unit/OrganizationTenancyScopeTests.cs` — encodes the tenancy design decision as an executable
check: `Organization` (the registry entity) does **not** implement `ITenantScoped`,
`OrganizationProfile` does. Catches an accidental future edit that would apply RLS-style isolation
to the tenant registry itself.

### Backend — BuildingBlocks (event contract + topic catalogue)

```
cd src/backend
dotnet test building-blocks/BuildingBlocks.Tests/Sellevate.BuildingBlocks.Tests.csproj
```

`TopicsCatalogTests.cs` includes `organization.created`/`organization.updated`/
`organization.suspended` in the reflected `Topics.All` set the startup `KafkaTopicProvisioner`
uses. `EventContractCatalogTests.cs` has one wire-format test per organization topic
(`OrganizationCreated_OrganizationProducer_MatchesFutureIdentityConsumer`, etc.) — there is no
consumer yet, so these pin the payload shape a future identity-service consumer (Phase 40.9) will
deserialize against.

### Backend — gateway route flip

```
cd src/backend
dotnet test gateway/Gateway.Tests/Sellevate.Gateway.Tests.csproj
```

`OrganizationRouteFlipTests.cs` (mirrors `CompanyRouteFlipTests.cs`): `/organizations/*` and the
bare `/organizations` route both target the `organization` cluster, the cluster has a destination
configured, and the route is not served by the retired monolith.

### Lint

```
python3 scripts/tenancy-boundary-lint.py src/backend
python3 scripts/tenancy-pool-lint.py src/backend
```

Both must report clean. The boundary lint in particular guards that no organization-service DTO,
`[FromQuery]`/`[FromRoute]` binding, or route template reads the tenant id from anywhere but the
gateway-validated `X-Organization-Id` header — see docs/TENANCY/TENANCY.md §1.3.

## Manual

1. `scripts/dev-infra.sh` (Postgres/Kafka/Loki), then `scripts/dev-organization.sh`.
2. `curl http://localhost:5010/health/live` and `/health/ready` return `200` once Postgres/Kafka
   are reachable (the `organization` database is auto-created + migrated on first start).
3. With a valid JWT: `POST /organizations` with `{"name": "Acme Sales"}` → `201` with a generated
   `acme-sales` slug; repeating the same request with an explicit `slug: "acme-sales"` → `409`.
4. `GET /organizations` lists it; `POST /organizations/{id}/suspend` then
   `GET /organizations/{id}` shows `status: "Suspended"`; `POST /organizations/{id}/reactivate`
   flips it back.
5. `GET /organizations/profile` with no `X-Organization-Id` header → `403` (from
   `TenantContextMiddleware`, before the request reaches the controller). With a manually-set
   `X-Organization-Id: <organization id from step 3>` header → `404` (no profile yet), then
   `PUT /organizations/profile` with a body → `200` and a subsequent `GET` returns it.
   (End-to-end through a real user's JWT is not testable yet — identity-service does not issue the
   `org_id` claim until Phase 40.6.)
6. Kafka UI (`http://localhost:8085`) shows `organization.created` / `organization.updated` /
   `organization.suspended` messages for the steps above.
