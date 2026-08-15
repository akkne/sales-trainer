# Multi-tenancy — isolation & access

**Status:** DESIGN ONLY. Nothing here is implemented. No `tenant` identifier exists anywhere in
the codebase today (verified: zero matches across `*.cs`, `*.ts`, `*.tsx`).

Companion docs: [CONTENT_MODEL.md](CONTENT_MODEL.md) (customization & versioning),
[ASSIGNMENTS.md](ASSIGNMENTS.md) (admin → manager workflow, AI in the admin panel).

**Execution plan:** [docs/ROADMAP.md](../ROADMAP.md) → **Phase 40** (in Russian) — stages A–G,
34 actionable blocks. Read this document before starting any of them.

---

## 0. Naming — the tenant is NOT called "Company"

`Company` is already taken, and by something completely different.
`src/backend/company-service` + `docs/COMPANIES/` is a **private prospect CRM for one
salesperson**: the companies they are selling *to*. Every row there is scoped `WHERE UserId = ...`
and carries a free-form description used to seed a practice cold call.

If the tenant is also called `Company`, then `CompanyId` means "the prospect I'm calling" in one
service and "the customer who bought Sellevate" in another. That ambiguity lands in JWT claims,
Kafka payloads and EF query filters — exactly the places where a mix-up is a data leak rather
than a compile error.

**Decision: the tenant entity is `Organization`.** Column `organization_id`, claim `org_id`,
header `X-Organization-Id`, service `organization-service`.

| Term | Means | Owner |
|------|-------|-------|
| `Organization` | A paying customer of Sellevate (a sales department) | `organization-service` (new) |
| `Company` | A prospect a salesperson practises calling | `company-service` (exists) |

Russian UI copy can still say «Компания» for the tenant — this constraint is about identifiers,
not user-facing labels.

---

## 1. Data isolation

### 1.1 The starting point is already DB-per-service

The proposal "one database, `tenant_id` column everywhere" does not describe this system. The
migration to microservices is finished and each service owns a **separate Postgres database**:
`identity`, `learning`, `ai`, `company`, `gamification`, `social`, `notification` (+ Redis-only
analytics, + Mongo for dialogs). There are no cross-service joins to protect.

This makes tenancy easier, not harder, but it changes the shape of the work:

- `organization_id` is added to tenant-scoped tables **in each of those databases**, not in one.
- RLS policies, the app role, and the `SET LOCAL` convention are configured **per database** —
  seven times, so it belongs in a shared BuildingBlocks component rather than being hand-rolled
  per service.
- There is no single "tenant" table every service can FK into. `organization-service` owns the
  registry; other services hold a bare `organization_id` `uuid` with **no** foreign key, and
  learn about lifecycle changes over Kafka. This is the same pattern already used for users
  (`BuildingBlocks/Identity/UserReplica.cs`).

### 1.2 What is tenant-scoped and what is not

Not every table gets the column. Three categories:

| Category | `organization_id` | Examples |
|----------|-------------------|----------|
| **Tenant data** | `NOT NULL` | user progress, attempts, dialog sessions, assignments, prospect companies, notifications |
| **Global content library** | `NULL` = global, non-null = an org's own/overridden copy | `skills`, `topics`, `lessons`, `exercises`, `techniques`, `reference_materials`, `dialog_modes` |
| **Platform-global** | no column | `default_avatars`, `exercise_type_prompts`, outbox plumbing tables |

The nullable-column trick for content is what makes one base curriculum serve every customer —
see [CONTENT_MODEL.md](CONTENT_MODEL.md). It also means the EF global query filter for content
tables is `x.OrganizationId == null || x.OrganizationId == current`, **not** plain equality.

### 1.3 Layer 1 — the gateway, not the token-per-service

The system already solved "where does identity come from" and the tenant must reuse it verbatim.
`BuildingBlocks/Identity/IdentityHeaders.cs`: the gateway validates the JWT **once**, then injects
`X-User-Id` / `X-User-Role` into the downstream request, and — the documented security rule —
*strips any client-supplied copies*. Downstream services trust the header only because it arrived
through the gateway.

So: add `org_id` to the JWT (issued by `identity-service`), and `X-Organization-Id` to
`IdentityHeaders`, set by the gateway from the validated token and stripped from inbound requests.

The rule the proposal states stands, sharpened: **the organization is never read from the request
body, query string, or route.** `?organizationId=5` is ignored, not validated. A superadmin
acting across tenants does so through an explicit impersonation endpoint that mints a new token —
never through a parameter on an ordinary endpoint.

Practical note: `ITenantContext` is populated from the header by middleware and must be
**request-scoped**. Background services (`FollowUpReminderBackgroundService`,
`OutboxRelayBackgroundService`, the Hangfire jobs in gamification) have no request, so they must
open a scope and set the context explicitly per unit of work — see §1.6.

### 1.4 Layer 2 — EF Core, for convenience only

```csharp
modelBuilder.Entity<UserExerciseAttempt>()
    .HasQueryFilter(x => x.OrganizationId == _tenant.OrganizationId);
```

The proposal's caveat is correct and worth keeping in writing: this is **ergonomics, not
security**. It does nothing on write, it is removed by `IgnoreQueryFilters()`, and it does not
exist for `ExecuteUpdate`/`ExecuteDelete` or any raw SQL.

Two EF-specific traps in this codebase:

- **Query filters are inherited by navigation properties, not by owned/`FromSql` queries.** The
  learning service composes `Skill → Topic → Lesson → Exercise`; a filter on `Lesson` does not
  imply one on `Exercise`. Every tenant-scoped entity needs its own.
- **`HasQueryFilter` captures the context instance.** Because `ITenantContext` is injected into
  the `DbContext` and the filter closes over it, the `DbContext` must be scoped (it is) and must
  never be reused across tenants. A pooled `DbContextFactory` — currently used only for design-time
  (`CompanyDbContextFactory`, `IdentityDbContextFactory`) — would silently cache the first tenant's
  filter. Do not introduce `AddDbContextPool` on tenant-scoped contexts without a
  `IDbContextFactory` tenant-aware wrapper.

The write-side guard is the `SaveChanges` override / interceptor — see §2.

### 1.5 Layer 3 — Postgres RLS, the actual boundary

This is the layer that survives a forgotten filter, a Dapper query, or an `ExecuteDelete`.

```sql
ALTER TABLE user_exercise_attempts ENABLE ROW LEVEL SECURITY;
ALTER TABLE user_exercise_attempts FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON user_exercise_attempts
  USING      (organization_id = current_setting('app.organization_id')::uuid)
  WITH CHECK (organization_id = current_setting('app.organization_id')::uuid);
```

Notes that decide whether this works or is theatre:

- **`FORCE` is required.** Without it, the table owner bypasses its own policies — and the
  migration role usually *is* the owner. Migrations then run as the owner (policies bypassed,
  which is what you want), while the app connects as a separate `sellevate_app` role that owns
  nothing and lacks `BYPASSRLS`.
- **`USING` alone only filters reads.** `WITH CHECK` is what blocks writing a foreign
  `organization_id`. State both.
- **`current_setting('app.organization_id')` throws if unset.** Use
  `current_setting('app.organization_id', true)` (missing_ok) and let the comparison against
  `NULL` yield zero rows — fail closed, not error-out. For content tables the policy is
  `organization_id IS NULL OR organization_id = ...`.
- **Connection pooling is the failure mode.** `SET LOCAL` is transaction-scoped, so it must be
  issued inside the same transaction as the query. A `SET` (without `LOCAL`) leaks the previous
  tenant onto the next request that borrows the pooled connection. This is the single highest-risk
  detail of the whole design and the reason it belongs in one shared `DbConnectionInterceptor` in
  BuildingBlocks, not in seven services.
- EF opens an implicit transaction only when there is something to save; read paths often run
  without one. The interceptor therefore sets the GUC on **connection open** and the code must
  ensure a connection is never handed between tenants — or, more robustly, wrap each request's
  data access in an explicit transaction.

### 1.6 The background-job hole

Every one of these runs with no HTTP request and today has no tenant:

| Service | Job | What breaks without a tenant context |
|---------|-----|--------------------------------------|
| company | `FollowUpReminderBackgroundService` | scans due follow-ups across **all** orgs |
| all | `OutboxRelayBackgroundService` | publishes rows it must not be able to read |
| identity | `ExpiredRefreshTokenCleanupService`, `ExpiredEmailVerificationCleanupService` | cross-tenant by design |
| gamification | Hangfire streak/weekly-closure jobs | cross-tenant by design |

Two legitimate modes, and they must be distinguishable in code:

- **Per-tenant iteration** — the job enumerates organizations and opens a scoped context per org.
  Correct for anything producing user-visible output (follow-up reminders, assignment deadline
  nudges).
- **System mode** — `ITenantContext.IsSystem`, connecting as a role with `BYPASSRLS`, for genuinely
  global plumbing (outbox relay, token cleanup). This must be an explicit, auditable opt-in, not
  the default when the context happens to be empty. **An unset tenant is an exception, never a
  license.**

### 1.7 Kafka and the outbox

`OutboxMessage` and `EventEnvelope` both need `OrganizationId`, and the envelope is the right
place — it is already the frozen cross-service contract (`{eventId, occurredAt, type, version,
data}` → add `organizationId`). Bumping the envelope is a coordinated change across all producers
and consumers, so it happens once, before any tenant-scoped consumer exists.

- `OutboxMessage.PartitionKey` is currently the user id (for per-user ordering). Keep it — user ids
  are globally unique, and switching to `org:user` would reshuffle partitions for no gain.
- **Consumers must set the tenant context from the envelope before handling**, and fail the message
  if it is absent. The failure mode the proposal names is exact: a consumer without context either
  silently reads zero rows or, worse, writes with a wrong/absent tenant.
- The outbox relay is the one component that legitimately reads every tenant's rows (system mode),
  which is why the tenant lives in the envelope payload rather than being re-derived at publish
  time.

### 1.8 Mongo (dialog sessions) and Redis

- **Mongo** — `DialogSession` documents hold real transcripts of real sales conversations. Add
  `organizationId` to the document, to the compound indexes, and make it the shard key prefix if
  sharding ever happens. There is no RLS equivalent, so the filter is application-enforced; keep
  all session reads behind one repository so there is exactly one place to audit.
  **Done in 40.11** — `DialogSessionRepository` in ai-service. What made it a boundary rather than
  a convention: it takes `ITenantContext` in its constructor, it holds the only
  `GetCollection<DialogSession>` in the service (`MongoDbContext` no longer exposes the
  collection), no method accepts an organization or returns "all organizations", an unset tenant
  raises instead of widening, there is no system-mode bypass, and a unit test asserts against the
  source tree that no second file reaches the collection. Copy that shape for
  `chat_conversations` in social-service (40.13).
- **Redis** — analytics, presence, notification inboxes, the idempotency store and the LLM verdict
  cache are all Redis-only. **Namespace every key with the org id** (`org:{orgId}:...`). The
  current `RedisIdempotencyStore` and the custom-scenario verdict cache key off user-supplied
  content; without a prefix, one org's cached verdict answers another org's request, and presence
  counts leak headcount between customers.
  **Done for ai-service in 40.11**: the verdict cache, the voice quota counters and
  `RedisIdempotencyStore` all carry `org:{orgId}:`. The idempotency organization comes from the
  event envelope, not from ambient context, and an event with no organization deliberately keeps
  the un-prefixed key — there is no tenant to confuse, and the unchanged key is what preserves
  dedupe across the deploy. Pre-prefix keys are never read again and expire on their own TTL;
  nothing is flushed (`docs/DECISIONS.md`).

### 1.9 Indexes and unique constraints

`organization_id` goes **first** in composite indexes — see §3 for the reasoning and the migration
mechanics.

The higher-severity item is uniqueness. Every existing global unique constraint must be re-examined:

| Constraint today | After tenancy |
|------------------|---------------|
| `UNIQUE(email)` on users | **stays global** — see §4.1, users are cross-org identities |
| `UNIQUE(iconic_name)` on skills | `UNIQUE(organization_id, iconic_name)` — two orgs may both have `objections` |
| dialog mode `key` (e.g. `company-call`, `custom-scenario`) | seeded modes stay global; org-authored modes need the org in the key — **done in 40.11**: `UNIQUE(OrganizationId, BundleId, Key) WHERE OrganizationId IS NOT NULL` plus a partial `UNIQUE(BundleId, Key) WHERE OrganizationId IS NULL`, because Postgres treats NULLs in a composite unique index as distinct |

A missed one does not leak data — it makes onboarding the second customer fail with a constraint
violation, which is a better failure than the alternative but still blocks the sale.

---

## 2. The `SaveChanges` write guard

### 2.1 The code

```csharp
public interface ITenantScoped
{
    Guid OrganizationId { get; set; }
}

public interface ITenantContext
{
    Guid? OrganizationId { get; }
    bool IsSystem { get; }
}
```

The guard belongs in a `SaveChangesInterceptor` in `BuildingBlocks`, registered once and added by
each service — the codebase has seven `DbContext`s and putting the logic in a base class would
either force a shared context hierarchy or be copy-pasted seven times.

```csharp
public sealed class TenantSaveChangesInterceptor(ITenantContext tenant) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Enforce(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Enforce(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Enforce(DbContext? context)
    {
        if (context is null || tenant.IsSystem)
        {
            return;
        }

        var current = tenant.OrganizationId
            ?? throw new InvalidOperationException("Organization context is not set.");

        foreach (var entry in context.ChangeTracker.Entries<ITenantScoped>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.OrganizationId == Guid.Empty)
                    {
                        entry.Entity.OrganizationId = current;
                    }
                    else if (entry.Entity.OrganizationId != current)
                    {
                        throw new CrossTenantWriteException(entry.Metadata.Name, current);
                    }

                    break;

                case EntityState.Modified:
                case EntityState.Deleted:
                    var original = entry.OriginalValues
                        .GetValue<Guid>(nameof(ITenantScoped.OrganizationId));

                    if (original != current || entry.Entity.OrganizationId != original)
                    {
                        throw new CrossTenantWriteException(entry.Metadata.Name, current);
                    }

                    break;
            }
        }
    }
}
```

Registered per service:

```csharp
builder.Services.AddScoped<TenantSaveChangesInterceptor>();
builder.Services.AddDbContext<LearningDbContext>((sp, options) => options
    .UseNpgsql(connectionString)
    .AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>()));
```

### 2.2 Why `OriginalValues` and not the current value

On `Modified`/`Deleted` the check reads the value **as loaded from the database**. Comparing only
`entry.Entity.OrganizationId` passes trivially for an attacker who loads a foreign row via
`IgnoreQueryFilters()` and then assigns their own org id to it — the entity now looks correct while
the `WHERE` clause still targets someone else's row. The second half of the condition
(`entry.Entity.OrganizationId != original`) additionally makes the column **immutable after
creation**: moving a row between organizations is never an incidental update.

### 2.3 Does it work with `SaveChangesAsync`? Yes — but only if both are overridden

`DbContext` exposes four virtual methods:

```
SaveChanges()                              → delegates to SaveChanges(bool)
SaveChanges(bool)                          ← real work
SaveChangesAsync(CancellationToken)        → delegates to SaveChangesAsync(bool, CancellationToken)
SaveChangesAsync(bool, CancellationToken)  ← real work
```

Overriding only `SaveChanges()` leaves the async path — which is what this codebase uses
everywhere (`async/await` is a CODESTYLE rule) — completely unguarded. Overriding the two
`bool` overloads covers all four, because the parameterless ones delegate.

The interceptor has the identical trap: `SavingChanges` and `SavingChangesAsync` are separate
methods and **both** must be implemented. An interceptor with only the sync method is a no-op in
this codebase.

### 2.4 What it cannot catch

- `ExecuteUpdate` / `ExecuteDelete` — bypass the change tracker and `SaveChanges` entirely
- Dapper and any raw SQL
- anything connecting to Postgres outside the app

This list is the entire justification for RLS. Layer 2 and this guard reduce the blast radius of
ordinary mistakes; layer 3 is what makes a mistake non-exploitable.

---

## 3. Composite index column order

### 3.1 How to express it in EF

Order of properties in the anonymous object = order of columns in the index:

```csharp
modelBuilder.Entity<UserExerciseAttempt>()
    .HasIndex(x => new { x.OrganizationId, x.UserId, x.AttemptedAt });
// → CREATE INDEX ... ON user_exercise_attempts (organization_id, user_id, attempted_at)
```

Rule: **equality predicates first, range/sort last.** `organization_id = X` is in every query, so
it is always the leading column; a timestamp used for `ORDER BY ... DESC` / `BETWEEN` goes last.

Why: a B-tree is sorted by the first column, then by the second within equal firsts, and so on
(the *leftmost prefix* rule). An index on `(user_id, organization_id)` cannot serve a query that
filters only on `organization_id`. An index on `(organization_id, user_id)` serves both
"everything in this org" and "this user in this org". Postgres 18 added skip scan, which partially
rescues a bad prefix — do not design for it; the plan is still worse and it does not exist on
older servers.

Descending components, when the query always sorts one way:

```csharp
modelBuilder.Entity<CallLogEntry>()
    .HasIndex(x => new { x.OrganizationId, x.CompanyId, x.OccurredAt })
    .IsDescending(false, false, true);
```

This matters for the existing `(CompanyId, OccurredAt DESC)` and `(CompanyId, CreatedAt DESC)`
indexes in `company-service`, which become `(OrganizationId, CompanyId, OccurredAt DESC)`.

### 3.2 Changing the order of an existing index

Column order cannot be altered in place — it is always drop + create. On a live database, create
the new one first so no query is ever left unindexed:

```csharp
migrationBuilder.Sql(
    "CREATE INDEX CONCURRENTLY ix_attempts_org_user ON user_exercise_attempts (organization_id, user_id);",
    suppressTransaction: true);

migrationBuilder.Sql(
    "DROP INDEX CONCURRENTLY ix_attempts_user;",
    suppressTransaction: true);
```

`suppressTransaction: true` is mandatory: `CONCURRENTLY` cannot run inside a transaction block and
EF wraps each migration in one. Without `CONCURRENTLY`, `CREATE INDEX` takes an `ACCESS EXCLUSIVE`
lock and blocks all writes to the table for the duration of the build.

Two operational caveats:

- A `CONCURRENTLY` build can fail (e.g. a deadlock or a unique violation) and leave an **invalid**
  index behind that still costs write overhead. Migrations must be followed by a check for
  `pg_index.indisvalid = false`.
- These services auto-migrate on startup (`DatabaseBootstrapper`). A long `CONCURRENTLY` build in
  a startup path delays readiness and, with multiple replicas, races. Index rebuilds of this class
  should be run as a deliberate operational step, not folded into the boot sequence.

---

## 4. Access provisioning

### 4.1 No public registration — and no route that could become one

"Hiding" the registration page is not a control. The design position: `identity-service` has **no
self-service registration route at all**. `POST /auth/register` is deleted, not guarded. An
organization is created only from the internal superadmin panel; its users arrive only by invite.

This is a real deletion of existing behaviour — registration, Google sign-in and email verification
all exist today (`AuthController`, `EmailVerificationService`, `GoogleAuthConfiguration`) — and the
invite flow replaces email verification, since possession of the invite token already proves the
address.

### 4.2 `memberships` from day one

```
user                     — a global identity
  id, email UNIQUE, password_hash, display_name, ...      (no organization_id)

membership
  user_id, organization_id, role, status, invited_by, joined_at, deactivated_at
  PRIMARY KEY (user_id, organization_id)
```

`users.email` stays globally unique and users hold **no** organization column. The join table
carries the relationship even while the UI allows exactly one organization per user. Retrofitting
it later means rewriting the JWT, every authorization check and the invite flow at once — and the
first person who needs two organizations (a consultant, or Sellevate's own support staff) shows up
earlier than expected.

`UserRole` today is a global enum `{User, Admin, SuperAdmin}` on the user row. It splits in two:

- **Platform role** (on `user`): `SuperAdmin` — Sellevate staff only, creates organizations.
- **Organization role** (on `membership`): `Manager` (the salesperson) / `OrgAdmin` (the РОП).

`Admin` as a global role disappears; a РОП is an admin **of one organization**, never of the
platform. Both go into the JWT: `role` (platform) and `org_role` (within `org_id`).

### 4.3 Invites

- One-time, signed token with a TTL; the row records `email`, `organization_id`, `role`,
  `expires_at`, `accepted_at`, `revoked_at`.
- Bulk import by pasting a list of emails — a РОП onboarding 40 people will not click 40 times.
- Revocable before acceptance.
- Accepting an invite for an email that already has a user account adds a `membership`; it does
  not create a second user. This is the case that is impossible to add later if `users` carries
  the org column.
- **Offboarding is deactivation, not deletion** — `membership.status = deactivated`. The
  manager's attempt history, call recordings and scores belong to the organization and are what
  the РОП is paying to keep. Deletion is a separate, explicit GDPR-style operation.

### 4.4 Subdomains — deliberately not now

`client.sellevate.site` costs wildcard TLS, DNS automation, per-tenant CORS (the allow-list is
currently a fixed list in `DEPLOYMENT.md`) and OAuth callback rework. It buys branding. Defer
until a customer pays for branding. Tenant resolution until then: from the JWT after login, and
from the email domain during login.

### 4.5 Login method is per-organization configuration

The login flow today is a single hardcoded branch: email + password (plus Google) for everyone.
The proposal is **not** to build SSO now, but to stop the current shape from making SSO a rewrite.

```
organization_auth_config
  organization_id PK
  method            -- password | oidc | saml   (always `password` at first)
  settings jsonb    -- issuer, client_id, metadata URL, signing certificate
  allowed_email_domains text[]
  jit_provisioning bool
  session_ttl
  require_mfa
```

```csharp
public interface IAuthProvider
{
    string Method { get; }
    Task<AuthResult> AuthenticateAsync(AuthRequest request, CancellationToken cancellationToken);
}
// One implementation: PasswordAuthProvider. That is the whole point.
```

And the login screen becomes three steps **immediately**, while there is one provider:

1. user enters email
2. backend resolves the organization from the email domain (or from the invite)
3. dispatch to the provider registered for that organization's `method`

What this buys, concretely: when a 200-seat customer requires sign-in through their Azure AD, the
work is *adding a provider*. Without it, the same request means rewriting the login flow, session
issuance, the invite mechanic and user provisioning simultaneously — under the customer's deadline,
with the deal as leverage.

`jit_provisioning` is the part usually missed. What a large customer wants is not "SSO", it is:
a person added to the «Продажи» group in their directory appears in Sellevate as a manager
automatically, and loses access when they leave. Without it the РОП invites 200 people by hand and
deprovisions them by hand — and will not.

Cost estimate: roughly one day now (a table, an interface with one implementation, a three-step
login flow) against a multi-week rewrite later.

### 4.6 Per-organization quotas

Voice minutes and LLM spend are already the dominant variable cost (`VOICE_ROLEPLAY.md`:
Deepgram STT + ElevenLabs TTS + OpenAI). Quotas are **per organization** and enforced **at
ai-service**, the one place every voice and LLM call passes through — not re-implemented per
caller. One customer running voice sessions all day must degrade only their own organization's
service, and must be visible in billing before the invoice is.

---

## 5. The commercial trap this architecture has to defuse

Per-customer customization looks like a feature and behaves like a linear cost of delivery.

The decisive question is not technical: **who adapts the content to a customer's script — us, or
their own admin?** If it is us, then at ~20 customers Sellevate is a content agency with a
throughput ceiling, and no amount of layering in the schema helps.

The architecture's actual job is to push customization into **self-service**. That is why
[CONTENT_MODEL.md](CONTENT_MODEL.md) puts the organization profile (product, ICP, typical
objections, script, tone) ahead of content forking: a parameterized base lesson that renders
against a filled-in profile serves every customer, while a forked lesson serves one.

**Measurement to run on the first pilot:** what share of the adaptation is closed by substitution
from the organization profile, and what share needs hands inside the lesson text? If the second
share exceeds a third, the parameterization is factored wrong, and it must be fixed before the
tenth customer, not after.
