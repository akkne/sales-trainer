# Identity Service (microservices Phase 2)

Implemented on branch `service/identity`. First **stateful** service extracted from the
monolith per [MICROSERVICES_ROADMAP.md](MICROSERVICES_ROADMAP.md) Phase 2. It is the
identity root of the platform and the **sole JWT issuer**; every other service trusts
JWTs validated at the gateway and keeps a local `UserReplica` fed by the `user.*` events
this service produces.

> Strangler-fig note: the monolith's `Auth`/`Profile`/`Onboarding`/`Avatars` slices are
> **left in place as reference** (project rule — never delete monolith code). Traffic is
> moved by flipping route prefixes at the YARP gateway, not by deleting code.

## Location & shape

```
src/backend/identity-service/
  Identity/                     ← ASP.NET Core 9 service (RootNamespace Sellevate.Identity)
    Program.cs                  ← host: Serilog→Loki, EF/Postgres, JWT, CORS, Kafka producer
    Features/{Auth,Invites,Membership,Profile,Onboarding,Avatars}/
    Eventing/                   ← user.* integration-event contracts + Kafka publisher
    Infrastructure/{Configuration,Data,Email,Storage}/
    Infrastructure/Data/Migrations/  ← own EF migrations (InitialIdentitySchema)
    Dockerfile                  ← build context = src/backend (needs building-blocks)
  Identity.Tests/               ← unit (InMemory) + integration (Testcontainers Postgres)
```

## Owns (Postgres database `identity-db`)

`Users`, `RefreshTokens`, `EmailVerificationCodes`, `UserProfiles`, `DefaultAvatar`,
`Memberships` (Phase 40.6 — see below), `Invites` (Phase 40.7 — see below),
`OrganizationAuthConfigurations` (Phase 40.8 — see below), `OrganizationReplicas` and
`ImpersonationAuditEntries` (Phase 40.9 — see below). The service has **its own database**, separate
from the monolith's. `DatabaseBootstrapper` creates the `identity` database on startup if
missing (idempotent), then EF `Migrate()` builds the schema — so it works against a fresh
or an already-populated shared Postgres instance.

## Memberships and role split (Phase 40.6, revised 2026-08-16)

Roles live on two independent axes:

- **Platform role** (`User.Role`, unchanged column): `User = 0`, `Admin = 1`, `SuperAdmin = 2` —
  Sellevate staff, deliberately **not** bounded by tenancy.
- **Organization role** (`Membership.Role`): `Manager = 0`, `TenancyAdmin = 1`,
  `TenancySuperAdmin = 2` — a РОП is the admin of one organization, never of the platform.

At **either** level the only difference between the admin and the superadmin is that only the
superadmin may **add or remove users**. Everything else is identical.

Phase 40.6 had removed `Admin` and left value 1 unassigned; the owner reinstated it on 2026-08-16
at that same value, where it already meant "global platform admin", so no stored row changes
meaning. `OrgAdmin` was renamed to `TenancyAdmin` in place — both role columns are `integer`
(`HasConversion<int>()`), so the rename needed **no data migration and no EF migration**. The
retired `OrgAdmin` string is rejected by `InviteService.ParseRole` with a message naming its
replacements rather than silently mapped. Full rationale and the route audit:
[DECISIONS.md](DECISIONS.md) → 2026-08-16.

`Membership (UserId, OrganizationId, Role, Status, InvitedBy, JoinedAt, DeactivatedAt)`,
PK `(UserId, OrganizationId)` — from day one, even though the current UI only ever creates
one row per user. `OrganizationId` is a bare `uuid` with **no FK**: `organization-service`
owns the registry in its own database (DB-per-service). A user with no `Membership` row has
no organization access. See [DB_SCHEMA.md](DB_SCHEMA.md) and
[TENANCY/TENANCY.md](TENANCY/TENANCY.md) §4.2.

Token issuance (`AuthenticationService.IssueTokensForUserAsync`) looks up the user's single
active membership (ordered by `JoinedAt`, deterministic if that assumption ever breaks
before multi-org support lands) and adds `org_id`/`org_role` claims to the JWT when one
exists; both claims are simply absent otherwise. `AuthTokenResponseDto` and `GET /auth/me`
mirror `orgId`/`orgRole` for the frontend.

A user with **no** active membership gets neither `org_id` nor `org_role` — absent membership is
never implied. Platform staff normally have no membership anywhere, which is exactly why
`RequireOrgAdmin` and `RequireOrgSuperAdmin` are satisfied by the platform `role` claim on its own.
For the same reason Google login exempts `Admin`/`SuperAdmin` from the "must have an active
membership" check; the password path never had it.

Authorization policies (`RequirePlatformAdmin`, `RequireSuperAdmin`, `RequireOrgAdmin`,
`RequireOrgSuperAdmin`) are declared identically in every service by
`Common/Constants/AuthorizationPolicies.Register` — see [ADMIN_PANEL.md](ADMIN_PANEL.md) for the
full policy table and the route audit across services.

Migrating existing users into a default organization's `Membership` row is **40.9's job**,
not this block's — the schema is intentionally nullable/backfillable (`InvitedBy`) to leave
that migration room without a follow-up schema change.

## Closed registration and invites (Phase 40.7)

`POST /auth/register` **is deleted** — not hidden, not flagged, not role-gated. There is no
route in this service that creates an account on request. `POST /auth/google` is a login
method only: an unknown Google identity, and a known one whose account has no *active*
membership, both get `401` with one identical message (a distinguishable answer would let an
outsider probe which addresses belong to a customer). See
[TENANCY/TENANCY.md](TENANCY/TENANCY.md) §4.1.

`Invite (Id, OrganizationId, Email, Role, TokenHash, ExpiresAt, AcceptedAt, RevokedAt,
InvitedBy, CreatedAt)` is the service's **first tenant-scoped table**: it implements
`ITenantScoped`, has an EF global query filter, and the `AddInvite` migration calls
`EnableTenantRls("Invites")`. Consequently `Program.cs` now registers
`TenantSaveChangesInterceptor` + `TenantConnectionInterceptor` on `IdentityDbContext` and adds
`UseSellevateTenantContext()`. `Users`, `RefreshTokens` and `Memberships` stay outside RLS —
the first two are platform-global identities and the third is what resolves an organization in
the first place.

Because RLS is on, any code path that **reads** invites must open an explicit transaction so
`TenantConnectionInterceptor` has somewhere to put its `SET LOCAL app.organization_id`; a bare
`SELECT` returns nothing. `InviteService` does this in all three operations, and so does the
test helper that inspects invite rows.

### The token

`{organizationId:N}.{nonce}.{signature}` — base64url parts, the signature an HMAC-SHA256 over
the first two, keyed by `Invites:SigningKey` (falling back to `Jwt:Key`). Only
`SHA256(rawToken)` is persisted; the raw token exists in the creation response and the invite
email and nowhere else.

The token carries its own organization on purpose. `POST /auth/invites/{token}/accept` is
anonymous — the caller has no organization and therefore no `X-Organization-Id` header — so the
organization is recovered from verified signed material rather than from a request field, which
keeps the "never read the organization from body/query/route" rule intact. The nonce carries the
entropy (32 bytes from a CSPRNG), so knowing an organization id buys an attacker nothing.

### Acceptance

Accepting an invite for an address that already has an account **adds a membership to that
account** and never creates a second user; a deactivated membership for the same organization is
reactivated rather than duplicated. A brand-new address needs a `password` in the body and gets
`IsEmailVerified = true` with no verification code — the token already proved the mailbox
(see [EMAIL_VERIFICATION.md](EMAIL_VERIFICATION.md)). The invite is single-use: `AcceptedAt` is
stamped in the same transaction, so a replay is `409`.

### Offboarding

`DELETE /memberships/{userId}` sets `Status = Deactivated` + `DeactivatedAt`. There is **no**
code path in this service that deletes a membership row — the manager's attempt history, calls
and scores belong to the organization (TENANCY.md §4.3). Deactivated members lose `org_id` /
`org_role` at the next token issuance and cannot sign in with Google.

## Login method as organization configuration (Phase 40.8)

The login flow is **three steps**, while there is still exactly one provider:

1. `POST /auth/login/start` `{email}` → `{method}`
2. the email is resolved to an organization — active `Membership` first (the invite path), then
   `AllowedEmailDomains` — and that organization's configured method is returned
3. `POST /auth/login` dispatches to the `IAuthProvider` whose `Method` matches

`OrganizationAuthConfiguration (OrganizationId PK, Method, ProviderSettings jsonb,
AllowedEmailDomains text[], IsJustInTimeProvisioningEnabled, SessionLifetime,
IsMultiFactorAuthenticationRequired, CreatedAt)` lives in **identity-db**, not in
organization-service: it is read before authentication, when there is no JWT and no
`X-Organization-Id`, and a cross-service call there would put login behind another service's
availability and would force an anonymous "which organization owns this domain" endpoint —
a better enumeration oracle than the one this design avoids. identity-service owns *access*;
organization-service owns the registry and the business profile.

Unlike `Invites`, the table is **not** `ITenantScoped` and has **no RLS policy**. Its main read
is a cross-tenant question asked without a tenant context; a table that must bypass RLS on every
login is not protected by it. See [DECISIONS.md](DECISIONS.md) (2026-08-15, 40.8) — including the
consequence that a future write path must scope by `ITenantContext` explicitly.

`IAuthProvider { string Method; Task<AuthResult> AuthenticateAsync(AuthRequest, ...) }` has one
implementation, `PasswordAuthProvider`, which holds the bcrypt check that used to sit inline in
`AuthenticationService.LoginWithEmailAsync`. Providers are injected as `IEnumerable<IAuthProvider>`
and selected by `Method`; adding OIDC/SAML is one registration in
`AuthenticationServiceCollectionExtensions` plus the implementation, and nothing in the flow
changes. A provider never issues tokens — that stays in `IssueTokensForUserAsync`, so every login
method produces identical claims and the same refresh-token family.

**Not implemented, on purpose:** OIDC, SAML, `IsJustInTimeProvisioningEnabled` (stored, never
read), `SessionLifetime` (stored, not yet applied to token issuance), and any endpoint that
writes a configuration row — organizations get one when 40.9's superadmin panel creates them, and
one with no row logs in with a password. An organization configured for a method with no provider
is **refused** (`401`, even for the right password), never downgraded to a password.

`POST /auth/login/start` answers `200` for every syntactically valid address, known or not, and
never names the organization — the same anti-enumeration property 40.7 gave `POST /auth/google`.

## Platform superadmin surface (Phase 40.9)

Two tables and one controller, all gated by `RequireSuperAdmin` (unchanged by the 2026-08-16 role
split: impersonation and bootstrapping an organization's first admin are both superadmin-exclusive)
and none of them tenant-scoped —
these routes act *on* organizations rather than *inside* one, so there is no `X-Organization-Id`
header to scope by. Contracts: [API_CONTRACTS.md](API_CONTRACTS.md) → "Platform superadmin".

### `OrganizationReplicas`

identity-service's read-only projection of organization-service's tenant registry
(`organizationId` PK, `name`, `slug`, `status`, `updatedAt`), fed over Kafka. It exists because
identity-service is the only service that mints tokens, and a suspended organization has to stop
producing them — asking organization-service synchronously would put a second service on the
authentication hot path.

Suspension is enforced in `AuthenticationService.IssueTokensForUserAsync`, the one point password
login, Google sign-in, invite acceptance and refresh all converge on. **A missing replica row reads
as active**, never as suspended: the projection is eventually consistent and a lagging consumer
must not lock a customer out of their own product.

### `ImpersonationAuditEntries`

Append-only: actor id and email, organization id and name (copied at write time), the mandatory
reason, issue and expiry times. Written and committed *before* the token is handed back, so a token
that exists always has a record behind it.

### The impersonation token

`POST /admin/platform/impersonation` mints a brand-new JWT rather than adding a parameter to an
existing route ([TENANCY.md §1.3](TENANCY/TENANCY.md)). It carries `role: User` — deliberately not
`SuperAdmin`, which is what stops it reaching any `RequireSuperAdmin` route including this one —
plus `org_id`, `org_role: TenancyAdmin` — deliberately one rank below
`TenancySuperAdmin`, so an impersonator cannot add or remove the borrowed organization's users —
and the marker claims `imp` / `imp_id` / `imp_actor`. `sub`
stays the superadmin's own user id: the impersonator borrows an organization, never an identity.
It is short-lived (`Impersonation:TokenLifetimeMinutes`, default 15) and has **no refresh token**.

Built by hand in `PlatformAdminService` rather than through `AuthenticationService`'s token path,
because every one of those differences *is* a security property.

### Bootstrapping the first `TenancySuperAdmin`

`POST /admin/platform/organizations/bootstrap-admin` opens a DI scope, points that scope's
`TenantContext` at the target organization and calls the ordinary Phase 40.7 `IInviteService` — the
same code, the same tenant guards, the same email. There is no second invite path. The role is
always `TenancySuperAdmin` and is not read from the request — only a superadmin can invite, so a
first admin one rank lower would leave the organization unable to add anybody. The endpoint answers
`409` if the organization already has an active `TenancySuperAdmin` or a pending
`TenancySuperAdmin` invite, so it cannot become a back door into a running customer's organization.

## Frontend REST (unchanged paths, served via the gateway)

`/auth/*`, `/demo/*`, `/profile/*`, `/onboarding/*`, `/avatars/*`, since 40.7 `/invites/*`
and `/memberships/*`, and since 40.9 `/admin/platform/*` (all gateway routes point at the
`identity` cluster) — identical request /
response contracts to the monolith (see [API_CONTRACTS.md](API_CONTRACTS.md)). JWT
issuance, Google OAuth, MailerSend email verification and S3/MinIO avatar storage are all
preserved verbatim.

## Kafka events produced (`user.*`)

| Topic | When | Payload |
|---|---|---|
| `user.registered` | invite accepted by a new address, super-admin seed | `{userId, email, displayName, avatarKey}` |
| `user.avatar.changed` | avatar upload / reset | `{userId, avatarKey}` (null on reset) |
| `user.updated` | (contract ready; no trigger yet — no rename endpoint exists) | `{userId, displayName, avatarKey}` |
| `user.deleted` | (contract ready; no trigger yet — no delete-account endpoint) | `{userId}` |

Events go through `IUserEventPublisher` → the shared `KafkaEventPublisher` in
`building-blocks`, keyed by `userId` for per-user ordering.

## Kafka events consumed (`organization.*`, Phase 40.9)

Until 40.9 identity only produced events. `OrganizationReplicaConsumer` now subscribes to
`organization.created` / `organization.updated` / `organization.suspended` and maintains
`OrganizationReplicas`. It opts out of the base consumer's "every message must carry an
organization" rule (`RequiresOrganization => false`): these events *describe* the tenant registry,
the organization is the payload rather than the context.

The projection logic lives in `OrganizationReplicaProjector` so it can be unit-tested without a
broker; the consumer is only the transport shell.

**Operational consequence:** identity-service now resolves the Redis-backed idempotency store at
startup (every `KafkaConsumerBackgroundService` needs one), so Redis has to be reachable when the
service boots. It did not before.

## Known transitional limitation — `GET /profile` aggregates

The profile-stats DTO includes streak / XP / completed-skill / average-score numbers that
are owned by **Gamification** and **Learning**, which are not extracted yet (roadmap
phases 7 & 8). Until they exist, the Identity service returns those four aggregate fields
as **0** while serving the identity-owned fields (displayName, email, persona, avatarUrl)
truthfully. The DTO shape is unchanged, so the frontend does not break; once Gamification/
Learning are extracted, `GET /profile` composes the real numbers from them. This is called
out in `ProfileService` and in [API_CONTRACTS.md](API_CONTRACTS.md).

## Adaptations vs the monolith slice

- The monolith's Hangfire daily `ExpiredEmailVerificationCleanupJob` became a lightweight
  `ExpiredEmailVerificationCleanupService` (`BackgroundService`, runs on startup + every
  24h) — the Identity service carries no Hangfire dependency.
- `AppDbContext` → a focused `IdentityDbContext` with only the entities this service owns.
- Prometheus login/registration counters from the monolith `AuthController` were dropped
  (Analytics owns product metrics; Identity stays lean).

## Running it

- Full Docker stack: `docker compose up --build -d identity gateway` (plus infra).
- Local dev (host, hot reload): `scripts/dev-identity.sh` after `scripts/dev-infra.sh`;
  run `scripts/dev-gateway.sh` too to exercise the flipped routes end to end.
- Direct: `http://localhost:5002` · health: `GET /healthz` · Swagger in Development.

See [LOCAL_DEV.md](LOCAL_DEV.md) for ports and [TESTING/IDENTITY_SERVICE.md](TESTING/IDENTITY_SERVICE.md)
for the test plan.
