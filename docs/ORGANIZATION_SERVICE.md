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
  product, ICP, objections, script, tone, glossary, banned claims. Since 40.29 this service also owns
  the **merge policy** by which a structure extracted from the customer's material is promoted into
  that row, and the vocabulary of questions asked about whatever is still missing — see
  "The profile as an interview" below. It remains the only writer of the row.
- **Announce** organization lifecycle changes on Kafka (`organization.created` /
  `organization.updated` / `organization.suspended`) so other services can react once they have a
  reason to (none do yet — Stage C, 40.10+).
- **Announce** profile changes on Kafka (`organization.profile.updated`, Phase 40.19) so that
  learning-service and ai-service can resolve `{{organization.*}}` placeholders locally instead of
  calling this service on their read paths — see [CONTENT_PARAMETERIZATION.md](CONTENT_PARAMETERIZATION.md) §5.

Explicitly **not** in scope for 40.5: memberships, users, invites, roles (40.6/40.7), the platform
superadmin panel and impersonation (40.9), SSO (40.8, deliberately deferred). `organization-service`
today only manages the registry row and the profile row; nothing yet reads or writes them except
the two controllers below — and, since 40.19, the two replica projections that follow the profile
into learning-db and ai-db.

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
`AddSellevateEventing` helper, since it never needs the Redis-backed consumer idempotency store).
Since Phase 40.9 the first three topics have a consumer on the other side: identity-service projects
them into its `OrganizationReplicas` table so a suspended organization stops producing tokens. Since
40.19 there is a fourth topic, with two consumers.

### `organization.profile.updated` (Phase 40.19)

| Topic | Payload | When |
|---|---|---|
| `organization.profile.updated` | `{organizationId, product, icp, tone, objectionsJson, scriptJson, glossaryJson, bannedClaimsJson, updatedAt}` | any successful write of the profile row: `PUT /organizations/profile`, and since 40.29 `PATCH /organizations/profile` and `POST /organizations/profile/draft/apply` |

Four properties of this event are decisions rather than details.

- **It carries an envelope `organizationId`, unlike the other three.** Those describe the tenant
  *registry* — the organization is their payload, not their context — so identity's consumer opts out
  of `RequiresOrganization`. A profile lives *inside* a tenant the way its lessons do, so this event
  keeps the default and its consumers write under ordinary tenant context with no RLS widening
  anywhere ([BACKGROUND_JOBS.md §4b](TENANCY/BACKGROUND_JOBS.md)).
- **The payload is the whole profile, never a delta.** Its consumers are last-writer-wins replicas: a
  delta would make a dropped message permanent, while a full snapshot makes the next save repair it.
- **The jsonb columns travel as raw JSON text.** Re-modelling objections, script, glossary and banned
  claims into three services' worth of DTOs would give each service its own chance to disagree about
  the shape; `OrganizationProfileSnapshot` in BuildingBlocks parses them once, identically, for
  everybody.
- **It is published after the commit, not inside it.** A replica that learned about a profile the
  transaction then rolled back would render a lesson with text no organization ever saved. The other
  direction — a committed save whose event is lost — is the one the payload is designed for.

Consumers: `OrganizationProfileConsumer` in learning-service and in ai-service, both writing
`OrganizationProfileReplicas`.

**What this service does not do:** there is no republish endpoint and no reconciliation job. A
profile saved before 40.19 shipped has never been published, so its replicas do not exist; the fix is
one manual re-save and it is recorded in [DONT_FORGET.md](DONT_FORGET.md). An endpoint that
republished on demand would be one more platform-only route to authorize for a problem that occurs
exactly once.

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

## Authorization (Phase 40.9)

`OrganizationController` — create / list / get / rename / suspend / reactivate — is
`[Authorize(Policy = AuthorizationPolicies.RequireSuperAdmin)]`. The policy is registered in this
service's own `Program.cs` and mirrors identity-service's definition verbatim (the platform role
travels in the JWT `role` claim, which every service validates directly), so the same token means
the same thing in both.

That gate is load-bearing rather than cosmetic: the registry is the one place in the backend where
an organization is legitimately addressed by an id supplied in the route
([TENANCY.md §1.3](TENANCY/TENANCY.md)), and the licence for that rests entirely on the routes being
platform-staff-only. `OrganizationControllerAuthorizationTests` asserts both halves — the registry
requires `RequireSuperAdmin`, and `OrganizationProfileController` deliberately does not (it belongs
to the organization itself and is read by its own members).

## The profile as an interview (Phase 40.29)

The profile of 40.5 is a form with seven fields, three of which are lists and one of which is a
dictionary — thirty-odd inputs in practice. **Nobody fills that in.** That is not a usability
complaint: an empty profile is the state in which 40.19's `{{organization.*}}` substitution resolves
to the neutral fallback everywhere, so the entire content-parameterization investment does nothing
for that customer. Roadmap 40.29 exists to make the profile get filled, and its measure of success is
a number — five minutes rather than an hour.

Nothing in the schema changed. What was added is four routes on `OrganizationProfileController` and
one merge policy.

| Route | What it is for |
|---|---|
| `GET /organizations/profile/gaps` | the interview: the next few questions, hardest-hitting first |
| `PATCH /organizations/profile` | one answer to one question; omitted fields keep their stored value |
| `POST /organizations/profile/draft` | what promoting an extracted draft would do to each field — writes nothing |
| `POST /organizations/profile/draft/apply` | the promotion itself, under the merge policy |

Exact bodies: [API_CONTRACTS.md](API_CONTRACTS.md#organization-profile-as-an-interview-phase-4029).

### There is no second extraction pipeline, and that was decided in 40.27

The material a РОП would upload to fill their profile is the same material the 40.27 content pipeline
already reads, and the structure it extracts is the profile's field list, field for field — 40.27
shaped it that way deliberately, so that this block would be a copy rather than a translation. So
this service does not talk to ai-service, does not gain a job table, does not gain a background
worker, and does not gain a Kafka consumer (it is still produce-only). The flow is:

1. The РОП starts an ordinary run: `POST /admin/content-generation` with the product deck and the
   call script (learning-service). The pipeline structures it and stops at the 40.27 checkpoint.
2. They correct what the model got wrong — the edit the checkpoint exists to make cheap.
3. `GET /admin/content-generation/{jobId}` returns `structure`; the client posts that document to
   `POST /organizations/profile/draft` to see what it would do, then to `…/draft/apply`.
4. `GET /organizations/profile/gaps` asks about whatever is still missing.

**The draft crosses the service boundary in the request body, carried by the client.** That is the
block's cross-service decision and the reasoning is in [DECISIONS.md](DECISIONS.md) (2026-08-18): it
keeps organization-service the only writer of the profile and the only publisher of
`organization.profile.updated`, keeps learning-service's replica read-only, and adds no authority,
because the same administrator can already `PUT` an arbitrary structure onto the run and an arbitrary
profile onto this row.

**A run refused by 40.28 is still a usable source.** A refusal after structuring leaves the structure
on the row, and a profile needs less than a lesson does — «нет ни одного возражения» blocks four good
exercises and does not block knowing what the company sells. Only a run refused *before* structuring
(too short, off topic) has nothing to promote.

### The merge policy — fill blanks, grow lists, never silently replace a human's words

This is the question 40.27 refused to answer on this block's behalf, and the scenario it is built for
is concrete: a compliance officer types `banned_claims` in March, a РОП pastes a new product deck in
June, and the model reads the deck's marketing copy as the company's position. An overwrite there is
not a lost edit — it is a persona that starts voicing a promise a lawyer forbade, discovered by the
customer.

- **`product`, `icp`, `tone`, `scriptStages`** each hold one value a suggestion would have to
  displace. Empty → filled without asking (filling a blank destroys nothing). Non-empty and different
  → reported as a `conflict` and **kept**, unless the caller names the field in `acceptedFields`.
- **`scriptStages` is in that group although it is a list**, because it is an ordered sequence
  describing one conversation, not a set: unioning a five-stage script with a seven-stage one produces
  twelve stages in an order that describes no call anybody makes.
- **`objections` and `glossary`** are collections that legitimately accumulate, so the merge is a
  union and an existing entry always wins — which is what preserves the `frequency` an extraction
  cannot know and the answer a manager wrote from experience rather than from a deck.
- **`bannedClaims` is union-only and has no consent value at all.** There is no `acceptedFields`
  string that deletes a banned claim, so no client bug and no stale second tab can produce one. The
  safe direction is the one that forbids more; removing an entry stays a deliberate act on the
  whole-profile form.

### Which gaps, and how many at a time

The vocabulary is `OrganizationProfileGapCodes` — seven codes, a fixed Russian question each, and a
priority. **It is deliberately not 40.28's `ContentSufficiencyCodes`:** «хватит ли материала на четыре
упражнения» and «заполнен ли профиль» disagree in both directions — `banned_claims` and the glossary
block nothing in generation and matter a great deal here, while `too_short` and `off_topic` are facts
about a document and say nothing about a row.

The questions are fixed on the server, not authored by the model. That is 40.28's «коды на проводе,
предложения на сервере» applied unchanged, and it matters more here, because a question is answered
*into a database column* — «пришлите ваш прайс в PDF» is a question with no field behind it.

- **`blocking`** — `product`, `icp`, `objections` (fewer than three). These are what 40.19 renders into
  every lesson and every persona prompt; while one is missing, that customer reads the library exactly
  as it read before 40.19 existed. `isReadyForParameterization` is exactly «none of these is open».
- **`important`** — `scriptStages` (fewer than three), `tone`, `bannedClaims`.
- **`optional`** — `glossary`.

**Three questions per round, and the cap is the feature.** The roadmap's failure mode is «30 пустых
полей никто не заполнит», which is about the size of what a person is shown. `totalGapCount` travels
alongside so a screen can show three and still say «осталось ещё 4» — a capped list with no total is a
progress bar that lies. Two of the questions («есть ли запрещённые обещания», «есть ли свои термины»)
may honestly be answered «таких нет», and the profile has no marker for that; they can therefore
persist forever, which is harmless because readiness is computed from the blocking tier only and the
cap means they are never shown while a real gap is open.

## What 40.6 needs to know

- `Organization.Id` is the value that will become the JWT `org_id` claim and the
  `X-Organization-Id` header. Nothing about its shape changes for 40.6 — it is a plain `Guid`.
- The `membership (user_id, organization_id, ...)` table from 40.6 will hold a bare
  `organization_id uuid` with **no** foreign key into this service's database (DB-per-service —
  see docs/TENANCY/TENANCY.md §1.1), the same pattern `identity-service`'s `UserReplica` already
  uses for users elsewhere.
- `organization.created` is consumed by identity-service since 40.9, into `OrganizationReplicas`.
  The first `OrgAdmin` is *not* minted from that event, though — it is created on demand by
  `POST /admin/platform/organizations/bootstrap-admin`, because an organization exists for a while
  before anyone decides who runs it. See [DECISIONS.md](DECISIONS.md) (2026-08-15).
- `OrganizationController`'s missing role restriction was closed in 40.9 — see "Authorization"
  above.
- `OrganizationProfileController` is already `[TenantScoped]` and reads the organization solely
  from `X-Organization-Id`. It will start being reachable through a real user's JWT the moment
  identity-service issues the `org_id` claim in 40.6 — no changes needed on this service's side.
