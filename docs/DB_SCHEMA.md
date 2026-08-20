# DB Schema

Last updated: 2026-08-18

## Databases overview

| Store      | Purpose                                      |
|------------|----------------------------------------------|
| PostgreSQL | Primary — all structured data                |
| MongoDB    | Chat messages and unstructured dialogue data |
| Redis      | Cache, sessions, team-progress rankings      |

> **Microservices migration — finished.** This overview used to say "so far: identity,
> ai and gamification" and "the monolith's copies remain as reference until Phase 9".
> Both statements are stale: the extraction completed at Phase 9 and the monolith was
> then **deleted** from `main` (it survives only on the `monolith-legacy` branch), so
> there are no "monolith copies" of anything left to compare against. There are now
> **seven** logical Postgres databases on the shared cluster, each with its own EF
> migrations and `DatabaseBootstrapper`, plus two services with no relational database
> at all.
>
> **`identity` (Phase 2)** — `Users`, `UserProfiles`, `RefreshTokens`, `DefaultAvatars`,
> `OutboxMessages`, and the tenancy tables added across Phase 40: `Memberships` (40.6),
> `Invites` (40.7), `OrganizationAuthConfigurations` (40.8), `OrganizationReplicas` and
> `ImpersonationAuditEntries` (40.9).
>
> **`ai` (Phase 6)** — `DialogBundles`, `DialogModes`, `OpenQuestionGlobalContexts`,
> `ExerciseTypePrompts` (ai's own copy), plus `OrganizationProfileReplicas` (40.19) and
> the quota tables `OrganizationQuotas` / `AiUsageRecords` (40.33).
>
> **`gamification` (Phase 7)** — `UserXpRecords`, `UserStreaks`, `GamificationSettings`,
> `ExerciseTypeRewards`, `StreakMilestones`, `Achievements`, `UserAchievements`,
> `Leagues`, `LeagueTiers`, `LeagueMemberships`, `LeagueSettings`, plus a local
> `UserReplicas`, a `UserLearningProgress` projection (completed-lesson count +
> has-completed-any-skill, fed by `lesson.completed`/`skill.completed`),
> `OutboxMessages`, and its own Hangfire schema — **the only Hangfire schema in the
> platform**. See [GAMIFICATION_SERVICE.md](GAMIFICATION_SERVICE.md).
>
> **`learning` (Phase 8)** — the last extraction, and by far the largest database. The
> content tree and progress: `Skills`, `SkillStages`, `Topics`,
> `UserSkillProgressRecords`, `Lessons`, `Exercises`, `UserLessonProgressRecords`,
> `UserExerciseAttempts`, `ExerciseTypePrompts`, `ReferenceMaterials`, `DailyQuotes`,
> `Techniques`, `TechniqueSkills`, `TechniqueCoaches`, `UserTechniqueProgress`, plus a
> local `UserReplicas` read-model fed by `user.*` events and `OutboxMessages`. Phase 40
> then added, in block order: `LessonVersions` (40.15),
> `ProgramVersions`/`ProgramItems`/`ProgramEnrollments` (40.17),
> `OrganizationProfileReplicas` (40.19), `Assignments` and `AssignmentProgressRecords`
> (40.21), `UserDialogScores` (40.22), `DialogReviewNotes` (40.25),
> `ContentGenerationJobs` (40.27), `TeamSkillGapDismissals` (40.31), and
> `ContentAdaptationJobs`/`ContentAdaptationItems` (40.32). See
> [LEARNING_SERVICE.md](LEARNING_SERVICE.md).
>
> **`social` (Phase 8)** — `Friendships`, the `Discuss*` cluster, `UserReplicas`.
> **`company` (Phase 39)** — `Companies`, `CompanyContacts`, `CompanyPersonas`,
> `CallLogEntries`, `PracticeCalls`.
>
> **`organization` (Phase 40.5)** — the tenant registry, new (not extracted from the
> monolith). Owns `Organizations` (the registry — deliberately not tenant-scoped, no RLS)
> and `OrganizationProfiles` (tenant-scoped, RLS enabled via `EnableTenantRls`). See
> [ORGANIZATION_SERVICE.md](ORGANIZATION_SERVICE.md) and [TENANCY.md](TENANCY/TENANCY.md).
>
> **`notification` and `analytics` have no database.** Both keep their state in Redis
> only, which is why the Redis key itself is their tenant boundary — see the Redis
> section below.

---

## PostgreSQL

All tables managed by EF Core migrations (`Infrastructure/Data/Migrations/`).

### Tenancy: the two session settings every RLS policy reads

Row-level security is described table by table below. Two Postgres session settings drive all of it,
both issued as `SET LOCAL` by `TenantConnectionInterceptor` when a transaction starts — never a bare
`SET`, so neither can outlive its transaction or leak onto the next request that borrows the same
pooled connection:

| Setting | Set when | Effect on a tenant policy |
|---|---|---|
| `app.organization_id` | the request or job has an organization | `USING` and `WITH CHECK`: `"OrganizationId" = NULLIF(current_setting('app.organization_id', true), '')::uuid` (the content flavour also admits `"OrganizationId" IS NULL`) |
| `app.platform_mode` | the caller is validated Sellevate staff (platform-wide mode) | **`USING` only**: `COALESCE(NULLIF(current_setting('app.platform_mode', true), ''), 'off') = 'on'` |

`app.platform_mode` arrived with the `RefreshTenantPoliciesForPlatformStaff` migration of
2026-08-16, which re-applied every policy in all seven Postgres databases. It made the two halves of
every policy **asymmetric on purpose: it widens visibility for platform staff, never authorship.** A
Sellevate administrator reads across every organization and still cannot insert or update a row into
one they did not name explicitly, because `WITH CHECK` ignores the setting entirely.

Both settings use `current_setting(..., true)` (missing_ok), so an unset value is `NULL` rather than
an error and the policy fails *closed* — an unscoped connection sees zero tenant rows. Note the
practical consequence: `SET LOCAL` has no effect outside a transaction, so a read issued outside one
sees nothing. That is why every tenant-touching service method opens a `TenantTransactionScope` —
see [ARCHITECTURE.md](ARCHITECTURE.md). System-mode background jobs set neither and rely on a
`BYPASSRLS` role instead.

---

### `Users`

| Column         | Type                        | Nullable | Notes                              |
|----------------|-----------------------------|----------|------------------------------------|
| `Id`           | `uuid`                      | NOT NULL | PK                                 |
| `Email`        | `text`                      | NOT NULL |                                    |
| `PasswordHash` | `text`                      | NULL     | NULL for Google-only accounts      |
| `DisplayName`  | `text`                      | NOT NULL |                                    |
| `GoogleId`     | `text`                      | NULL     | NULL for email/password accounts   |
| `Role`                | `integer`                   | NOT NULL | Platform role. 0=User, 1=Admin, 2=SuperAdmin. Removed in 40.6 and reinstated at the same value on 2026-08-16, so no stored row changes meaning (`docs/DECISIONS.md`) |
| `AvatarType`          | `integer`                   | NOT NULL | 0=Default, 1=Uploaded (default 0)  |
| `AvatarKey`           | `text`                      | NULL     | S3 object key for uploaded avatar; NULL when using a default |
| `DefaultAvatarIndex`  | `integer`                   | NOT NULL | Index into `DefaultAvatars` catalog (default 0) |
| `IsEmailVerified`     | `boolean`                   | NOT NULL | Email confirmed via code (default false; existing rows backfilled true; Google accounts auto-true) |
| `CreatedAt`           | `timestamp with time zone`  | NOT NULL |                                    |

---

### `EmailVerificationCodes`

Short-lived registration verification codes. One active row per email (a new request replaces
the old). Only the code hash is stored. See [EMAIL_VERIFICATION.md](EMAIL_VERIFICATION.md).

| Column         | Type                       | Nullable | Notes                              |
|----------------|----------------------------|----------|------------------------------------|
| `Id`           | `uuid`                     | NOT NULL | PK                                 |
| `Email`        | `text`                     | NOT NULL | Normalized lowercase               |
| `CodeHash`     | `text`                     | NOT NULL | SHA-256 hex of the numeric code    |
| `ExpiresAt`    | `timestamp with time zone` | NOT NULL | Default 10 min after creation      |
| `AttemptCount` | `integer`                  | NOT NULL | Wrong-try counter; invalidated at the configured max |
| `CreatedAt`    | `timestamp with time zone` | NOT NULL | Drives the resend cooldown         |

Indexes: `IX_EmailVerificationCodes_Email`. Expired rows are purged by the daily Hangfire job
`expired-email-verification-cleanup`.

---

### `DefaultAvatars`

Catalog of bundled default avatar images stored in S3. Seeded by the admin; users pick one by index.

| Column      | Type                       | Nullable | Notes                              |
|-------------|----------------------------|----------|------------------------------------|
| `Id`        | `uuid`                     | NOT NULL | PK                                 |
| `Index`     | `integer`                  | NOT NULL | UNIQUE — display order / picker index |
| `ObjectKey` | `text`                     | NOT NULL | S3 object key, e.g. `defaults/avatar-03.png` |
| `CreatedAt` | `timestamp with time zone` | NOT NULL |                                    |

Indexes: `IX_DefaultAvatars_Index` (unique).

---

### `RefreshTokens`

| Column      | Type                       | Nullable | Notes                        |
|-------------|----------------------------|----------|------------------------------|
| `Id`        | `uuid`                     | NOT NULL | PK                           |
| `UserId`    | `uuid`                     | NOT NULL | FK → `Users.Id` ON DELETE CASCADE |
| `Token`     | `text`                     | NOT NULL |                              |
| `ExpiresAt` | `timestamp with time zone` | NOT NULL |                              |
| `IsRevoked` | `boolean`                  | NOT NULL |                              |

Indexes: `IX_RefreshTokens_UserId`

---

### `Memberships` (Phase 40.6)

A user's role within one organization — split out of the old global `Users.Role` (which
kept `Admin`, now removed). Composite PK from day one, even though the current UI only
ever creates one row per user: retrofitting a join table later would mean rewriting the
JWT, every authorization check and the invite flow at once. See
[docs/TENANCY/TENANCY.md](TENANCY/TENANCY.md) §4.2 and `docs/DECISIONS.md` (2026-08-15).

| Column           | Type                        | Nullable | Notes                              |
|------------------|------------------------------|----------|------------------------------------|
| `UserId`         | `uuid`                       | NOT NULL | PK (part 1). FK → `Users.Id` ON DELETE CASCADE (same database) |
| `OrganizationId` | `uuid`                       | NOT NULL | PK (part 2). **No FK** — bare uuid; `organization-service` owns the registry in its own database (DB-per-service) |
| `Role`           | `integer`                    | NOT NULL | Org-scoped role. 0=Manager, 1=TenancyAdmin, 2=TenancySuperAdmin. `OrgAdmin` → `TenancyAdmin` was a source-level rename at the same value on 2026-08-16 — **no data migration** |
| `Status`         | `integer`                    | NOT NULL | 0=Active (default), 1=Deactivated. Offboarding sets `Deactivated`, never deletes — attempt history/scores belong to the organization |
| `InvitedBy`      | `uuid`                       | NULL     | The inviting user's id (40.7 invite flow; not populated yet) |
| `JoinedAt`        | `timestamp with time zone`  | NOT NULL |                                    |
| `DeactivatedAt`   | `timestamp with time zone`  | NULL     |                                    |

Indexes: `IX_Memberships_OrganizationId`. `UserId` needs no separate index — it is the
leading column of the composite PK (leftmost-prefix rule).

A user with no `Memberships` row has no organization access — absent membership is never
implicit privilege. Migrating existing users into a default organization's `Memberships`
is 40.9's job, not this table's; the schema is nullable/backfillable where needed (`InvitedBy`)
to leave that migration room.

---

### `Invites` (Phase 40.7)

A single-use invitation to join one organization with one organization role. The product has no
public registration route ([TENANCY.md](TENANCY/TENANCY.md) §4.1), so this row is the only way a user
account or a `Memberships` row ever comes into existence outside the platform superadmin panel — which
makes it the whole of Phase 40.7 and the reason the `OrganizationAuthConfigurations` note below can
say "unlike `Invites`".

| Column           | Type                        | Nullable | Notes                              |
|------------------|------------------------------|----------|------------------------------------|
| `Id`             | `uuid`                       | NOT NULL | PK                                 |
| `OrganizationId` | `uuid`                       | NOT NULL | The inviting organization. **No FK** — `organization-service` owns the registry in its own database, same as `Memberships` |
| `Email`          | `varchar(320)`               | NOT NULL | Lower-cased invitee address, matched against the globally unique `Users.Email` |
| `Role`           | `integer`                    | NOT NULL | The org-scoped role the invitee will get. Same vocabulary as `Memberships.Role`: 0=Manager, 1=TenancyAdmin, 2=TenancySuperAdmin |
| `TokenHash`      | `varchar(64)`                | NOT NULL | **Only the hash is stored.** The raw token is returned once, at creation, in the response and the invite email — so a database dump never yields a usable invite. The same shape `RefreshToken.Token` and `EmailVerificationCode.CodeHash` already use |
| `ExpiresAt`      | `timestamp with time zone`   | NOT NULL |                                    |
| `AcceptedAt`     | `timestamp with time zone`   | NULL     | Set once; a second acceptance is rejected rather than ignored |
| `RevokedAt`      | `timestamp with time zone`   | NULL     | Revocation is a timestamp, not a delete — an admin should still see that the invite existed |
| `InvitedBy`      | `uuid`                       | NULL     | The issuing user. Nullable so a platform-side bootstrap invite — the first `TenancySuperAdmin` of a brand new organization (40.9) — has somewhere to land |
| `CreatedAt`      | `timestamp with time zone`   | NOT NULL |                                    |

Indexes: `IX_Invites_TokenHash` (UNIQUE) and `IX_Invites_OrganizationId_Email`. The unique one is
deliberately **not** organization-first, breaking the tenant-scoped index rule in
[TENANCY.md](TENANCY/TENANCY.md) §3, because acceptance finds the invite by hash alone — the caller
has no organization yet. The listing index follows the rule normally.

**RLS:** `EnableTenantRls("Invites")` (strict equality, not the content flavour — there is no such
thing as a global invite), plus the EF query filter and the `TenantSaveChangesInterceptor` write
guard. One organization can therefore never see or revoke another's invites.

Acceptance is the single deliberate exception to "the organization never comes from the request", and
it is worth being precise about why it is still safe: `InviteService.AcceptAsync` reads the
organization from the **HMAC signature of the token itself** (`InviteTokenFactory`), before touching
the database, and calls `TenantContext.SetOrganization` with it. The value is not attacker-chosen —
forging it means forging the signature. If the request already carries an organization context and it
disagrees with the token's, the invite is reported as not found rather than accepted.

---

### `OrganizationAuthConfigurations` (Phase 40.8)

How one organization's people sign in — the roadmap's `organization_auth_config`. In **identity-db**
rather than `organization`-db because it is read on `POST /auth/login/start`, before
authentication: a cross-service call there would put login behind another service's availability.
See [docs/TENANCY/TENANCY.md](TENANCY/TENANCY.md) §4.5 and `docs/DECISIONS.md` (2026-08-15, 40.8).

| Column                                | Type                        | Nullable | Notes                              |
|---------------------------------------|------------------------------|----------|------------------------------------|
| `OrganizationId`                      | `uuid`                       | NOT NULL | PK. **No FK** — bare uuid; `organization-service` owns the registry (DB-per-service) |
| `Method`                              | `character varying(32)`      | NOT NULL | `password` \| `oidc` \| `saml`, enforced by `CK_OrganizationAuthConfigurations_Method`. Stored as text so the database, `IAuthProvider.Method` and the JSON all read the same |
| `ProviderSettings`                    | `jsonb`                      | NULL     | Provider-specific (issuer, client id, metadata URL, certificate). Null while the method is `password` |
| `AllowedEmailDomains`                 | `text[]`                     | NOT NULL | Domains that map to this organization at login step 1. Empty = reachable only through an existing membership |
| `IsJustInTimeProvisioningEnabled`     | `boolean`                    | NOT NULL | Reserved for SSO. **Stored, never read** — provisioning stays invite-only |
| `SessionLifetime`                     | `interval`                   | NULL     | Per-organization override; null = `Jwt:RefreshTokenLifetimeDays`. **Stored, not yet applied** |
| `IsMultiFactorAuthenticationRequired` | `boolean`                    | NOT NULL | Reserved for SSO/MFA. **Stored, never read** |
| `CreatedAt`                           | `timestamp with time zone`   | NOT NULL |                                    |

Indexes: `IX_OrganizationAuthConfigurations_AllowedEmailDomains` (**GIN**) — the domain lookup on
the first login step asks "which organization claims this domain", which no b-tree helps with.

**No row-level security**, deliberately, unlike `Invites`. The table is not `ITenantScoped`; its
main read is a cross-tenant question asked with no tenant context, and system mode would depend
on a `BYPASSRLS` role that does not exist on real servers yet. A future write path must therefore
scope by `ITenantContext` explicitly in the query.

An organization with no row here signs in with a password — the same answer an unknown address
gets, which is what keeps login step 1 non-enumerable. There is still no write endpoint; the first
rows arrive with the 40.9 data migration
(`docs/TENANCY/sql/40.9_default_organization_backfill_identity_db.sql`), which seeds one row with
`method = password` and deliberately **empty** `AllowedEmailDomains` — claiming a domain would
route every address at that domain to that organization, which is wrong the moment a second
customer shares a mail provider.

---

### `OrganizationReplicas` (Phase 40.9)

identity-service's read-only projection of the tenant registry owned by `organization-service`,
kept current over Kafka (`organization.created` / `.updated` / `.suspended`). The same shape as
`UserReplica` elsewhere: a bare uuid key, no FK, eventual consistency accepted on purpose
([TENANCY.md §1.1](TENANCY/TENANCY.md)).

| Column           | Type                       | Nullable | Notes                                                          |
|------------------|----------------------------|----------|----------------------------------------------------------------|
| `OrganizationId` | `uuid`                     | NOT NULL | PK. **No FK** — `organization-service` owns the registry        |
| `Name`           | `character varying(200)`   | NOT NULL |                                                                 |
| `Slug`           | `character varying(100)`   | NOT NULL |                                                                 |
| `Status`         | `integer`                  | NOT NULL | `0 = Active`, `1 = Suspended`. Stored as an int here; the registry itself stores the same value as **text** — the two are not interchangeable, each side uses its own representation |
| `UpdatedAt`      | `timestamp with time zone` | NOT NULL |                                                                 |

It exists because identity-service is the only service that mints tokens, and a suspended
organization has to stop producing them; a synchronous call to `organization-service` on every
login would put authentication behind another service's availability.

**No row-level security**, for the same reason as `OrganizationAuthConfigurations`: it is read
while deciding whether a token may be issued at all, before there is a tenant context to filter by.

**A missing row means active, not suspended.** The projection is eventually consistent, and a
consumer that is briefly behind must not lock a paying customer out of their own product.

---

### `ImpersonationAuditEntries` (Phase 40.9)

Append-only record of a platform superadmin minting a token for someone else's organization. The
row is written and committed *before* the token is returned, so a token that exists always has a
record behind it.

| Column             | Type                       | Nullable | Notes                                                     |
|--------------------|----------------------------|----------|-----------------------------------------------------------|
| `Id`               | `uuid`                     | NOT NULL | PK                                                        |
| `ActorUserId`      | `uuid`                     | NOT NULL | The platform staff member who asked                       |
| `ActorEmail`       | `character varying(320)`   | NOT NULL | Copied at write time, so the row still reads correctly after a rename |
| `OrganizationId`   | `uuid`                     | NOT NULL | **No FK** — cross-service reference                        |
| `OrganizationName` | `character varying(200)`   | NOT NULL | Copied for the same reason as `ActorEmail`                |
| `Reason`           | `character varying(500)`   | NOT NULL | Required. An impersonation with no stated reason is the one nobody can review afterwards |
| `IssuedAt`         | `timestamp with time zone` | NOT NULL |                                                           |
| `ExpiresAt`        | `timestamp with time zone` | NOT NULL | The true end of the session — impersonation tokens have no refresh companion |

Indexes: `IX_ImpersonationAuditEntries_IssuedAt` (DESC) for "who went in recently";
`IX_ImpersonationAuditEntries_OrganizationId_IssuedAt` (org, issued DESC) for "who has been inside
this organization".

**No row-level security**: the record exists to describe crossings *between* tenants, and its
readers are platform staff, not the organization named in it. Nothing in the codebase updates or
deletes a row.

---

### `UserProfiles`

| Column                  | Type      | Nullable | Notes                                                            |
|-------------------------|-----------|----------|------------------------------------------------------------------|
| `Id`                    | `uuid`    | NOT NULL | PK                                                               |
| `UserId`                | `uuid`    | NOT NULL | FK → `Users.Id` (no cascade configured)                         |
| `SalesType`             | `text`    | NOT NULL | `b2b_saas` / `retail` / `real_estate` / `finance` / `b2c`       |
| `ExperienceLevel`       | `text`    | NOT NULL | `beginner` / `experienced` / `manager`                          |
| `Goal`                  | `text`    | NOT NULL | e.g. `close_deals` / `cold_calls` / `everything`                |
| `IsOnboardingCompleted` | `boolean` | NOT NULL |                                                                  |
| `Persona`               | `text`    | NULL     | `sdr` / `account_executive` / `account_manager` / `founder` / `other` |

---

### `Skills`

| Column        | Type      | Nullable | Notes                          |
|---------------|-----------|----------|--------------------------------|
| `Id`          | `uuid`    | NOT NULL | PK                             |
| `OrganizationId` | `uuid` | NULL | Phase 40.10 — `NULL` = global library shared by every organization; non-null = one organization's own copy (40.18). RLS policy `Skills_tenant_isolation` (content variant: `IS NULL OR = current`). |
| `IconicName`  | `text`    | NOT NULL | English identifier. UNIQUE **per organization** since 40.10 — see the index note below |
| `OrderInTree` | `integer` | NOT NULL | Display order in tree          |
| `Title`       | `text`    | NOT NULL | Localized display name         |
| `Description` | `text`    | NULL     |                                |
| `Stage`       | `text`    | NOT NULL | Funnel stage bucket (DEFAULT `general`). References `SkillStages.Key`; built-in keys: `preparation`, `discovery`, `engagement`, `closing`, `retention`. Free string (no FK) — `general` and unknown keys fall back to a generic bucket. |

Indexes (Phase 40.10): `IX_Skills_OrganizationId_IconicName` (unique), `IX_Skills_IconicName_Global`
(unique, partial `WHERE "OrganizationId" IS NULL`), `IX_Skills_OrganizationId_Stage`. The slug is
unique **per organization**, not globally — otherwise a second customer could not have its own
`objections` skill. The partial index is not redundant: Postgres treats NULLs in a composite unique
index as distinct, so without it two global `objections` skills would be allowed. Built by
`docs/TENANCY/sql/40.10_learning_organization_indexes_concurrently.sql`, not by the EF migration.

### `SkillStages`

The configurable funnel-stage list used to group skills on `/tree` (replaces the previously frontend-hardcoded list). Seeded by migration `20260616132237_AddSkillStages` with the original 5 stages (`preparation/discovery/engagement/closing/retention`). Managed via `/admin/skill-stages`; read publicly (ordered by `Order`) at `GET /skills/stages`. `Skills.Stage` references `Key` (free string, no FK constraint).

| Column   | Type          | Nullable | Notes                                                  |
|----------|---------------|----------|--------------------------------------------------------|
| `Id`     | `uuid`        | NOT NULL | PK                                                     |
| `Key`    | `varchar(40)` | NOT NULL | unique slug, immutable (stored on `Skills.Stage`)      |
| `Label`  | `varchar(60)` | NOT NULL | display label                                          |
| `Accent` | `varchar(40)` | NOT NULL | CSS color (hex or `var(--token)`)                      |
| `Order`  | `integer`     | NOT NULL | display order along the funnel (ascending)             |

Index: `IX_SkillStages_Key` (unique). `general` is the implicit fallback for unassigned/unknown keys and is intentionally not a stored row.

---

### `Topics`

| Column        | Type      | Nullable | Notes                          |
|---------------|-----------|----------|--------------------------------|
| `Id`          | `uuid`    | NOT NULL | PK                             |
| `OrganizationId` | `uuid` | NULL | Phase 40.10 — `NULL` = global library shared by every organization; non-null = one organization's own copy (40.18). RLS policy `Topics_tenant_isolation` (content variant: `IS NULL OR = current`). |
| `SkillId`     | `uuid`    | NOT NULL | FK → `Skills.Id`               |
| `IconicName`  | `text`    | NOT NULL | English identifier. UNIQUE **per organization** since 40.10 — see the index note below |
| `OrderInSkill`| `integer` | NOT NULL |                                |
| `Title`       | `text`    | NOT NULL | Localized display name         |

Indexes: `IX_Topics_IconicName`, `IX_Topics_SkillId_OrderInSkill`

---

### `Lessons`

| Column        | Type      | Nullable | Notes                |
|---------------|-----------|----------|----------------------|
| `Id`          | `uuid`    | NOT NULL | PK                   |
| `OrganizationId` | `uuid` | NULL | Phase 40.10 — `NULL` = global library shared by every organization; non-null = one organization's own copy (40.18). RLS policy `Lessons_tenant_isolation` (content variant: `IS NULL OR = current`). |
| `ParentLessonId` | `uuid` | NULL | Phase 40.15 — set when this row is one organization's override of a global lesson; FK → `Lessons.Id` `ON DELETE RESTRICT`. **Written since 40.18** by copy-on-write override creation. `RESTRICT` rather than `CASCADE`/`SET NULL`: a global lesson three customers have overridden must not be deletable in one click, and silently promoting the overrides to standalone lessons would lose the fact that they were ever derived. `CK_Lessons_OverrideHasOwner` (40.18): a row with a parent always has an `OrganizationId` — a global row overriding a global row would hide the shared library behind a copy of itself. |
| `Slug`        | `varchar(160)` | NOT NULL | Phase 40.15 — stable identifier within the organization. |
| `IsArchived`  | `boolean` | NOT NULL | Phase 40.15, default `false`. Retired lessons stay in the table because published versions and historical progress point at them. **40.18** also uses it for the review action "take the new base": the override is archived, read resolution stops shadowing the global row, and no history is orphaned. |
| `TopicId`     | `uuid`    | NOT NULL | FK → `Topics.Id`     |
| `OrderInTopic`| `integer` | NOT NULL |                      |
| `Title`       | `text`    | NOT NULL |                      |

Indexes: `IX_Lessons_OrganizationId_TopicId_OrderInTopic`, `IX_Lessons_TopicId`,
`IX_Lessons_OrganizationId_Slug` (UNIQUE), `IX_Lessons_Slug_Global` (UNIQUE, `WHERE "OrganizationId" IS NULL`),
`IX_Lessons_ParentLessonId`

The pre-40.10 `IX_Lessons_TopicId_OrderInTopic` is gone: every read now filters on `OrganizationId`
first, so 40.10 rebuilt the index organization-first and
`docs/TENANCY/sql/40.10_learning_organization_indexes_concurrently.sql` drops the old one
explicitly (a bare `IX_Lessons_TopicId` is kept for the lookups that have no ordering).

> **Two slug indexes, not one.** In a composite unique index Postgres treats NULLs as **distinct**,
> so `UNIQUE (OrganizationId, Slug)` does **not** stop two *global* lessons sharing a slug. The
> partial index over the global rows is what preserves that guarantee, while the composite one lets
> a second customer have its own `objections`. Exactly the pattern already used for
> `Skill.IconicName`, `Topic.IconicName` and `Technique.Slug` (TENANCY.md §1.9).

> **Slug backfill.** Existing lessons were given `'lesson-' || replace("Id"::text, '-', '')` by the
> migration itself — derived from each row's own primary key, so unique by construction and needing
> no separate maintenance window. Titles are Russian prose and nothing transliterates them; a
> readable slug is an explicit rename by an admin, which is safe because nothing routes by the slug.
> The generated form is duplicated in `LessonSlugGenerator.GenerateFromLessonId`; if one changes, so
> must the other.

---

### `LessonVersions`

Phase 40.15. Immutable snapshots of a lesson **together with its full ordered set of exercises**
(docs/TENANCY/CONTENT_MODEL.md §2). The versioned unit is the whole lesson because a `Lessons` row
has no body of its own — only a title — and because versioning each `Exercise` separately would turn
every historical question into a reconstruction from N rows.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `Id` | `uuid` | NOT NULL | PK |
| `OrganizationId` | `uuid` | NULL | Denormalized copy of the owning lesson's organization. `NULL` = global library. Denormalized because an RLS policy can only compare columns of the row it filters, so the boundary needs the value here and not one join away. RLS policy `LessonVersions_tenant_isolation` (content variant: `IS NULL OR = current`). |
| `LessonId` | `uuid` | NOT NULL | FK → `Lessons.Id` `ON DELETE CASCADE` |
| `VersionNumber` | `integer` | NOT NULL | Monotonic per lesson, from 1 |
| `Content` | `jsonb` | NOT NULL | The snapshot — see the shape below |
| `ContentHash` | `varchar(64)` | NOT NULL | Lowercase hex SHA-256 of the **canonical** `Content`, UTF-8 |
| `Status` | `varchar(16)` | NOT NULL | `draft` \| `published` \| `archived`; default `draft`; `CK_LessonVersions_Status` |
| `BaseVersionId` | `uuid` | NULL | Which version of the parent lesson an override was forked from. FK → `LessonVersions.Id` `ON DELETE SET NULL` |
| `IsBreaking` | `boolean` | NOT NULL | Cosmetic (`false`) or semantic (`true`) edit |
| `CreatedBy` | `uuid` | NULL | The administrator who opened the draft |
| `CreatedAt` | `timestamptz` | NOT NULL | |
| `PublishedAt` | `timestamptz` | NULL | `CK_LessonVersions_PublishedAt`: NOT NULL whenever `Status = 'published'` |

Indexes: `IX_LessonVersions_LessonId_VersionNumber` (UNIQUE),
`IX_LessonVersions_LessonId_Draft` (UNIQUE, `WHERE "Status" = 'draft'`),
`IX_LessonVersions_OrganizationId_LessonId_VersionNumber`,
`IX_LessonVersions_BaseVersionId`

**Snapshot shape** (every object's keys in ordinal order — that is what makes the hash reproducible):

```json
{
  "exercises": [
    { "content": {}, "customAiPrompt": null, "exerciseId": "...", "orderInLesson": 1, "type": "choose_option" }
  ],
  "schemaVersion": 1,
  "title": "..."
}
```

> **The hash is over the canonical form, not over what the database returns.** `Content` is `jsonb`,
> so Postgres re-normalizes it on write (its own key ordering, its own whitespace) and a `SELECT`
> gives back something equivalent to but not byte-identical with what was hashed. Recomputing
> `ContentHash` from a `SELECT` will not match; it is defined over the output of
> `LessonSnapshotSerializer`.

> **Why canonicalize at all.** Without sorting object keys before hashing, an admin panel that
> re-serialized an exercise's content with its keys in a different order would look like a content
> change on every save — and `content_hash` exists precisely so that pressing "publish" with nothing
> changed does not mint a version. Array order is preserved (it is meaningful in exercise content);
> numbers are written through unchanged, which is the one accepted gap, since normalizing `1` and
> `1.0` would mean picking a numeric model and rewriting customer content to fit it.

> **`exerciseId` is inside the snapshot and therefore inside the hash.** That is the identity 40.16
> needs to say *which exercise inside which version* an attempt answered. The accepted cost: deleting
> an exercise row and recreating it with identical content produces a new hash and so a new version.
> That is the honest answer — the identity really did change.

**Frozen after publication, in the database.** Trigger
`LessonVersions_reject_frozen_change` (`BEFORE UPDATE`) refuses any change to `Content`,
`ContentHash`, `VersionNumber`, `LessonId`, `OrganizationId`, `IsBreaking` or `PublishedAt` once the
row has left `draft`, and refuses `published → draft` and any exit from `archived`. It is in the
database rather than in the service because a snapshot that can be edited afterwards is not a
snapshot: every historical attempt scored against it would silently re-interpret, which is exactly
the metric corruption 40.16 is being written to fix. `BaseVersionId` and `Status` stay writable on a
frozen row on purpose — 40.18's stale-override review offers "keep the override, re-point its base"
as one of its three actions, and archiving a version is a lifecycle move rather than a rewrite.

**At most one draft per lesson**, enforced by the partial unique index rather than by application
code: two admins pressing "edit" at the same moment is precisely the race a check-then-insert loses,
and two drafts are two branches of a lesson with no merge story (CONTENT_MODEL.md §2.6 — merging
prose and grading criteria automatically produces plausible nonsense that then grades a salesperson).

---

### `ProgramVersions`, `ProgramItems`, `ProgramEnrollments`

Phase 40.17. One organization's curriculum, frozen at a point in time, and who is standing on which
frozen copy (docs/TENANCY/CONTENT_MODEL.md §2.5).

All three are **strict tenant data**, unlike everything else in this file that came out of Stage D.
There is no such thing as a global programme: a curriculum is a decision one organization made about
its own people, so `OrganizationId` is `NOT NULL` and every policy is plain equality rather than the
content flavour `IS NULL OR = current`. A `NULL` owner here would mean "everybody's programme".

#### `ProgramVersions`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `Id` | `uuid` | NOT NULL | PK |
| `OrganizationId` | `uuid` | NOT NULL | RLS policy `ProgramVersions_tenant_isolation` (strict equality) |
| `VersionNumber` | `integer` | NOT NULL | Monotonic per organization, from 1 |
| `Status` | `varchar(16)` | NOT NULL | `draft` \| `published` \| `archived`; default `draft`; `CK_ProgramVersions_Status` |
| `CreatedBy` | `uuid` | NULL | The administrator who opened the draft |
| `CreatedAt` | `timestamptz` | NOT NULL | |
| `PublishedAt` | `timestamptz` | NULL | `CK_ProgramVersions_PublishedAt`: NOT NULL whenever `Status = 'published'` |

Indexes: `IX_ProgramVersions_OrganizationId_VersionNumber` (UNIQUE),
`IX_ProgramVersions_OrganizationId_Draft` (UNIQUE, `WHERE "Status" = 'draft'`).

#### `ProgramItems`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `Id` | `uuid` | NOT NULL | PK |
| `OrganizationId` | `uuid` | NOT NULL | Denormalized from the owning version, for the same reason `LessonVersions` denormalizes it: an RLS policy can only compare columns of the row it filters |
| `ProgramVersionId` | `uuid` | NOT NULL | FK → `ProgramVersions.Id` `ON DELETE CASCADE` |
| `SkillId` | `uuid` | NOT NULL | Reference only, **no FK** |
| `LessonId` | `uuid` | NOT NULL | Denormalized from the pinned snapshot. Reference only, **no FK** |
| `LessonVersionId` | `uuid` | NOT NULL | **The pin.** Reference only, **no FK** |
| `OrderIndex` | `integer` | NOT NULL | Position in the running order, zero-based, dense within a version; `CK_ProgramItems_OrderIndex` (`>= 0`) |

Indexes: `IX_ProgramItems_ProgramVersionId_LessonId` (UNIQUE),
`IX_ProgramItems_OrganizationId_ProgramVersionId_OrderIndex`,
`IX_ProgramItems_LessonVersionId`.

> **No foreign key on the three content references, on purpose.** `Skills`, `Lessons` and
> `LessonVersions` are content tables under an `IS NULL OR = current` policy, while this is strict
> tenant data under plain equality — the same call 40.16 made for
> `UserExerciseAttempts.LessonVersionId`: a constraint spanning the two is validated with the
> writer's privileges and would either leak the existence of rows the writer may not read or refuse
> writes it may. `docs/TENANCY/sql/40.17_program_versioning_verify.sql` checks by query what the
> constraint would have checked.

> **`LessonId` is denormalized deliberately, and it earns its column.** Without it, "the same lesson
> is now pinned to a different version" is inexpressible: the unique index above would only stop the
> same *version* appearing twice, not the same lesson appearing at versions 3 and 5 inside one
> curriculum — the same material with two answer keys. It cannot drift, because
> `LessonVersion.LessonId` is frozen by 40.15's trigger.

> **The pin survives 40.18.** That block re-points `LessonVersions.BaseVersionId` on frozen rows.
> `BaseVersionId` is provenance, not identity; a published lesson version is immutable and its `Id`
> never moves, so nothing 40.18 does can change what a programme item points at.

#### `ProgramEnrollments`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `Id` | `uuid` | NOT NULL | PK |
| `OrganizationId` | `uuid` | NOT NULL | RLS policy `ProgramEnrollments_tenant_isolation` (strict equality) |
| `UserId` | `uuid` | NOT NULL | |
| `ProgramVersionId` | `uuid` | NOT NULL | FK → `ProgramVersions.Id` `ON DELETE RESTRICT` — a version somebody is standing on is not something to delete, and refusing is a better answer than unpinning a learner mid-course |
| `PreviousProgramVersionId` | `uuid` | NULL | Where the learner was before their last explicit switch |
| `EnrolledAt` | `timestamptz` | NOT NULL | |
| `SwitchedAt` | `timestamptz` | NULL | |

Indexes: `IX_ProgramEnrollments_OrganizationId_UserId` (UNIQUE — one pin per learner per
organization; the same person may belong to two organizations and then holds one pin in each),
`IX_ProgramEnrollments_ProgramVersionId`.

**Frozen after publication, in the database — structure included.** Two triggers.
`ProgramVersions_reject_frozen_change` (`BEFORE UPDATE`) refuses any change to `VersionNumber`,
`OrganizationId` or `PublishedAt` once the row has left `draft`, refuses `published → draft` and any
exit from `archived`. `ProgramItems_reject_frozen_change` (`BEFORE INSERT OR UPDATE OR DELETE`) is
the one that matters: the structure lives in those rows, so that is where a retroactive reorder would
actually be written, and removing a lesson from a frozen programme is the same edit seen from the
other side. A cascade from deleting the programme version itself is allowed through by the "parent
row is already gone" branch — Postgres runs `ON DELETE CASCADE` after the parent row is deleted, so
a lookup that finds nothing means exactly that.

The reason to put it in the database is sharper than it was for lessons: a lesson version edited
after the fact corrupts a metric, while a programme version edited after the fact rearranges the
curriculum under somebody who is on lesson 8 of 21 — the failure the block is named for.

**No backfill, and no "programme version 1".** The migration creates no programme and enrolls
nobody. 40.16 did mint a lesson's version 1, because the lesson body existed and only the snapshot
was missing; a programme version is not a snapshot of something that exists but a curriculum
decision nobody has made yet. Absent enrollment, learners read the live tree exactly as they did
before — see [DECISIONS.md](DECISIONS.md), 2026-08-17.

---

### `Assignments`, `AssignmentProgressRecords`

Phase 40.21 (thresholds and their evaluation: 40.22; issuing and the deadline notice: 40.23; automatic repeats: 40.24), the first tables of Stage E. What the РОП asks their team to practise after an internal
training, and where each person stands on it (docs/TENANCY/ASSIGNMENTS.md §1).

Both are **strict tenant data**, like the programme tables above and unlike the content tables: there
is no such thing as a global assignment, so `OrganizationId` is `NOT NULL` and both policies are plain
equality rather than the content flavour `IS NULL OR = current`. A `NULL` owner here would mean
"everybody's homework".

> **Why this is not a `ProgramVersion` with a deadline column.** The programme is a long, sequential,
> self-paced curriculum somebody walks over months and is pinned to; an assignment is days long, aimed
> at named people, issued after one training session, and worthless once its deadline passes. Sharing
> a table would give the curriculum a deadline it has no meaning for and give the assignment a
> version-and-pin lifecycle nobody wants for a five-day task.

#### `Assignments`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `Id` | `uuid` | NOT NULL | PK |
| `OrganizationId` | `uuid` | NOT NULL | RLS policy `Assignments_tenant_isolation` (strict equality) |
| `CreatedBy` | `uuid` | NULL | The РОП. Null for rows a background path creates — 40.24's repeats have no human pressing anything |
| `Title` | `varchar(200)` | NOT NULL | |
| `Goal` | `varchar(2000)` | NULL | Shown to the team, never parsed |
| `SourceType` | `varchar(16)` | NOT NULL | `training` \| `manual` \| `gap_detected`; `CK_Assignments_SourceType` |
| `SourceRef` | `varchar(200)` | NULL | Read according to `SourceType`, always `<kind>:<id>`. `training` → `lesson-version:<uuid>` (frozen, never `lesson:`); `gap_detected` → `skill-gap:<stage>@<yyyy-MM-dd>` (**40.31** — the funnel stage the team failed and the day it was measured; the numbers themselves go into `Goal`); `manual` → null, by `CK_Assignments_ManualHasNoSourceRef` |
| `Content` | `jsonb` | NOT NULL | `{"items":[{"kind","reference","orderIndex"}]}` — **references only**; `CK_Assignments_Content` (must be an object) |
| `Audience` | `jsonb` | NOT NULL | `{"kind":"whole_team"}` \| `{"kind":"users","userIds":[…]}` \| `{"kind":"group","groupId":…}`; `CK_Assignments_Audience` (object carrying `kind`) |
| `OpensAt` | `timestamptz` | NULL | Null means "as soon as it is active" |
| `Deadline` | `timestamptz` | NULL | `CK_Assignments_Schedule`: strictly after `OpensAt` when both are present |
| `CompletionRule` | `jsonb` | NOT NULL | **No default.** `CK_Assignments_CompletionRule`: an object carrying a `kind`. Since 40.22 the service also refuses anything outside the vocabulary below |
| `RepeatSchedule` | `jsonb` | NULL | Null = one-shot; otherwise an object carrying a `kind` (`CK_Assignments_RepeatSchedule`). Since 40.24 the service also refuses anything outside the vocabulary below. `CK_Assignments_RepeatNoCascade`: always null on a repeat |
| `Status` | `varchar(16)` | NOT NULL | `draft` \| `active` \| `closed`; default `draft`; `CK_Assignments_Status` |
| `CreatedAt` | `timestamptz` | NOT NULL | |
| `UpdatedAt` | `timestamptz` | NOT NULL | |
| `ActivatedAt` | `timestamptz` | NULL | `CK_Assignments_ActivatedAt`: NOT NULL whenever the status is not `draft` |
| `ClosedAt` | `timestamptz` | NULL | `CK_Assignments_ClosedAt`: NOT NULL whenever the status is `closed` |
| `DeadlineNoticeSentAt` | `timestamptz` | NULL | **Phase 40.23**, reused unchanged by 40.26. When the "deadline is close" notice went out for the deadline this row *currently* has — since 40.26 that means both the notices to the people who owe the work **and** the digest to the organization's administrators, because they are published in one transaction and announce the same date. Cleared whenever `Deadline` changes. No constraint: an assignment with no deadline simply never gets stamped |
| `RepeatOfAssignmentId` | `uuid` | NULL | **Phase 40.24.** The assignment this row is a repeat of; null on anything a human created. FK → `Assignments.Id` **`ON DELETE RESTRICT`** (self-referencing). Always the *origin*, never another repeat |
| `RepeatWaveIndex` | `int` | NULL | **Phase 40.24.** Which wave of the origin's schedule this is, 1-based. `CK_Assignments_RepeatWave`: null exactly when `RepeatOfAssignmentId` is, and ≥ 1 otherwise |

Indexes: `IX_Assignments_OrganizationId_Status_Deadline`, `IX_Assignments_OrganizationId_CreatedAt`,
`IX_Assignments_RepeatOfAssignmentId_RepeatWaveIndex` (**unique**, partial on
`RepeatOfAssignmentId IS NOT NULL`, 40.24).

> **The 40.24 unique index is the repeat sweep's entire idempotency story.** A wave has been issued
> exactly when its row exists — nothing is incremented, nothing is stamped — so two ticks racing inside
> one window collide on this index rather than issuing the same shortened work to the same team twice.
> The sent-ness column every other sweep in this feature uses (`DeadlineNoticeSentAt` two rows up) is
> not merely unnecessary here but **impossible**: the origin may be `closed`, and the freeze trigger
> refuses any update at all to a closed row, so a stamp-based design would throw on every closed
> origin — and skipping closed origins would mean that tidying up a finished five-day assignment
> silently cancels its refreshers.

> **It deliberately does not lead with `OrganizationId`**, the second such exception in this feature
> after `IX_AssignmentProgressRecords_AssignmentId_Status` below, and for the same two reasons: it is
> the only index covering the new self-referencing foreign key, so without it Postgres scans the whole
> table on every attempt to delete an assignment; and an origin id is globally unique already, so
> putting the organization in front would weaken the uniqueness rather than scope it. Isolation is
> decided by the RLS policy, never by an index.

> **Three 40.24 check constraints, and the middle one is load-bearing.**
> `CK_Assignments_RepeatWave` (both series columns or neither, wave ≥ 1),
> `CK_Assignments_RepeatNoCascade` (**a repeat carries no schedule of its own** — otherwise a repeat
> repeats itself and two waves each spawn two more, an exponential fan-out of progress rows and
> notifications in which every individual step looks exactly like the feature working), and
> `CK_Assignments_RepeatNotSelf`.

> **The freeze trigger gained the two series columns in 40.24** — which series a row belongs to and
> which wave it is are identity, and every recorded score is read through them. `RepeatSchedule` stays
> **out** of the frozen set on purpose: editing it on an active assignment is the only way a РОП can
> cancel waves that have not gone out yet.

> **`DeadlineNoticeSentAt` got no index of its own, and that is a decision.** The deadline sweep's
> enumeration is the one query in learning-service that filters without leading on `OrganizationId` —
> "which organizations have an unannounced deadline coming", asked across all of them — so an index
> for it would have to be a partial index on `(Deadline)`, the exact shape the convention since 40.10
> exists to prevent. Over a table that grows at the rate a human writes assignments the scan is
> cheaper than the exception; the tenant-leading index above serves every per-organization query that
> follows the enumeration.

> **The freeze trigger deliberately does not name it.** The sweep stamps active rows, so a frozen
> `DeadlineNoticeSentAt` would make an announced deadline unrecordable and the sweep would re-announce
> the same date every half hour, forever. `docs/TENANCY/sql/40.23_assignment_fanout_verify.sql` §2
> asserts the trigger body does not mention the column.

> **Phase 40.26 added no column, no index and no migration to this table, and that is a decision
> rather than an omission.** The block sends the РОП a digest of who has not started, a day before the
> deadline — which reads like it needs its own sent-ness column beside `DeadlineNoticeSentAt`. It does
> not: the digest is published by the same sweep, in the same transaction, about the same date, so one
> timestamp answers "has this deadline been announced" for both audiences and moving the deadline
> re-arms both at once. A second column would have been a second answer to one question, with its own
> chance of disagreeing. The one case a separate column would have handled — a tick that can read the
> roster but not the administrators — is handled instead by skipping the organization entirely and
> stamping nothing, which self-heals on the next tick. See
> [BACKGROUND_JOBS.md](TENANCY/BACKGROUND_JOBS.md) §4g. There is therefore no
> `docs/TENANCY/sql/40.26_*_indexes_concurrently.sql`, no maintenance window and nothing to back-fill;
> the read-only `docs/TENANCY/sql/40.26_deadline_digest_verify.sql` exists only to let an operator see
> what the sweep would send, because the РОП has no screen yet.

> **`CompletionRule` has no default, and that is the load-bearing decision of the whole block.** A
> default would have to mean "no threshold", and an assignment that completes on a click is the
> compliance-theatre failure [ASSIGNMENTS.md](TENANCY/ASSIGNMENTS.md) §1.1 is written to prevent:
> managers click through in four minutes, the dashboard reads 100%, and the number is a lie the РОП
> eventually catches. With no default and no way for the API to omit the field, that failure mode has
> no resting place in the schema. The database constraint asserts only that a `kind` is named; the
> *vocabulary* is 40.22's and lives in the service, because it is a product decision that will grow
> (40.24, 40.25) and a `CHECK` listing kinds would have to be migrated on every addition.

> **The 40.22 vocabulary, as stored.** Two kinds, both from the roadmap. `{"kind":"dialog_score",
> "minimumScore":70,"requiredCount":3}` — met once that many graded conversations have each cleared
> that bar. `{"kind":"exercise_accuracy","minimumAccuracyPercent":80}` — met once every exercise in
> the pinned `lesson_version` has been attempted and correct submissions ÷ all submissions clears the
> bar. Both numbers are 1–100 and a **zero bar is refused**: "score at least 0" is a threshold every
> click clears. `docs/TENANCY/sql/40.22_completion_threshold_verify.sql` fails a row whose `kind` is
> outside the vocabulary, because an unevaluable rule is indistinguishable on the dashboard from a
> team that has not started.

> **`Content` holds references and never an exercise body.** An assignment's exercises are ordinary
> exercises inside a pinned `LessonVersion` (`kind = "lesson_version"`, `reference` = a
> `LessonVersions.Id`), so their bodies stay in `Exercises.SerializedContent`, the eleven existing
> renderers play them with no new code, and there is no second grading path, no second override story
> and nothing for 40.19's substitution to forget. Pointing at mutable `Exercises.Id` values instead
> would repeat exactly the defect 40.16 removed from progress. The other two kinds are
> `dialog_scenario` (an ai-service dialog mode **key**, not a uuid — that is how ai-service addresses
> modes) and `reference_material` (a `ReferenceMaterials.Id`, ungraded theory).

> **No foreign key on any of those references, on purpose.** Same call as 40.16 and 40.17:
> `LessonVersions` and `ReferenceMaterials` are content tables under an `IS NULL OR = current` policy
> while `Assignments` is strict tenant data under plain equality, and a constraint spanning the two is
> validated with the writer's privileges — it would either leak the existence of rows the writer may
> not read or refuse writes it should allow. `docs/TENANCY/sql/40.21_assignments_verify.sql` checks by
> query what the constraint would have checked.

> **`SourceRef` names a frozen version, never a lesson.** When the source is library content the
> reference is written `lesson-version:<uuid>`; a `LessonId` would silently re-point at whatever the
> lesson has become, which is the defect 40.16 spent a block removing. The verify script fails a row
> whose `SourceRef` starts with `lesson:`.

> **`Audience` stores the rule, not the people.** The list of an organization's employees lives in
> identity-service (`Memberships`); learning-db holds only `UserReplicas`, which is platform-global and
> says nothing about who belongs where. A resolved list in this column would be a stale copy of
> somebody else's data the moment anybody is hired or leaves. 40.23 resolves the rule at issue time,
> and its output — the `AssignmentProgressRecords` rows — is the authoritative record of who actually
> got it.

#### `AssignmentProgressRecords`

The roadmap calls this table `assignment_progress`; the name follows the `UserLessonProgressRecords`
convention already in this database.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `Id` | `uuid` | NOT NULL | PK |
| `OrganizationId` | `uuid` | NOT NULL | Denormalized from the assignment, for the same reason `ProgramItems` denormalizes it: an RLS policy can only compare columns of the row it filters |
| `AssignmentId` | `uuid` | NOT NULL | FK → `Assignments.Id` **`ON DELETE RESTRICT`** |
| `UserId` | `uuid` | NOT NULL | |
| `Status` | `varchar(20)` | NOT NULL | `not_started` \| `in_progress` \| `completed` \| `failed_threshold`; default `not_started`; `CK_AssignmentProgressRecords_Status` |
| `BestScore` | `integer` | NULL | 0–100, `CK_AssignmentProgressRecords_BestScore`. Best rather than latest: a threshold cleared once stays cleared |
| `AttemptCount` | `integer` | NOT NULL | `CK_AssignmentProgressRecords_AttemptCount` (`>= 0`) |
| `FirstOpenedAt` | `timestamptz` | NULL | `CK_AssignmentProgressRecords_FirstOpenedAt`: NOT NULL for any status but `not_started` |
| `CompletedAt` | `timestamptz` | NULL | `CK_AssignmentProgressRecords_CompletedAt`: NOT NULL whenever the status is `completed` |

Indexes: `IX_AssignmentProgressRecords_OrganizationId_AssignmentId_UserId` (UNIQUE — one row per person
per assignment), `IX_AssignmentProgressRecords_OrganizationId_UserId_Status` (the manager's own list,
40.23), `IX_AssignmentProgressRecords_AssignmentId_Status`.

> **The last index deliberately does not lead with `OrganizationId`,** unlike every other index in this
> section. It serves two things that both need `AssignmentId` first: the funnel of one assignment
> (40.25) and the `ON DELETE RESTRICT` check on the foreign key. Without it Postgres scans this whole
> table on every attempt to delete an assignment — the trap 40.12 documented when company-service's
> child indexes stopped covering their foreign key.

> **`RESTRICT`, not `CASCADE`.** A progress row is the record that somebody was asked to do something;
> deleting an assignment must not erase it. Drafts have no progress rows, so deleting a draft (the only
> deletion the service permits) still works, and the constraint is a second, database-level guarantee
> behind that rule.

> **`failed_threshold` is why this status vocabulary is not a copy of `UserLessonProgressRecords`'s.**
> A lesson is finished or not; an assignment is finished only when a quality threshold is met, so
> "started, tried four times, still under the bar" has to be a state the РОП can see rather than an
> invisible retry loop. The roadmap calls it the most valuable row on the screen, and `AttemptCount`
> carries its whole weight: without it, "did not finish" and "tried four times and did not reach the
> bar" are the same status and call for opposite reactions.

**Frozen after issue, in the database.** `Assignments_reject_frozen_change` (`BEFORE UPDATE`) refuses
any change to `SourceType`, `SourceRef`, `Content`, `CompletionRule`, `OrganizationId`, `ActivatedAt`
or — since 40.24 — `RepeatOfAssignmentId` and `RepeatWaveIndex`, once the row has left `draft`; those
are what every recorded score was measured against and read through. It also refuses `active → draft`,
and freezes a `closed` row whole. `Title`, `Goal`, `Audience`, `OpensAt`, `Deadline` and
`RepeatSchedule` stay writable on purpose: adding three people to a running assignment and extending a
deadline are ordinary acts of running a team, and a trigger that forbade them would be one 40.23 and
40.24 have to break — 40.24 in particular needs `RepeatSchedule` writable, because editing it on an
active assignment is the only way to cancel waves that have not gone out yet.

**No backfill, and no maintenance window.** Both tables are created empty by the migration, nothing
else filters on them, and no existing row anywhere gains a meaning it did not have. The same holds for
40.23's single added column and 40.24's two: `Assignments` is empty in every deployed database, so
they are added over zero rows — which is also why 40.24's unique index needs no
`CREATE INDEX CONCURRENTLY` script.

**Who writes `AssignmentProgressRecords`, as of 40.24 — still two writers, disjoint by column.**

- **40.23 creates rows and never updates one.** Issuing an assignment (and re-saving an active one's
  audience) resolves the audience rule into people by asking identity-service who currently works
  here, then inserts a `not_started` row per recipient and stages one `assignment.issued` outbox event
  per recipient in the same transaction. It only ever *adds*: somebody removed from the audience keeps
  their row, because the row is the record that they were asked and deleting it would rewrite what
  happened — the same argument that made the foreign key `RESTRICT`. **40.24's repeat sweep is not a
  third writer**: it creates a new `Assignments` row and then runs the very same fan-out
  (`AssignmentFanOut`, extracted for exactly this reason), so the rows it writes are that new
  assignment's, one per recipient, and the idempotency story is unchanged.
- **40.22 updates rows and never creates one.** `AssignmentThresholdEvaluator` moves `Status`,
  `BestScore`, `AttemptCount`, `FirstOpenedAt` and `CompletedAt`, recomputed from attempt rows rather
  than incremented.

That split is what makes both idempotent: a re-run of the fan-out skips whoever has a row and cannot
walk somebody halfway through back to `not_started`, and a redelivered Kafka event recomputes to the
same numbers. Nothing on the learner's read path writes at all — there is deliberately no "mark as
opened" route, which would be a third writer with a different idea of what "started" means.

Verification scripts: `docs/TENANCY/sql/40.21_assignments_verify.sql`,
`docs/TENANCY/sql/40.22_completion_threshold_verify.sql`,
`docs/TENANCY/sql/40.23_assignment_fanout_verify.sql` and
`docs/TENANCY/sql/40.24_assignment_repeats_verify.sql` (all read-only, never executed).

**Who writes the four progress columns (40.22).** `AssignmentThresholdConsumer` in learning-service,
reacting to `dialog.evaluated` and `exercise.completed`. `Status`, `BestScore`, `AttemptCount`,
`FirstOpenedAt` and `CompletedAt` are **recomputed** from the attempt rows that already exist — the
person's `UserExerciseAttempts` and their `UserDialogScores` — rather than incremented, so a
redelivered Kafka message leaves the same values behind. `BestScore` only ever rises; `completed` is
terminal. Work recorded before the assignment was issued is excluded: the window opens at the later of
`ActivatedAt` and `OpensAt`.

---

### `UserDialogScores`

Phase 40.22. One graded practice conversation, as learning-service heard about it on
`dialog.evaluated`. Strict tenant data: a conversation happens inside exactly one organization, so
`OrganizationId` is `NOT NULL` and the policy `UserDialogScores_tenant_isolation` is plain equality.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `Id` | `uuid` | NOT NULL | PK |
| `OrganizationId` | `uuid` | NOT NULL | RLS, strict equality |
| `UserId` | `uuid` | NOT NULL | |
| `SessionId` | `varchar(64)` | NOT NULL | ai-service's session id, a string because that is what the event carries. `CK_UserDialogScores_SessionId` (non-blank) |
| `DialogModeKey` | `varchar(100)` | NOT NULL | How an assignment's `dialog_scenario` item names a scenario. `CK_UserDialogScores_DialogModeKey` (non-blank) |
| `DialogModeId` | `uuid` | NOT NULL | Kept for tracing back to ai-service; never matched on |
| `Score` | `integer` | NOT NULL | The grade the learner was shown, 0–100. `CK_UserDialogScores_Score` |
| `EvaluatedAt` | `timestamptz` | NOT NULL | The envelope's `occurredAt` |

Indexes: `IX_UserDialogScores_OrganizationId_UserId_SessionId` (**UNIQUE**),
`IX_UserDialogScores_OrganizationId_UserId_DialogModeKey_Evalua~`.

> **Why a table and not two more columns on the progress row.** "3 диалога с оценкой ≥70" is a
> question about a *set* of conversations, and no counter can answer it. Keeping the set also means
> `AttemptCount` and `BestScore` are derived rather than incremented, which is what makes an
> at-least-once Kafka redelivery harmless: the unique index above turns a reprocessed event into a
> no-op. A counter would drift upward on its own once the Redis dedupe window expires, and "tried 4
> times and did not reach the bar" is the line a РОП acts on — a number that inflates while nobody
> practises is worse than no number.

> **Matched by key, not by id, and not tied to an assignment.** The row records what happened to a
> person, not what it counted towards: one conversation may satisfy two assignments referencing the
> same scenario, so a foreign key would mean duplicating it. `DialogModeKey` rather than
> `DialogModeId` because 40.18's copy-on-write override of a global dialog mode keeps its parent's key
> and gets a new id — an assignment written against the shared library keeps working after an
> organization customizes the prompt.

> **Nothing before 40.22 can be backfilled here, ever.** `dialog.evaluated` carried no grade until
> this block added one (`rawScore` is the pre-multiplier XP reward, not a score), so conversations
> graded before the deploy are invisible to every assignment. Recorded in
> [DONT_FORGET.md](DONT_FORGET.md).

---

### `DialogReviewNotes`

Phase 40.25. One annotation on one graded conversation: either the РОП coaching a manager on a
quoted fragment of it (`coaching_note`), or the manager saying the AI graded them wrongly
(`score_dispute`) — docs/TENANCY/ASSIGNMENTS.md §4.1. One table for both directions rather than two,
because they share a session, a quoted fragment, a comment, an author, a subject and a resolution;
what differs is who may close the row and with which word, which is a check constraint below rather
than a second schema. Alternatives are in [DECISIONS.md](DECISIONS.md) (2026-08-18).

**Strict tenant data, plain-equality RLS.** A conversation and everything said about it happen inside
one organization, so `OrganizationId` is `NOT NULL` and the policy is plain equality — never the
content tables' `IS NULL OR = current`. A global row here would mean one customer's manager arguing
about a grade in front of every other customer.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `Id` | `uuid` | NOT NULL | PK |
| `OrganizationId` | `uuid` | NOT NULL | RLS, plain equality |
| `Kind` | `varchar(20)` | NOT NULL | `coaching_note` \| `score_dispute`; default `coaching_note`; `CK_DialogReviewNotes_Kind`. Immutable once written |
| `SessionId` | `varchar(64)` | NOT NULL | ai-service's session id, copied from the `UserDialogScores` row for it. Same width as `UserDialogScores.SessionId`. **Not a foreign key** (see below). `CK_DialogReviewNotes_SessionId` (non-blank) |
| `DialogModeKey` | `varchar(100)` | NOT NULL | Copied from the score row, denormalized so "which prompts do managers argue with" is one query over this table rather than a join into ai-service |
| `SubjectUserId` | `uuid` | NOT NULL | Whose conversation it is — the manager. Resolved from the score row, never from the request |
| `AuthorUserId` | `uuid` | NOT NULL | Who wrote the row: the РОП for a coaching note, the manager for a dispute. `CK_DialogReviewNotes_Author`: equal to `SubjectUserId` whenever `Kind = 'score_dispute'` |
| `QuotedFromMessageIndex` | `integer` | NULL | First message of the quoted fragment; null when the note is about the conversation as a whole |
| `QuotedToMessageIndex` | `integer` | NULL | Last message of the quoted fragment, inclusive |
| `QuotedText` | `varchar(8000)` | NULL | A frozen copy of the quoted lines, kept even though the transcript is one service away — retention/latency must not turn Monday's coaching note into three empty lines |
| `Comment` | `varchar(4000)` | NOT NULL | The РОП's coaching, or the manager's reason for disputing. `CK_DialogReviewNotes_Comment` (non-blank) |
| `DisputedScore` | `integer` | NULL | The 0–100 grade being argued about, frozen at write time |
| `Status` | `varchar(20)` | NOT NULL | Default `open`; `CK_DialogReviewNotes_Status` (below) |
| `Resolution` | `varchar(4000)` | NULL | The РОП's verdict in their own words; required by the service when a dispute is rejected |
| `AdjustedScore` | `integer` | NULL | What the grade should have been, 0–100, set only when a dispute is upheld. Never written back to `UserDialogScores` |
| `ResolvedBy` | `uuid` | NULL | Who closed it: the manager for a note, the РОП for a dispute |
| `ResolvedAt` | `timestamptz` | NULL | |
| `CreatedAt` | `timestamptz` | NOT NULL | |
| `UpdatedAt` | `timestamptz` | NOT NULL | |

Indexes: `IX_DialogReviewNotes_OrganizationId_SubjectUserId_Status` (the manager's inbox),
`IX_DialogReviewNotes_OrganizationId_Kind_Status_CreatedAt` (the РОП's queue — kind before status,
because the queue is always asked for one kind at a time), `IX_DialogReviewNotes_OrganizationId_SessionId`
(everything ever said about one conversation).

> **`UX_DialogReviewNotes_OpenDisputePerSession`** — a **unique partial index** on
> `("OrganizationId", "SessionId") WHERE "Kind" = 'score_dispute' AND "Status" = 'open'`. At most one
> unreviewed dispute per conversation: a queue that fills with duplicates of one complaint is a queue
> the РОП stops opening, and the whole mechanism only works while they keep opening it. Partial, so the
> same conversation may be disputed again after a verdict — someone told "the grade stands" who then
> finds new evidence is not spamming.

Check constraints: `CK_DialogReviewNotes_Kind` (`Kind IN ('coaching_note','score_dispute')`);
`CK_DialogReviewNotes_Status` (a coaching note may only be `open`/`acknowledged`, a dispute only
`open`/`upheld`/`rejected` — a coaching note cannot be "upheld" and a dispute cannot be closed by being
read, because those two words are what separate a review from an acknowledgement);
`CK_DialogReviewNotes_Author` (`Kind <> 'score_dispute' OR AuthorUserId = SubjectUserId` — asserted in
one direction only, so a РОП who also practises may still write a note on their own conversation);
`CK_DialogReviewNotes_Comment` / `CK_DialogReviewNotes_SessionId` (non-blank, trimmed);
`CK_DialogReviewNotes_Quote` (both indexes non-negative, `to >= from` when both are present);
`CK_DialogReviewNotes_Scores` (`DisputedScore`/`AdjustedScore` each 0–100, and `AdjustedScore` only
when `Status = 'upheld'`); `CK_DialogReviewNotes_CoachingNoteQuote` (a coaching note requires a
non-blank `QuotedText` — its entire product value is the three lines the РОП is taking to Monday's
meeting).

> **`SessionId` is not a foreign key, and never can be: the conversation is a Mongo document in
> ai-service.** What makes the value trustworthy is that nothing writes it from a request — every
> insert copies it from the `UserDialogScores` row for that session, which is itself under row-level
> security, so a session belonging to another organization does not exist to the code that would write
> it here.

**No backfill, no maintenance window, no concurrent-index script** — the table is created empty by
this migration, so all three ordinary indexes and the partial unique index are built over zero rows
and the `ACCESS EXCLUSIVE` lock costs nothing. Nothing could be backfilled either: no coaching note or
dispute has ever existed anywhere to copy from.

Verification script: [docs/TENANCY/sql/40.25_dialog_reviews_verify.sql](TENANCY/sql/40.25_dialog_reviews_verify.sql)
(read-only, never executed).

---

### `Exercises`

| Column              | Type                       | Nullable | Notes                                                                         |
|---------------------|----------------------------|----------|-------------------------------------------------------------------------------|
| `Id`                | `uuid`                     | NOT NULL | PK                                                                            |
| `OrganizationId` | `uuid` | NULL | Phase 40.10 — `NULL` = global library shared by every organization; non-null = one organization's own copy (40.18). RLS policy `Exercises_tenant_isolation` (content variant: `IS NULL OR = current`). |
| `LessonId`          | `uuid`                     | NOT NULL | FK → `Lessons.Id`                                                             |
| `Type`              | `text`                     | NOT NULL | `choose_option`, `fill_blank`, `free_text`, `reorder`, `match_pairs`, `categorize`, `spot_mistake`, `rewrite` |
| `OrderInLesson`     | `integer`                  | NOT NULL |                                                                               |
| `SerializedContent` | `jsonb`                    | NOT NULL | Schema varies by type                                                         |
| `CustomAiPrompt`    | `text`                     | NULL     | Per-exercise AI evaluation criteria                                           |
| `CreatedAt`         | `timestamp with time zone` | NOT NULL |                                                                               |
| `UpdatedAt`         | `timestamp with time zone` | NOT NULL |                                                                               |

Indexes: `IX_Exercises_LessonId_OrderInLesson`

---

### `ExerciseTypePrompts`

| Column        | Type                       | Nullable | Notes                                          |
|---------------|----------------------------|----------|------------------------------------------------|
| `Id`          | `uuid`                     | NOT NULL | PK                                             |
| `ExerciseType`| `text`                     | NOT NULL | UNIQUE — type key                              |
| `SystemPrompt`| `text`                     | NOT NULL | Global system prompt for all exercises of type |
| `UpdatedAt`   | `timestamp with time zone` | NOT NULL |                                                |

**AI Evaluation Logic:** Final prompt = `exercise_type_prompts.system_prompt` + (if exercise.custom_ai_prompt) + exercise content + user answer.

---

### `Friendships`

> **Microservices (Phase 5):** owned by the **social-service** Postgres database
> (`social`), along with all `Discuss*` tables and the `chat_conversations` Mongo
> collection. The `social` database also holds a `UserReplicas` read-model table
> (`UserId`, `Email`, `DisplayName`, `AvatarKey`) fed by `user.*` Kafka events.
> `RequesterId`/`AddresseeId` (and Discuss `AuthorId`/`UserId`) are loose `Guid`s in the
> social database — no cross-DB FK to `Users`. See [SOCIAL_SERVICE.md](SOCIAL_SERVICE.md).

| Column        | Type                       | Nullable | Notes                              |
|---------------|----------------------------|----------|------------------------------------|
| `Id`          | `uuid`                     | NOT NULL | PK                                 |
| `OrganizationId` | `uuid`                  | NOT NULL | Phase 40.13 — owning tenant. `ITenantScoped`, strict RLS (plain equality — there is no global content flavour in social-db). A friendship cannot cross the organization boundary. |
| `RequesterId` | `uuid`                     | NOT NULL | FK → `Users.Id` — who sent (loose `Guid` in `social`) |
| `AddresseeId` | `uuid`                     | NOT NULL | FK → `Users.Id` — who received (loose `Guid` in `social`) |
| `Status`      | `integer`                  | NOT NULL | 0=Pending, 1=Accepted, 2=Declined |
| `CreatedAt`   | `timestamp with time zone` | NOT NULL |                                    |
| `AcceptedAt`  | `timestamp with time zone` | NULL     |                                    |

**Indexes:**
- UNIQUE `(OrganizationId, RequesterId, AddresseeId)` — no duplicate requests within one
  organization. Was plain `(RequesterId, AddresseeId)`; Phase 40.13 put the organization first
  because memberships (40.6) let one person belong to two customers, and the old platform-wide pair
  rejected the second organization's friendship between the same two people as a duplicate. Swapped
  **inside** the `AddOrganizationId` migration, not the concurrent-rebuild script.
- UNIQUE `(OrganizationId, CanonicalLowId, CanonicalHighId)` (`IX_Friendships_CanonicalPair`) —
  canonical-pair guard against concurrent inserts, same reasoning.
- Individual on `(OrganizationId, RequesterId)` and `(OrganizationId, AddresseeId)`

**Constraints:**
- CHECK `RequesterId != AddresseeId` — cannot friend yourself

> **Phase 40.13** also added `OrganizationId` (`NOT NULL`, strict RLS) to `DiscussThreads`,
> `DiscussReplies`, `DiscussVotes`, `DiscussThreadTags` and `DiscussPhotos` (below), and a
> **nullable** `OrganizationId` to `DiscussTags` — `NULL` is the curated vocabulary every
> organization shares, non-null is one customer's own tag, `UNIQUE(OrganizationId, Slug)` plus a
> partial unique index over the global rows (Postgres treats `NULL`s in a composite unique index as
> distinct, so the composite alone would allow a duplicate curated tag). These tables are not yet
> individually documented in this file; see
> [SOCIAL_SERVICE.md](SOCIAL_SERVICE.md#multi-tenancy-phase-4013) for the full column/RLS/index
> breakdown and [DECISIONS.md](DECISIONS.md) for the `DiscussTags` content-flavour call.

---

### Notifications — **there is no such table**

This document used to carry a full `Notifications` Postgres table here, with two indexes and a
Hangfire recurring job `notification-cleanup`. None of it exists, and none of it survived the
monolith split: **notification-service has no database.** No `DbContext`, no `Migrations/` folder,
no Npgsql package reference. Hangfire exists in exactly one service, gamification, and its only two
recurring jobs are `WeeklyLeagueClosure` and `DailyStreakReset` — there has never been a
`notification-cleanup`.

Notifications live in Redis, and the key is the tenant boundary rather than an `OrganizationId`
column:

| Key | Type | TTL | Holds |
|---|---|---|---|
| `org:{orgId}:notifications:inbox:{userId}` | List | `NotificationStorage:RetentionDays` (default 30) | the serialized notifications themselves, newest first, capped at `NotificationStorage:InboxCapacity` (default 100) |
| `org:{orgId}:notifications:unread:{userId}` | String | same | the unread counter |

`RedisKeys.OrganizationPrefix` throws `InvalidOperationException("Organization context is not
set.")` on an empty organization id rather than falling back to a zero guid — without that, an
unset tenant would silently build one shared `org:00000000-…:` bucket collecting every caller whose
context was missing, which is worse than the un-prefixed key 40.13 replaced. Expiry is the TTL; no
job sweeps anything.

The Redis section below is the authoritative description of these keys, together with the
chat-email watermark and the deliberately un-prefixed delayed-email queue.

---

### `ReferenceMaterials`

Legacy markdown glossary, kept to serve old skill-detail pages. Superseded for the "Коллекция" redesign by the `Techniques` cluster below — see [HANDBOOK_REDESIGN.md](HANDBOOK_REDESIGN.md).

| Column            | Type      | Nullable | Notes              |
|-------------------|-----------|----------|--------------------|
| `Id`              | `uuid`    | NOT NULL | PK                 |
| `OrganizationId` | `uuid` | NULL | Phase 40.10 — `NULL` = global library shared by every organization; non-null = one organization's own copy (40.18). RLS policy `ReferenceMaterials_tenant_isolation` (content variant: `IS NULL OR = current`). |
| `ParentMaterialId` | `uuid` | NULL | Phase 40.18 — set when this row is one organization's copy-on-write override of a global material; FK → `ReferenceMaterials.Id` `ON DELETE RESTRICT`. `CK_ReferenceMaterials_OverrideHasOwner`: a row with a parent always has an owner. |
| `BaseContentHash` | `varchar(64)` | NULL | Phase 40.18 — lowercase hex SHA-256 of the parent's canonical content at fork time (or at the last "keep override" review). The base has moved, and this override is stale, when the parent's current hash differs. A fingerprint rather than a frozen-version id because this family has no version table — see `docs/DECISIONS.md` (2026-08-18). |
| `IsArchived`      | `boolean` | NOT NULL | Phase 40.18, default `false`. The review action "take the new base" archives the override instead of deleting it: nothing has a foreign key to this row, but progress and history reference it, and resolution ignores archived overrides so the global material becomes visible again. |
| `SkillId`         | `uuid`    | NOT NULL | FK → `Skills.Id`   |
| `Title`           | `text`    | NOT NULL |                    |
| `MarkdownContent` | `text`    | NOT NULL |                    |
| `SortOrder`       | `integer` | NOT NULL |                    |
| `Category`        | `text`    | NULL     |                    |
| `Tags`            | `text`    | NULL     | Comma-separated    |

Indexes: `IX_ReferenceMaterials_OrganizationId_SkillId_SortOrder`, `IX_ReferenceMaterials_ParentMaterialId` (40.18 — read resolution is a `NOT EXISTS` on exactly this column).

---

### `Techniques`

Techniques replace `ReferenceMaterials` as the handbook's primary entity. Dialog samples and case studies now live in single `jsonb` columns on this table (not separate sub-tables) — the admin writes the JSON directly.

| Column           | Type                       | Nullable | Notes                                                                            |
|------------------|----------------------------|----------|----------------------------------------------------------------------------------|
| `Id`             | `uuid`                     | NOT NULL | PK                                                                               |
| `OrganizationId` | `uuid` | NULL | Phase 40.10 — `NULL` = global library shared by every organization; non-null = one organization's own copy (40.18). RLS policy `Techniques_tenant_isolation` (content variant: `IS NULL OR = current`). |
| `ParentTechniqueId` | `uuid` | NULL | Phase 40.18 — copy-on-write override pointer; FK → `Techniques.Id` `ON DELETE RESTRICT`. `CK_Techniques_OverrideHasOwner`: a row with a parent always has an owner. |
| `BaseContentHash` | `varchar(64)` | NULL | Phase 40.18 — SHA-256 of the parent's canonical content (technique row + coach + additional-skill links) at fork time or at the last "keep override" review. Excludes `Id`, `OrganizationId` and `UpdatedAt`: a base re-saved unchanged has not changed, and a queue that cries wolf teaches its reader to click through. |
| `IsArchived`     | `boolean` | NOT NULL | Phase 40.18, default `false`. "Take the new base" archives the override — `UserTechniqueProgress` points at this row without a foreign key, so deleting it to tidy a queue would orphan history. |
| `Slug`           | `varchar(120)`             | NOT NULL | Unique **per organization**, not per installation. An override deliberately carries its base's slug, which is what keeps the handbook URL stable across a customization; read resolution is what makes the lookup by slug return exactly one row. |
| `Name`           | `varchar(200)`             | NOT NULL |                                                                                  |
| `Summary`        | `text`                     | NOT NULL | Short excerpt shown on card                                                      |
| `Body`           | `text`                     | NOT NULL | Markdown body for expanded view                                                  |
| `Tags`           | `text[]`                   | NOT NULL | Free tags for search/filter                                                      |
| `PrimarySkillId` | `uuid`                     | NULL     | FK → `Skills.Id` ON DELETE SET NULL; drives the skill filter pill                |
| `Difficulty`     | `integer`                  | NOT NULL | 1=Novice, 2=Practitioner, 3=Expert, 4=Master (`TechniqueLevels`)                 |
| `DialogJson`     | `jsonb`                    | NULL     | Ordered array of `{ orderIndex, side, text, annotations }` — null if no sample   |
| `CaseJson`       | `jsonb`                    | NULL     | Single case object `{ title, body, metrics? }` — null if no case                 |
| `SortOrder`      | `integer`                  | NOT NULL |                                                                                  |
| `CreatedAt`      | `timestamp with time zone` | NOT NULL |                                                                                  |
| `UpdatedAt`      | `timestamp with time zone` | NOT NULL |                                                                                  |

Indexes: `IX_Techniques_OrganizationId_Slug` (unique), `IX_Techniques_Slug_Global` (unique, `WHERE "OrganizationId" IS NULL`), `IX_Techniques_OrganizationId_PrimarySkillId`, `IX_Techniques_OrganizationId_SortOrder`, `IX_Techniques_ParentTechniqueId` (40.18 — read resolution is a `NOT EXISTS` on exactly this column).

---

### `TechniqueSkills`

M:N link table — a technique can additionally span multiple skills (the primary skill lives on `Techniques.PrimarySkillId`).

| Column        | Type   | Nullable | Notes                                   |
|---------------|--------|----------|-----------------------------------------|
| `TechniqueId` | `uuid` | NOT NULL | FK → `Techniques.Id` ON DELETE CASCADE  |
| `SkillId`     | `uuid` | NOT NULL | FK → `Skills.Id` ON DELETE CASCADE      |

Composite PK: (`TechniqueId`, `SkillId`).

---

### `TechniqueCoaches`

Optional NPC-coach sidecar (quote + practice challenges). At most one per technique.

| Column            | Type    | Nullable | Notes                                     |
|-------------------|---------|----------|-------------------------------------------|
| `Id`              | `uuid`  | NOT NULL | PK                                        |
| `TechniqueId`     | `uuid`  | NOT NULL | FK → `Techniques.Id` ON DELETE CASCADE, UNIQUE |
| `AvatarSeed`      | `text`  | NOT NULL | Seed for `GeoAvatar` procedural portrait  |
| `Name`            | `text`  | NOT NULL |                                           |
| `Role`            | `text`  | NOT NULL |                                           |
| `Quote`           | `text`  | NOT NULL |                                           |
| `ChallengesJson`  | `jsonb` | NULL     | `[{ label, kind, targetSlug }]`           |

---

### `UserTechniqueProgressRecords`

Per-user mastery tracking for techniques (drives the `MasteryRing` + `isNew` chip).

| Column           | Type                       | Nullable | Notes                                              |
|------------------|----------------------------|----------|----------------------------------------------------|
| `Id`             | `uuid`                     | NOT NULL | PK                                                 |
| `OrganizationId` | `uuid` | NOT NULL | Phase 40.10 — owning tenant. RLS policy `UserTechniqueProgressRecords_tenant_isolation`. |
| `UserId`         | `uuid`                     | NOT NULL | Owning user. **Not a foreign key** — `Users` lives in identity-db; learning-db keeps only a `UserReplicas` read model. There is no cascade either: nothing in this database deletes when a user does. |
| `TechniqueId`    | `uuid`                     | NOT NULL | FK → `Techniques.Id` ON DELETE CASCADE             |
| `Level`          | `integer`                  | NOT NULL | 0=Unseen, 1=Novice, 2=Practitioner, 3=Expert, 4=Master |
| `MasteryPercent` | `integer`                  | NOT NULL | 0–100                                              |
| `FirstSeenAt`    | `timestamp with time zone` | NULL     | Set by POST `/techniques/{slug}/seen`              |
| `UpdatedAt`      | `timestamp with time zone` | NOT NULL |                                                    |

Indexes: `IX_UserTechniqueProgress_User_Technique` (unique on `UserId`,`TechniqueId`).

---

### `UserSkillProgressRecords`

| Column                | Type      | Nullable | Notes                                               |
|-----------------------|-----------|----------|-----------------------------------------------------|
| `Id`                  | `uuid`    | NOT NULL | PK                                                  |
| `OrganizationId` | `uuid` | NOT NULL | Phase 40.10 — owning tenant. RLS policy `UserSkillProgressRecords_tenant_isolation`. |
| `UserId`              | `uuid`    | NOT NULL | Owning user. Not a foreign key — `Users` lives in identity-db; learning-db keeps only a `UserReplicas` read model. |
| `SkillId`             | `uuid`    | NOT NULL | FK → `Skills.Id`                                    |
| `Status`              | `text`    | NOT NULL | `locked` / `available` / `in_progress` / `completed`|
| `CompletedLessonCount`| `integer` | NOT NULL |                                                     |
| `TotalLessonCount`    | `integer` | NOT NULL |                                                     |

---

### `UserLessonProgressRecords`

| Column        | Type                       | Nullable | Notes                                      |
|---------------|----------------------------|----------|--------------------------------------------|
| `Id`          | `uuid`                     | NOT NULL | PK                                         |
| `OrganizationId` | `uuid` | NOT NULL | Phase 40.10 — owning tenant. RLS policy `UserLessonProgressRecords_tenant_isolation`. |
| `UserId`      | `uuid`                     | NOT NULL | Owning user. Not a foreign key — `Users` lives in identity-db; learning-db keeps only a `UserReplicas` read model. |
| `LessonId`    | `uuid`                     | NOT NULL | FK → `Lessons.Id`                          |
| `LessonVersionId` | `uuid`                 | NULL     | Phase 40.16 — the `LessonVersions` row this progress's `BestScore` and `CompletedAt` were achieved against. Refreshed only when the row actually advances (new best score, or the transition to completed), so "completed version 1" does not silently become "completed version 3". No FK — see `UserExerciseAttempts` below. |
| `Status`      | `text`                     | NOT NULL | `not_started` / `in_progress` / `completed`|
| `BestScore`   | `integer`                  | NOT NULL |                                            |
| `CompletedAt` | `timestamp with time zone` | NULL     |                                            |

Indexes: `IX_UserLessonProgressRecords_OrganizationId_UserId_LessonId`,
`IX_UserLessonProgressRecords_OrganizationId_LessonVersionId` (40.16).
Neither is created by a migration — both are built by hand
(`docs/TENANCY/sql/40.10_learning_organization_indexes_concurrently.sql`,
`40.16_progress_version_indexes_concurrently.sql`), because this table grows with usage and the
migrations run from `Database.Migrate()` at startup.

---

### `UserExerciseAttempts`

| Column                | Type                       | Nullable | Notes                              |
|-----------------------|----------------------------|----------|------------------------------------|
| `Id`                  | `uuid`                     | NOT NULL | PK                                 |
| `OrganizationId` | `uuid` | NOT NULL | Phase 40.10 — owning tenant. RLS policy `UserExerciseAttempts_tenant_isolation`. |
| `UserId`              | `uuid`                     | NOT NULL | Owning user. Not a foreign key — `Users` lives in identity-db; learning-db keeps only a `UserReplicas` read model. |
| `LessonVersionId`     | `uuid`                     | NULL     | Phase 40.16 — the immutable `LessonVersions` snapshot this answer was scored against. |
| `ExerciseId`          | `uuid`                     | NOT NULL | Since 40.16 read as the exercise's identity **inside** `LessonVersionId`'s snapshot (the `exerciseId` key in its `Content`), not as a pointer into the mutable `Exercises` table. Same value, different meaning. |
| `SerializedAnswer`    | `jsonb`                    | NOT NULL | User's answer payload              |
| `IsCorrect`           | `boolean`                  | NOT NULL |                                    |
| `Score`               | `integer`                  | NOT NULL |                                    |
| `SerializedAiFeedback`| `jsonb`                    | NULL     | Present for AI-evaluated types     |
| `AttemptedAt`         | `timestamp with time zone` | NOT NULL |                                    |

Indexes: `IX_UserExerciseAttempts_OrganizationId_UserId_ExerciseId`,
`IX_UserExerciseAttempts_OrganizationId_LessonVersionId_Exercis~` (40.16 — the `~` is EF's
truncation at Postgres's 63-byte identifier limit, and the name has to stay exactly that or the next
`dotnet ef migrations add` will emit a table-locking `CreateIndex`). Both built by hand, as above.

> **Why the version reference exists (Phase 40.16, CONTENT_MODEL.md §2.3).** Before it, an attempt
> pointed only at `ExerciseId` — a row an administrator can edit. Fixing a wrong correct-answer
> therefore re-interpreted every historical attempt, and accuracy-per-skill (the number sold to the
> РОП as a measure of team readiness) moved retroactively. Bound to a frozen snapshot, the edit
> produces a new version and the old series keeps pointing at the old content.

> **Nullable, and no foreign key — both deliberate.** Nullable, because attempts recorded before
> 40.16 have nothing to point at until `docs/TENANCY/sql/40.16_progress_version_backfill.sql` runs;
> `NULL` reads as "unversioned", which `GET /admin/lessons/{id}/accuracy` reports as its own bucket
> rather than folding into version 1 and quietly claiming to know what those answers were scored
> against. No foreign key, because `LessonVersions` is a content table under an `IS NULL OR = current`
> RLS policy while this is strict tenant data: a foreign key is validated with the referencing
> statement's privileges, so under a `NOBYPASSRLS` role it would reject rows that exist. `ExerciseId`
> has never carried one either.

---

### `UserStreaks`

> **Microservices (Phase 7):** owned by the **gamification-service** Postgres database
> (`gamification`). See [GAMIFICATION_SERVICE.md](GAMIFICATION_SERVICE.md).

| Column                  | Type      | Nullable | Notes                  |
|-------------------------|-----------|----------|------------------------|
| `Id`                    | `uuid`    | NOT NULL | PK                     |
| `OrganizationId`        | `uuid`    | NOT NULL | Phase 40.13 — owning tenant. `ITenantScoped`, strict RLS. |
| `UserId`                | `uuid`    | NOT NULL | Owning user. Not a foreign key — `Users` lives in identity-db; gamification-db keeps only a `UserReplicas` read model. |
| `CurrentStreakDayCount` | `integer` | NOT NULL |                        |
| `LongestStreakDayCount` | `integer` | NOT NULL |                        |
| `LastActivityDate`      | `date`    | NULL     |                        |

**Indexes:** UNIQUE `(OrganizationId, UserId)` — was plain `UNIQUE(UserId)`; Phase 40.13 added the
organization because memberships (40.6) let one person belong to two customers, and the old
constraint would have refused a second streak row. Swapped **inside** the `AddOrganizationId`
migration (at most one row per user, so a short lock), not the concurrent-rebuild script.

---

### `UserXpRecords`

| Column     | Type                       | Nullable | Notes                                            |
|------------|----------------------------|----------|--------------------------------------------------|
| `Id`       | `uuid`                     | NOT NULL | PK                                               |
| `OrganizationId` | `uuid`                | NOT NULL | Phase 40.13 — owning tenant. `ITenantScoped`, strict RLS. |
| `UserId`   | `uuid`                     | NOT NULL | Owning user. Not a foreign key — `Users` lives in identity-db; gamification-db keeps only a `UserReplicas` read model. |
| `Amount`   | `integer`                  | NOT NULL |                                                  |
| `Source`   | `text`                     | NOT NULL | `exercise` / `streak_bonus` / `league_bonus` / `admin_correction` |
| `EarnedAt` | `timestamp with time zone` | NOT NULL |                                                  |

**Indexes:** `(OrganizationId, UserId)`, rebuilt with `CREATE INDEX CONCURRENTLY` by
`docs/TENANCY/sql/40.13_gamification_organization_indexes_concurrently.sql` — this table grows
without bound, unlike the other six 40.13 tables, so its index work stayed out of the EF migration.
UNIQUE `(SourceEventId)` stays **global** on purpose: it is a statement about the Kafka event
stream, and scoping it per organization would let one event grant XP once per tenant.

---

### `Leagues`

| Column          | Type      | Nullable | Notes                                           |
|-----------------|-----------|----------|-------------------------------------------------|
| `Id`            | `uuid`    | NOT NULL | PK                                              |
| `OrganizationId` | `uuid`   | NOT NULL | Phase 40.13 — owning tenant. `ITenantScoped`, strict RLS. |
| `Tier`          | `text`    | NOT NULL | tier key → `LeagueTiers.Key` (e.g. `bronze`)    |
| `WeekStartDate` | `date`    | NOT NULL | start of the period (named "week" for history)  |
| `WeekEndDate`   | `date`    | NOT NULL | end of the period (period length is configurable) |

**Indexes:** UNIQUE `(OrganizationId, WeekStartDate, Tier)` — was `UNIQUE(WeekStartDate, Tier)`,
i.e. "one bronze league per week for the whole platform"; the second organization to roll over
would have hit a unique violation and gotten no league at all. Swapped **inside** the
`AddOrganizationId` migration (a handful of rows per week, so a short lock), not the
concurrent-rebuild script.

---

### `LeagueTiers`

The configurable tier ladder (replaces the previously hardcoded list). Seeded by migration `20260616120000_AddLeagueTiersAndSchedule` with `bronze/silver/gold/diamond`. Managed via `/admin/leagues/tiers`. `LeagueService` reads the ladder ordered by `Order`; `Leagues.Tier` references `Key`.

| Column   | Type                    | Nullable | Notes                                         |
|----------|-------------------------|----------|-----------------------------------------------|
| `Id`     | `uuid`                  | NOT NULL | PK                                            |
| `Key`    | `varchar(40)`           | NOT NULL | unique slug, immutable (stored on `Leagues.Tier`) |
| `Name`   | `varchar(60)`           | NOT NULL | display label                                 |
| `Color`  | `varchar(20)`           | NOT NULL | hex color for badges                          |
| `Order`  | `integer`               | NOT NULL | promotion ladder, ascending (lowest = entry tier) |

---

### `LeagueMemberships`

| Column            | Type      | Nullable | Notes                                        |
|-------------------|-----------|----------|----------------------------------------------|
| `Id`              | `uuid`    | NOT NULL | PK                                           |
| `OrganizationId`  | `uuid`    | NOT NULL | Phase 40.13 — owning tenant. `ITenantScoped`, strict RLS. |
| `UserId`          | `uuid`    | NOT NULL | Owning user. Not a foreign key — `Users` lives in identity-db; gamification-db keeps only a `UserReplicas` read model. |
| `LeagueId`        | `uuid`    | NOT NULL | FK → `Leagues.Id`                            |
| `WeeklyXpAmount`  | `integer` | NOT NULL |                                              |
| `Rank`            | `integer` | NOT NULL |                                              |
| `PromotionOutcome`| `text`    | NULL     | `promoted` / `demoted` / `stayed` / NULL (active) |

**Indexes:** UNIQUE `(OrganizationId, UserId, LeagueId)` — was `UNIQUE(UserId, LeagueId)`; scoped
per organization for the same memberships-can-belong-to-two-customers reason as `Leagues`/
`UserStreaks`. Plus a plain `(OrganizationId, LeagueId)` index. Both are rebuilt with `CREATE INDEX
CONCURRENTLY` by `docs/TENANCY/sql/40.13_gamification_organization_indexes_concurrently.sql` — this
table grows without bound, so (unlike `Leagues`/`UserStreaks`/`UserAchievements`) its index work
stayed out of the EF migration. A plain `(LeagueId)` index is created **before** the old one is
dropped, so the FK to `Leagues` is never left unindexed mid-rollout.

---

### `LeagueSettings`

Was a single-row table (same pattern as `OpenQuestionGlobalContexts`) through 40.12. **Phase 40.13
made it per-organization**: `CurrentPeriodStartDate`/`CurrentPeriodEndsAt` are the state of a
running competition, not configuration — shared, the first organization to roll over advanced the
period for everybody. Seeded by migration `20260607000000_AddLeagueSettings`; period columns added
by `20260616120000_AddLeagueTiersAndSchedule`. Read by `LeagueService` at runtime; edited via
`/admin/leagues/settings`. The period columns are initialized on first access (to the current
Monday-based week) if null. Since 40.13 the startup seeder no longer creates this row at all — an
organization gets one lazily, on its admin's first settings save.

| Column                          | Type                       | Nullable | Notes                                            |
|---------------------------------|----------------------------|----------|--------------------------------------------------|
| `Id`                            | `uuid`                     | NOT NULL | PK                                               |
| `OrganizationId`                | `uuid`                     | NOT NULL | Phase 40.13 — owning tenant. `ITenantScoped`, strict RLS. UNIQUE — one settings row per organization, created lazily. |
| `MaximumLeagueParticipantCount` | `integer`                  | NOT NULL | default 30                                       |
| `PromotionZoneSize`             | `integer`                  | NOT NULL | default 10                                       |
| `DemotionZoneSize`              | `integer`                  | NOT NULL | default 5                                        |
| `CurrentPeriodStartDate`        | `date`                     | NULL     | start of the running period                      |
| `CurrentPeriodEndsAt`           | `timestamptz`              | NULL     | exact close moment; drives countdown + rollover  |
| `PeriodLengthDays`              | `integer`                  | NOT NULL | default 7; applied to each new period on rollover |

---

### `GamificationSettings`

Single-row table holding the admin-editable progress-points economy (daily/weekly goals + dialog scoring) that was previously hardcoded. Created and seeded by migration `20260616130000_AddGamificationSettings`. Loaded-or-created on first access by `GamificationService`; edited via `/admin/gamification/settings`. Consumed by `SkillTreeService` (goals) and `DialogService`/`OpenAiChatService` (dialog scoring).

| Column                   | Type               | Nullable | Notes                                                        |
|--------------------------|--------------------|----------|--------------------------------------------------------------|
| `Id`                     | `uuid`             | NOT NULL | PK                                                           |
| `DailyXpGoal`            | `integer`          | NOT NULL | default 100                                                  |
| `WeeklyXpGoal`           | `integer`          | NOT NULL | default 500                                                  |
| `DialogXpMultiplier`     | `double precision` | NOT NULL | default 1.0; earned XP = `round(rawScore × multiplier)`      |
| `DialogWeightConfidence` | `integer`          | NOT NULL | default 25; max points for tone/confidence criterion         |
| `DialogWeightStructure`  | `integer`          | NOT NULL | default 25; max points for argument structure criterion      |
| `DialogWeightObjection`  | `integer`          | NOT NULL | default 25; max points for objection-handling criterion      |
| `DialogWeightGoal`       | `integer`          | NOT NULL | default 25; max points for call-goal criterion               |

### `ExerciseTypeRewards`

Per-exercise-type base progress points, replacing the hardcoded flat 10. Seeded with all 10 exercise types → 10 by `20260616130000_AddGamificationSettings`. Read by `GamificationService.GetExerciseBaseXpAsync` (falls back to 10 for unknown types); edited via `/admin/gamification/exercise-rewards/:exerciseType` (upsert).

| Column         | Type                     | Nullable | Notes                                  |
|----------------|--------------------------|----------|----------------------------------------|
| `Id`           | `uuid`                   | NOT NULL | PK                                     |
| `ExerciseType` | `character varying(40)`  | NOT NULL | UNIQUE — see `ExerciseTypes` constants |
| `BaseXpReward` | `integer`                | NOT NULL | XP on correct/passed answer            |

### `StreakMilestones`

Admin-editable activity-consistency bonus ladder, replacing the hardcoded `7→50, 30→200` switch. Seeded with those two rows. Read by `GamificationService.GetStreakBonusXpAsync` — authoritative when non-empty, otherwise the historic ladder is used. Managed via `/admin/gamification/streak-milestones` (CRUD).

| Column     | Type      | Nullable | Notes                                     |
|------------|-----------|----------|-------------------------------------------|
| `Id`       | `uuid`    | NOT NULL | PK                                        |
| `DayCount` | `integer` | NOT NULL | UNIQUE — streak length that triggers bonus |
| `XpReward` | `integer` | NOT NULL | one-off bonus XP                          |

---

### `DailyQuotes`

Quote of the day shown in the stats widget ("Совет дня"). One quote per calendar date; managed from the admin calendar at `/admin/quotes`. Created by migration `20260607120000_AddDailyQuotes`, which also seeds today's row with the previously hardcoded widget tip. The public `GET /daily-quote` endpoint falls back to the most recent quote at or before the requested date.

| Column      | Type                       | Nullable | Notes                          |
|-------------|----------------------------|----------|--------------------------------|
| `Id`        | `uuid`                     | NOT NULL | PK                             |
| `Date`      | `date`                     | NOT NULL | UNIQUE — one quote per day     |
| `Text`      | `text`                     | NOT NULL | quote body                     |
| `Author`    | `character varying(120)`   | NOT NULL | may be empty string            |
| `CreatedAt` | `timestamp with time zone` | NOT NULL |                                |
| `UpdatedAt` | `timestamp with time zone` | NOT NULL |                                |

---

### `Achievements`

| Column               | Type      | Nullable | Notes                                                       |
|----------------------|-----------|----------|-------------------------------------------------------------|
| `Id`                 | `uuid`    | NOT NULL | PK                                                          |
| `Key`                | `text`    | NOT NULL | Unique machine key, e.g. `first_lesson`, `streak_7`        |
| `Title`              | `text`    | NOT NULL |                                                             |
| `Description`        | `text`    | NOT NULL |                                                             |
| `IconEmoji`          | `text`    | NOT NULL | Emoji shown in the milestone UI                             |
| `ConditionType`      | `text`    | NOT NULL | `first_lesson` / `lesson_count` / `xp_total` / `streak_days` / `skill_completed` |
| `ConditionThreshold` | `integer` | NOT NULL | Numeric threshold; 0 for event-based conditions             |
| `SortOrder`          | `integer` | NOT NULL |                                                             |

---

### `UserAchievements`

| Column          | Type                       | Nullable | Notes                     |
|-----------------|----------------------------|----------|---------------------------|
| `Id`            | `uuid`                     | NOT NULL | PK                        |
| `OrganizationId`| `uuid`                     | NOT NULL | Phase 40.13 — owning tenant. `ITenantScoped`, strict RLS. |
| `UserId`        | `uuid`                     | NOT NULL | Owning user. **Not a foreign key and no cascade** — `Users` lives in identity-db; gamification-db keeps only a `UserReplicas` read model. |
| `AchievementId` | `uuid`                     | NOT NULL | FK → `Achievements.Id` CASCADE |
| `UnlockedAt`    | `timestamp with time zone` | NOT NULL |                           |

**Indexes:** UNIQUE `(OrganizationId, UserId, AchievementId)` — was `UNIQUE(UserId, AchievementId)`;
scoped per organization for the same memberships-can-belong-to-two-customers reason as `Leagues`/
`UserStreaks`. Swapped **inside** the `AddOrganizationId` migration, not the concurrent-rebuild
script.

> **Phase 40.13** also added `OrganizationId` (`NOT NULL`, `ITenantScoped`, strict RLS) to
> `UserLearningProgress` (not otherwise documented in this file — the local completed-lesson-count
> / has-completed-any-skill projection fed by `lesson.completed`/`skill.completed`), whose primary
> key moved from `UserId` alone to `(OrganizationId, UserId)` for the same reason. See
> [GAMIFICATION_SERVICE.md](GAMIFICATION_SERVICE.md#multi-tenancy-phase-4013) for the full
> seven-table breakdown, including the `LeagueSettings` per-organization change below.

---

### `DiscussPhotos`

Photo attachments for Discuss threads and replies. Polymorphic owner (no FK), mirroring `DiscussVotes`. See [DISCUSS.md](DISCUSS.md#photos).

| Column        | Type            | Nullable | Notes                                                  |
|---------------|-----------------|----------|--------------------------------------------------------|
| `Id`          | `uuid`          | NOT NULL | PK                                                     |
| `OrganizationId` | `uuid`       | NOT NULL | Phase 40.13 — owning tenant. `ITenantScoped`, strict RLS. |
| `OwnerType`   | `integer`       | NOT NULL | 0=Thread, 1=Reply                                      |
| `OwnerId`     | `uuid`          | NOT NULL | thread id or reply id (polymorphic, no FK — mirrors `DiscussVotes`) |
| `ObjectKey`   | `varchar(512)`  | NOT NULL | S3 object key. New uploads since 40.13 are keyed `org/{organizationId}/discuss/threads/...` (or `.../replies/...`); pre-40.13 keys are unaffected and keep serving — the key is read from this row, never recomputed. |
| `ContentType` | `varchar(100)`  | NOT NULL | e.g. `image/png`                                       |
| `OrderIndex`  | `integer`       | NOT NULL | 0-based display order                                  |
| `SizeBytes`   | `bigint`        | NOT NULL | uploaded byte size                                     |
| `CreatedAt`   | `timestamp with time zone` | NOT NULL |                                             |

Indexes: `IX_DiscussPhotos_OwnerType_OwnerId_OrderIndex`.

---

## Hierarchy Structure

```
Skills
└── Topics (multiple per skill)
    └── Lessons (multiple per topic)
        └── Exercises (multiple per lesson)
```

---

## MongoDB

### Collection: `chat_messages`

| Field            | Type     | Notes                                  |
|------------------|----------|----------------------------------------|
| `_id`            | ObjectId |                                        |
| `user_id`        | string   | References `Users.Id` (UUID as string) |
| `exercise_id`    | string   | References `Exercises.Id`              |
| `role`           | string   | `user` / `ai_character` / `system`     |
| `character_slug` | string   | NULL for non-character messages        |
| `content`        | string   |                                        |
| `metadata`       | object   | Arbitrary key-value pairs              |
| `created_at`     | date     |                                        |

### Collection: `chat_conversations`

| Field            | Type       | Notes                                  |
|------------------|------------|----------------------------------------|
| `_id`            | ObjectId   |                                        |
| `organizationId` | Guid       | Phase 40.13 — owning tenant. Mongo has no RLS, so this field is enforced entirely by `ChatConversationRepository`, the only class permitted to query this collection (asserted against the source tree by a unit test). |
| `participantIds` | Guid[]     | Always 2 elements, sorted              |
| `messages`       | ChatMessage[] | Embedded array                      |
| `lastMessageAt`  | date?      | Updated on each new message            |
| `createdAt`      | date       |                                        |

**ChatMessage (embedded):**

| Field      | Type     | Notes                          |
|------------|----------|--------------------------------|
| `id`       | string   | ObjectId as string             |
| `senderId` | Guid     |                                |
| `content`  | string   |                                |
| `sentAt`   | date     |                                |

**Index:** `participantIds` for efficient conversation lookup by user; `organizationId` added by the
40.13 rollout script (`docs/TENANCY/mongo/40.13_chat_conversations_organization_backfill.js`), not
run against any database yet — see `docs/DONT_FORGET.md`.

---

## Redis

| Key pattern                        | Type   | TTL      | Purpose                              |
|------------------------------------|--------|----------|--------------------------------------|
| `session:{userId}`                 | Hash   | 24h      | Session data                         |
| `league:weekly:{leagueId}`         | Sorted | Until EOW| Weekly team-progress ranking          |
| `user:xp_total:{userId}`           | String | —        | Cached total XP (invalidated on earn)|
| `org:{orgId}:presence:online`      | Sorted | —        | analytics-service online-presence (member=userId, score=last-seen unix sec); pruned to a 5-min window by the gauge updater — see [MONITORING.md](MONITORING.md). Phase 40.13 namespaced this per organization (was the platform-wide `presence:online`); the old key is never read after the rollout but has no TTL, so it is not removed automatically — see `docs/DONT_FORGET.md`. |
| `presence:organizations`           | Set    | —        | analytics-service registry of organization ids that have recorded presence (40.13) — how the platform-wide `app_users_online` gauge finds every organization with no database to query. |
| `org:{orgId}:notifications:inbox:{userId}` | List | `RetentionDays` (default 30) | notification-service per-user inbox. Phase 40.13 namespaced this per organization (was `notifications:inbox:{userId}`); old keys are never read after the rollout and expire on their own TTL. |
| `org:{orgId}:notifications:unread:{userId}` | String | `RetentionDays` (default 30) | notification-service unread counter, namespaced per organization since 40.13 (was `notifications:unread:{userId}`). |
| `org:{orgId}:notifications:chat-email:read:{userId}:{conversationId}` | String | `BookkeepingRetentionHours` | notification-service chat-email read watermark, namespaced per organization since 40.13 — a conversation belongs to one organization. |
| `notifications:chat-email:pending` | Sorted | —        | notification-service delayed unread-chat email queue. **Deliberately not namespaced** (40.13) — one due-time-ordered work list, organization travels inside each queued item instead, the same way it rides in a Kafka envelope. |
| `org:{orgId}:idem:{group}:{eventId}` | String | `Kafka:IdempotencyTtlDays` | Kafka consumer dedupe (Phase 40.11 added the `org:` prefix; an event whose envelope carries no organization keeps the historical `idem:{group}:{eventId}`) |
| `org:{orgId}:dialog:scenario-validation:v1:{sha256}` | String | 30d approved / 7d rejected | ai-service custom-scenario relevance verdict, keyed by a hash of the normalized text — see [CUSTOM_SCENARIO.md](CUSTOM_SCENARIO.md) |
| `org:{orgId}:voice:{userId}:day:{y}:{m}:{d}` / `:month:{y}:{m}` | String | end of window | ai-service voice-quota counters |

> **Phase 40.11 rule:** every ai-service Redis key is namespaced `org:{orgId}:`. Without it one
> organization's cached verdict answers another organization's request. Keys written before the
> prefix are unreachable by the current code and expire on their own TTL — nothing was flushed
> ([DECISIONS.md](DECISIONS.md)). **Phase 40.13 applied the same rule to notification-service's
> inboxes/counters/watermarks and analytics-service's presence sets** — both services have no
> database, so the Redis key *is* the tenant boundary. The one deliberate exception in either
> service is the notification-service delayed-email queue above, which stays a single un-prefixed
> list because it is a work queue, not a store of tenant data.

---

## Migrations history

One EF migration history per database — there is no shared one, and there has not been since the split. This table used to open with the monolith's migrations (`InitialSchema`, `AddUserRole`, `AddAchievements`, `AddNotifications`, `AddTechniques`, `AddDiscussPhotos`, `AddSkillStages` and the rest). **Those files no longer exist**: they were deleted from `main` with `src/backend/api` and survive only on the `monolith-legacy` branch. Every row below corresponds to a file actually present under `src/backend/<service>/Infrastructure/Data/Migrations/`, in per-service chronological order.

### `identity` (identity-service)

| Migration name | Date | Summary |
|---|---|---|
| `InitialIdentitySchema` | 2026-06-20 | Standalone `identity` database (Phase 2): `Users`, `UserProfiles`, `RefreshTokens`, `EmailVerificationCodes`, `DefaultAvatars`. The first extraction, and the one every other service replicates from. |
| `AddUniqueUserEmailIndex` | 2026-06-21 | Unique index on `Users.Email`. Email is globally unique across the whole platform, not per organization — which is what lets the invite flow match an invitee to an existing account. |
| `AddUniqueRefreshTokenIndex` | 2026-06-21 | Unique index on `RefreshTokens.Token`, so a token can never resolve to two rows. |
| `AddOutboxMessages` | 2026-06-21 | Phase 10.3. `OutboxMessages` table + relay for identity's `user.*` producers, converted from direct publishing to the outbox. |
| `AddOutboxMessageOrganizationId` | 2026-08-14 | Phase 40.3. Nullable `OrganizationId` on `OutboxMessages`, same as gamification and learning. |
| `AddMembership` | 2026-08-14 | Phase 40.6. `Memberships` — a user's role inside one organization, composite PK `(UserId, OrganizationId)` from day one, replacing the global `Users.Role` for org-scoped authorization. No FK on `OrganizationId`: the registry lives in `organization`-db. |
| `AddInvite` | 2026-08-15 | Phase 40.7, and the whole of that block. `Invites` with `EnableTenantRls` (strict), a **globally** unique index on `TokenHash` and `IX_Invites_OrganizationId_Email`. The unique index is deliberately not organization-first — acceptance finds the invite by hash alone, because the caller has no organization yet. Only the hash is stored, so a database dump never yields a usable invite. |
| `AddOrganizationAuthConfiguration` | 2026-08-15 | Phase 40.8. `OrganizationAuthConfigurations` — how one organization's people sign in. Lives in identity-db rather than `organization`-db because it is read on `POST /auth/login/start`, before any organization context exists. Deliberately **no RLS**, unlike `Invites`. |
| `AddOrganizationReplicaAndImpersonationAudit` | 2026-08-15 | Phase 40.9. Two tables: `OrganizationReplicas` (a local read model of the registry, fed by `organization.*` events, so login can resolve an organization without a cross-service call) and `ImpersonationAuditEntries` (RLS-enabled — every time Sellevate staff enters a customer's account, permanently recorded). |
| `RefreshTenantPoliciesForPlatformStaff` | 2026-08-16 | Re-applies every tenant policy in this database so its `USING` clause also admits validated platform staff (`app.platform_mode`), leaving `WITH CHECK` alone — visibility widens, authorship does not. The policy text is not written in the migration: it calls the same `EnableTenantRls`/`EnableTenantRlsForContent` helpers the original migration called, which now drop and re-create rather than fail on an existing policy, so there is never a second copy of the policy SQL to drift. `Down` passes `admitPlatformStaff: false` through the same helpers, making the rollback the exact pre-change policy rather than an approximation. **The model is untouched** — nothing is added, dropped or altered, so the snapshot is identical to the previous one, which is expected rather than an oversight. |

### `learning` (learning-service)

| Migration name | Date | Summary |
|---|---|---|
| `InitialLearningSchema` | 2026-06-20 | Standalone `learning` database: `Skills`, `SkillStages`, `Topics`, `UserSkillProgressRecords`, `Lessons`, `Exercises`, `UserLessonProgressRecords`, `UserExerciseAttempts`, `ExerciseTypePrompts`, `ReferenceMaterials`, `DailyQuotes`, `Techniques`, `TechniqueSkills`, `TechniqueCoaches`, `UserTechniqueProgress`, and `UserReplicas` (read-model). Owned by learning-service, not the monolith `AppDbContext`. |
| `AddOutboxMessages` | 2026-06-21 | Phase 10.3. `OutboxMessages` table + relay; learning's `exercise/lesson/skill.completed` producers were converted to stage the enqueue inside the business `SaveChangesAsync`. |
| `AddOutboxMessageOrganizationId` | 2026-08-14 | Phase 40.3. Nullable `OrganizationId` on `OutboxMessages`. |
| `AddOrganizationId` | 2026-08-15 | Phase 40.10, first Stage-C service. `OrganizationId uuid NOT NULL` on `UserSkillProgressRecords`, `UserLessonProgressRecords`, `UserExerciseAttempts`, `UserTechniqueProgress` (added with an all-zeros placeholder default, which is then dropped) and `OrganizationId uuid NULL` on `Skills`, `Topics`, `Lessons`, `Exercises`, `Techniques`, `ReferenceMaterials` (`NULL` = global library). RLS on all ten: `EnableTenantRls` for the progress tables, `EnableTenantRlsForContent` for the content ones. Contains **no `CREATE INDEX` and no backfill** on purpose — both are operational steps (`docs/TENANCY/sql/40.10_learning_organization_backfill.sql`, `..._indexes_concurrently.sql`), because the migration runs from `Database.Migrate()` at startup where a long index build would stall readiness. |
| `RefreshTenantPoliciesForPlatformStaff` | 2026-08-16 | Re-applies every tenant policy in this database so its `USING` clause also admits validated platform staff (`app.platform_mode`), leaving `WITH CHECK` alone — visibility widens, authorship does not. The policy text is not written in the migration: it calls the same `EnableTenantRls`/`EnableTenantRlsForContent` helpers the original migration called, which now drop and re-create rather than fail on an existing policy, so there is never a second copy of the policy SQL to drift. `Down` passes `admitPlatformStaff: false` through the same helpers, making the rollback the exact pre-change policy rather than an approximation. **The model is untouched** — nothing is added, dropped or altered, so the snapshot is identical to the previous one, which is expected rather than an oversight. |
| `AddLessonVersioning` | 2026-08-17 | Phase 40.15, Stage D. `ParentLessonId uuid NULL` (self-FK, `RESTRICT`), `Slug varchar(160) NOT NULL` and `IsArchived boolean NOT NULL` on `Lessons`; new table `LessonVersions` with `EnableTenantRlsForContent`, two check constraints and the `LessonVersions_reject_frozen_change` trigger. **Unlike 40.10–40.13 this migration creates its indexes itself**, and there is no companion `_indexes_concurrently.sql`: `LessonVersions` is created empty so its indexes are built over zero rows, and `Lessons` is a content table of a few hundred rows where the build is milliseconds — the same judgement 40.13 made for the four small gamification tables. Slug uniqueness is correctness, and leaving it unenforced until someone remembers a script is the worse trade. The slug backfill is also in-migration (derived from each row's own primary key), so unlike 40.9–40.13 there is **no maintenance window and no interval in which data is invisible**. Verification script: `docs/TENANCY/sql/40.15_lesson_versioning_verify.sql` (read-only). |
| `AddProgressLessonVersionBinding` | 2026-08-17 | Phase 40.16, Stage D. `LessonVersionId uuid NULL` on `UserExerciseAttempts` and `UserLessonProgressRecords`, and nothing else. Both columns are nullable, so Postgres 11+ adds them as a catalogue-only change — no rewrite, no long lock — which is why this one is allowed to run inside `Database.Migrate()` on two tables that grow with usage. **Indexes are not created here** (they are declared in the entity configurations so the model snapshot carries them, and built by `docs/TENANCY/sql/40.16_progress_version_indexes_concurrently.sql`), and **the historical backfill is not here either**: it needs a "version 1" to point at, and that snapshot's `ContentHash` is defined over the exact bytes `LessonSnapshotSerializer` emits — SQL cannot reproduce them, because Postgres orders `jsonb` keys by length and then bytes. `LessonVersionBackfill` mints those versions at startup; `docs/TENANCY/sql/40.16_progress_version_backfill.sql` then binds the existing rows. No foreign key to `LessonVersions` on purpose (see the `UserExerciseAttempts` notes above). **No window in which any data is invisible**, unlike 40.10–40.13: nothing filters on these columns. |
| `AddProgramVersioning` | 2026-08-17 | Phase 40.17, Stage D. Three new **strict tenant** tables — `ProgramVersions`, `ProgramItems`, `ProgramEnrollments` — with `EnableTenantRls` (plain equality, not the content flavour: there is no global programme), three check constraints and two freeze triggers (`ProgramVersions_reject_frozen_change`, `ProgramItems_reject_frozen_change`). **Indexes are created here and there is no companion `_indexes_concurrently.sql`** — the same call 40.15 made, for the same reason: all three tables are created empty by this very migration, so every index is built over zero rows, and two of them (one draft per organization, one pin per learner) are correctness constraints that must not wait for a script somebody has to remember. **No backfill and no maintenance window**: the migration mints no programme version and enrolls nobody, nothing filters on the new tables, and an organization without a published programme behaves exactly as it did before — learners read the live tree, unpinned. Verification script: `docs/TENANCY/sql/40.17_program_versioning_verify.sql` (read-only, never executed). |
| `AddContentOverrides` | 2026-08-17 | Phase 40.18, Stage D. `ParentTechniqueId uuid NULL` (self-FK, `RESTRICT`), `BaseContentHash varchar(64) NULL` and `IsArchived boolean NOT NULL DEFAULT false` on `Techniques`; the same three (as `ParentMaterialId`) on `ReferenceMaterials`; two indexes on the parent pointers; and three CHECK constraints — `CK_Techniques_OverrideHasOwner`, `CK_ReferenceMaterials_OverrideHasOwner` and `CK_Lessons_OverrideHasOwner` — saying that a row with a parent always has an owning organization. `Lessons` needed nothing else: 40.15 built its override columns already. **No companion `_indexes_concurrently.sql` and no backfill, both deliberate.** Nullable columns and a NOT NULL boolean with a constant default are catalog changes on Postgres 11+; the two indexes are built over tables holding tens to hundreds of rows; the CHECKs scan the same tens. Nothing fills a column on an existing row, so — as in 40.15 and 40.17 — **there is no maintenance window and no interval in which data is invisible**: every existing row keeps a null parent, which is the correct value for "this is the library, not somebody's copy". Verification script: `docs/TENANCY/sql/40.18_content_overrides_verify.sql` (read-only, never executed). |
| `AddOrganizationProfileReplica` | 2026-08-17 | Phase 40.19, Stage D. One new table, `OrganizationProfileReplicas` — a read-only copy of organization-service's `OrganizationProfiles`, fed by the `organization.profile.updated` consumer. **`EnableTenantRls` (plain equality), not `EnableTenantRlsForContent`**, and that is the interesting bit: every other table this folder added since 40.15 is content, where a NULL owner means "the shared library". Here a NULL owner would mean every organization's product name and banned claims at once, so the tenant column is the primary key and a row without an owner is unrepresentable. **No backfill, no maintenance window, no companion `_indexes_concurrently.sql`** — the table is created empty, is fed by events, holds at most one row per customer, and its only query is a lookup by that primary key, so there is no second access path to index. What *is* owed to a human is a one-time republish of profiles saved before this phase (`docs/DONT_FORGET.md`). Verification: `docs/TENANCY/sql/40.19_organization_profile_verify.sql` (read-only, never executed). |
| `AddAssignments` | 2026-08-17 | Phase 40.21, **Stage E opens**. Two new **strict tenant** tables — `Assignments`, `AssignmentProgressRecords` — with `EnableTenantRls` (plain equality, not the content flavour: there is no global assignment), fifteen check constraints, one `ON DELETE RESTRICT` foreign key and the `Assignments_reject_frozen_change` trigger. **Indexes are created here and there is no companion `_indexes_concurrently.sql`** — the same call 40.15/40.17/40.18 made: both tables are created empty by this very migration, so every index is built over zero rows, and one of them (one progress row per person per assignment) is a correctness constraint that must not wait for a script somebody has to remember. **No backfill and no maintenance window**: nothing filters on the new tables and no existing row gains a meaning. The one constraint worth naming is `CK_Assignments_CompletionRule` — the column has no default and the API cannot omit it, so "completion means opening everything" has no resting place in the schema (docs/TENANCY/ASSIGNMENTS.md §1.1); its vocabulary stays 40.22's. `AssignmentProgressRecords` has **no writer at all** until 40.23 resolves an audience. Verification script: `docs/TENANCY/sql/40.21_assignments_verify.sql` (read-only, never executed). |
| `AddAssignmentThresholdEvaluation` | 2026-08-17 | Phase 40.22. One new **strict tenant** table, `UserDialogScores` — one row per graded practice conversation, unique on `(OrganizationId, UserId, SessionId)` — plus three check constraints. That uniqueness is the idempotency guarantee, not a performance choice: without it a redelivered `dialog.evaluated` writes a second row, `AttemptCount` climbs while nobody practised, and the line the РОП acts on ("tried 4 times and did not reach the bar") becomes a lie. No backfill is possible, let alone needed: `dialog.evaluated` carried no grade before this block, so conversations graded earlier are invisible to every assignment, permanently. Verification script: `docs/TENANCY/sql/40.22_completion_threshold_verify.sql` (read-only, never executed). *(Recorded retroactively in 40.23 — the row was missing from this ledger.)* |
| `AddAssignmentDeadlineNotice` | 2026-08-17 | Phase 40.23. **One nullable column**, `Assignments."DeadlineNoticeSentAt"` — when the "deadline is close" notice went out for the deadline the row currently has; cleared whenever the deadline changes. No index, no backfill, no `_indexes_concurrently.sql`, no maintenance window: `Assignments` is empty in every deployed database (nothing could create one before 40.21, and 40.21 shipped without a screen), so the column is added over zero rows and no existing row changes meaning. The index is omitted deliberately rather than forgotten — see the note in the `Assignments` section above. This is the migration behind the block that finally gives `AssignmentProgressRecords` a row **creator**: the audience fan-out. Verification script: `docs/TENANCY/sql/40.23_assignment_fanout_verify.sql` (read-only, never executed). |
| `AddAssignmentRepeats` | 2026-08-18 | Phase 40.24. **Two nullable columns** — `Assignments."RepeatOfAssignmentId"` (self-referencing FK, `ON DELETE RESTRICT`) and `"RepeatWaveIndex"` — one **unique partial index** over them, three check constraints, and a `CREATE OR REPLACE` of the 40.21 freeze trigger that adds both columns to its frozen set. The index is created here rather than deferred to a `_indexes_concurrently.sql` because it is a *correctness* constraint, not a performance one: a wave has been issued exactly when its row exists, so this index is the only thing standing between two sweep ticks racing inside one window and the same shortened assignment landing on the same team twice. A sent-ness column of the kind 40.23 added was **impossible** here — the origin may be `closed`, and the freeze trigger refuses any update to a closed row. `CK_Assignments_RepeatNoCascade` (a repeat carries no schedule of its own) is what stops the fan-out from becoming exponential. No backfill, no maintenance window: `Assignments` is empty in every deployed database. Verification script: `docs/TENANCY/sql/40.24_assignment_repeats_verify.sql` (read-only, never executed). |
| `AddDialogReviewNotes` | 2026-08-18 | Phase 40.25. One new **strict tenant** table, `DialogReviewNotes` — a manager's written note on one practice conversation — with `EnableTenantRls` and three indexes, all created here because the table is created empty by this very migration. No backfill: no review existed before this block. |
| `AddContentGenerationJobs` | 2026-08-18 | Phase 40.27. `ContentGenerationJobs` (strict `EnableTenantRls`, three indexes) — the queue behind «сгенерировать урок из материалов», one row per requested generation with its step state. Created empty, so the indexes are built over zero rows and there is no companion `_indexes_concurrently.sql`. |
| `AddContentGenerationSufficiency` | 2026-08-18 | Phase 40.28. Two columns on `ContentGenerationJobs` — `Insufficiency` and `StructuredMaterialLength` — so a job can end in «материала не хватает, вот чего именно» instead of quietly producing a thin lesson. Nullable additions, catalog-only, no index, no backfill. |
| `AddTeamSkillGaps` | 2026-08-18 | Phase 40.31. `TeamSkillGapDismissals` (strict `EnableTenantRls`, two indexes) plus a `GapSourceRef` column — a dismissed gap stays dismissed instead of returning on the next recompute. Created empty; no backfill. |
| `AddContentAdaptationBatches` | 2026-08-18 | Phase 40.32. Two new **strict tenant** tables, `ContentAdaptationJobs` and `ContentAdaptationItems`, with `EnableTenantRls` on both and seven indexes — a batch that rewrites existing content to one organization's profile, one row per batch and one per item so a partial failure is visible per item rather than sinking the batch. Both created empty by this migration; no backfill, no maintenance window. |

### `ai` (ai-service)

| Migration name | Date | Summary |
|---|---|---|
| `InitialAiSchema` | 2026-06-20 | Standalone `ai` database (Phase 6): `DialogBundles`, `DialogModes` and a local `UserReplicas` read model. Owned by ai-service, not the monolith. |
| `AddDialogBundleIsHidden` | 2026-07-09 | `DialogBundles.IsHidden` boolean NOT NULL DEFAULT false — a bundle can be retired from the learner-facing list without deleting it and the sessions that point at it. |
| `AddOrganizationId` | 2026-08-15 | Phase 40.11. `OrganizationId uuid NULL` on `DialogBundles` and `DialogModes` with `EnableTenantRlsForContent` — the content flavour, because `NULL` is the shared scenario library every organization sees. Read indexes are rebuilt by `docs/TENANCY/sql/40.11_ai_organization_indexes_concurrently.sql`; this block also namespaced every ai-service Redis key `org:{orgId}:`. |
| `RefreshTenantPoliciesForPlatformStaff` | 2026-08-16 | Re-applies every tenant policy in this database so its `USING` clause also admits validated platform staff (`app.platform_mode`), leaving `WITH CHECK` alone — visibility widens, authorship does not. The policy text is not written in the migration: it calls the same `EnableTenantRls`/`EnableTenantRlsForContent` helpers the original migration called, which now drop and re-create rather than fail on an existing policy, so there is never a second copy of the policy SQL to drift. `Down` passes `admitPlatformStaff: false` through the same helpers, making the rollback the exact pre-change policy rather than an approximation. **The model is untouched** — nothing is added, dropped or altered, so the snapshot is identical to the previous one, which is expected rather than an oversight. |
| `AddDialogModeOverrides` | 2026-08-17 | Phase 40.18, Stage D. `ParentModeId uuid NULL` (self-FK, `RESTRICT`), `BaseContentHash varchar(64) NULL` and an index on the parent pointer, plus `CK_DialogModes_OverrideHasOwner`, on `DialogModes`. `DialogBundles` gets nothing — a bundle carries no prompt, and copying one would fork its whole mode list (`docs/DECISIONS.md`, 2026-08-18). An override keeps its parent's `BundleId` and `Key`, which the 40.11 unique indexes already permit because the composite one is filtered to non-global rows. Same shape as the learning-service migration above: catalog-only column changes, one small index, no backfill, no window. |
| `AddOrganizationProfileReplica` | 2026-08-17 | Phase 40.19, Stage D. The same table, in `ai`, for the same reason — persona and feedback prompts resolve `{{organization.*}}` and enforce `banned_claims` locally. **This is the first non-content table in ai-db**, which is exactly why the RLS flavour is worth stating: both neighbours (`DialogBundles`, `DialogModes`) legitimately use `IS NULL OR = current`, so copying the neighbouring policy is the natural mistake and would hand one customer's compliance list to everybody. Otherwise identical to the learning-service migration: catalog-only, empty table, no backfill, no window, no index script. |
| `AddOrganizationQuotas` | 2026-08-18 | Phase 40.33, Stage G. Two new **strict tenant** tables in `ai` — `OrganizationQuotas` (one row per organization that has been given limits of its own) and `AiUsageRecords` (what each organization spent on each model in each UTC month) — both with `EnableTenantRls` (plain equality, never the content flavour: there is no global allowance and no global bill), both with the tenant column leading the primary key so a row without an owner is unrepresentable. **Indexes are created here and there is no companion `_indexes_concurrently.sql`**: both tables are created empty by this migration, and every read is a prefix scan on the leading key columns of a table holding one row per organization per model per month. **No backfill, and none is possible or needed** — no spend was ever recorded before this migration, and an absent `OrganizationQuotas` row deliberately means «the platform defaults in `AiQuotas:Default…`» rather than «unmetered», so every organization is metered from the first request after the deploy without a single row being written. Verification: `docs/TENANCY/sql/40.33_ai_quotas_verify.sql` (read-only, never executed). |

### `gamification` (gamification-service)

| Migration name | Date | Summary |
|---|---|---|
| `InitialGamificationSchema` | 2026-06-20 | Standalone `gamification` database (Phase 7): `UserXpRecords`, `UserStreaks`, `GamificationSettings`, `ExerciseTypeRewards`, `StreakMilestones`, `Achievements`, `UserAchievements`, `Leagues`, `LeagueTiers`, `LeagueMemberships`, `LeagueSettings`, the `UserLearningProgress` projection and a local `UserReplicas`. |
| `AddOutboxMessages` | 2026-06-21 | Phase 10.3, the reference implementation. `OutboxMessages` table so a state change and its event publish commit in one transaction; the relay forwards each stored envelope to Kafka verbatim. |
| `AddXpRecordSourceEventIdIdempotency` | 2026-06-21 | `UserXpRecords.SourceEventId` plus a **partial** unique index `WHERE "SourceEventId" IS NOT NULL`, and a unique index on `UserStreaks.UserId`. Partial because rows granted by something other than an event legitimately have no source id, and a plain unique index would let only one of them exist. |
| `AddLeagueUniqueConstraints` | 2026-06-21 | Makes `IX_Leagues_WeekStartDate_Tier` unique so two concurrent weekly-rollover calls cannot both create the same league. |
| `AddOutboxMessageOrganizationId` | 2026-08-14 | Phase 40.3. Nullable `OrganizationId` on `OutboxMessages` so the tenant travels with the event envelope; nullable because platform-level events legitimately have no organization. |
| `AddOrganizationId` | 2026-08-15 | Phase 40.13. `OrganizationId uuid NOT NULL` (placeholder default) on `UserXpRecords`, `UserStreaks`, `UserAchievements`, `UserLearningProgress`, `Leagues`, `LeagueMemberships`, `LeagueSettings` — strict `EnableTenantRls` on all seven (no global content flavour in this database). `Achievements`, `LeagueTiers`, `GamificationSettings`, `StreakMilestones`, `ExerciseTypeRewards`, `UserReplicas`, `OutboxMessages` get nothing. Unlike 40.10–40.12, **this migration does swap four unique constraints in place** (`Leagues.(WeekStartDate,Tier)`, `UserStreaks.(UserId)`, `UserAchievements.(UserId,AchievementId)`, `UserLearningProgress`'s primary key) because each was load-bearing for correctness in the deploy-to-script window and every affected table holds at most a row per user/week. Read indexes on `UserXpRecords`/`LeagueMemberships` stay out, rebuilt by `docs/TENANCY/sql/40.13_gamification_organization_indexes_concurrently.sql`. |
| `RefreshTenantPoliciesForPlatformStaff` | 2026-08-16 | Re-applies every tenant policy in this database so its `USING` clause also admits validated platform staff (`app.platform_mode`), leaving `WITH CHECK` alone — visibility widens, authorship does not. The policy text is not written in the migration: it calls the same `EnableTenantRls`/`EnableTenantRlsForContent` helpers the original migration called, which now drop and re-create rather than fail on an existing policy, so there is never a second copy of the policy SQL to drift. `Down` passes `admitPlatformStaff: false` through the same helpers, making the rollback the exact pre-change policy rather than an approximation. **The model is untouched** — nothing is added, dropped or altered, so the snapshot is identical to the previous one, which is expected rather than an oversight. |

### `social` (social-service)

| Migration name | Date | Summary |
|---|---|---|
| `InitialSocialSchema` | 2026-06-20 | Standalone `social` database: `Friendships`, all `Discuss*` tables, and `UserReplicas` (read-model). Owned by social-service, not the monolith `AppDbContext`. |
| `FriendshipCanonicalPair` | 2026-06-21 | Two computed columns on `Friendships` — `CanonicalLowId` = `LEAST(RequesterId, AddresseeId)`, `CanonicalHighId` = `GREATEST(...)` — and a unique index over the pair, so A→B and B→A cannot both exist as separate friendships. |
| `AddOrganizationId` | 2026-08-16 | Phase 40.13. `OrganizationId uuid NOT NULL` (placeholder default) on `Friendships`, `DiscussThreads`, `DiscussReplies`, `DiscussVotes`, `DiscussThreadTags`, `DiscussPhotos` — strict `EnableTenantRls` on all six. `OrganizationId uuid NULL` on `DiscussTags` (`NULL` = curated vocabulary shared by every organization), `EnableTenantRlsForContent`. `UserReplicas` gets nothing. Two unique swaps happen in-migration, not the concurrent-rebuild script: `DiscussTags.Slug` → `(OrganizationId, Slug)` + a partial unique index over the global rows, and `Friendships.(RequesterId,AddresseeId)` (+ the canonical-pair index) → organization-first. Every read index stays out, rebuilt by `docs/TENANCY/sql/40.13_social_organization_indexes_concurrently.sql`. Mongo `chat_conversations` gets `organizationId` via a separate script (`docs/TENANCY/mongo/40.13_chat_conversations_organization_backfill.js`), not this migration. |
| `RefreshTenantPoliciesForPlatformStaff` | 2026-08-16 | Re-applies every tenant policy in this database so its `USING` clause also admits validated platform staff (`app.platform_mode`), leaving `WITH CHECK` alone — visibility widens, authorship does not. The policy text is not written in the migration: it calls the same `EnableTenantRls`/`EnableTenantRlsForContent` helpers the original migration called, which now drop and re-create rather than fail on an existing policy, so there is never a second copy of the policy SQL to drift. `Down` passes `admitPlatformStaff: false` through the same helpers, making the rollback the exact pre-change policy rather than an approximation. **The model is untouched** — nothing is added, dropped or altered, so the snapshot is identical to the previous one, which is expected rather than an oversight. |

### `company` (company-service)

| Migration name | Date | Summary |
|---|---|---|
| `InitialCompanySchema` | 2026-07-09 | Standalone `company` database: `Companies`, `CallLogEntries`, `PracticeCalls` tables. Owned by company-service (port 5009). |
| `AddCompanyContacts` | 2026-07-09 | `CompanyContacts` table (mini-CRM, Phase 39.9); `CallLogEntries.ContactId` nullable FK → `CompanyContacts(Id)` ON DELETE SET NULL. |
| `AddCompanyStatus` | 2026-07-10 | `Companies.Status` varchar(32) NOT NULL DEFAULT 'Lead' (status pipeline, Phase 39.10); plain `AddColumn` with a Postgres column default, so existing rows read as `Lead` without a separate `UPDATE`. |
| `AddCompanyFollowUp` | 2026-07-10 | `Companies.NextActionAt` (timestamptz, nullable), `NextActionNote` (varchar(2000), nullable), `FollowUpNotifiedAt` (timestamptz, nullable) (follow-up reminders, Phase 39.11); sparse index `IX_Companies_NextActionAt` (filtered `WHERE "NextActionAt" IS NOT NULL`) keeps the reminder poll cheap. |
| `AddCompanyBriefing` | 2026-07-10 | `Companies.BriefingContent` (text, nullable), `BriefingGeneratedAt` (timestamptz, nullable) (AI pre-call briefing cache, Phase 39.12); plain `AddColumn`, no index (read only via the single-row `GET/POST /companies/{id}/briefing`). |
| `AddCompanyPersonas` | 2026-07-10 | `CompanyPersonas` table (AI persona generation, Phase 39.14); FK → `Companies(Id)` ON DELETE CASCADE. |
| `AddCompanyReadiness` | 2026-07-10 | `Companies.ReadinessJson` (text, nullable), `ReadinessGeneratedAt` (timestamptz, nullable) (AI readiness-score cache, Phase 39.16); plain `AddColumn`, no index (read/written only via the single-row `GET /companies/{id}/readiness`). |
| `AddCompanyReadinessNoFeedbackCache` | 2026-07-11 | `Companies.ReadinessNoFeedbackUntil` (timestamptz, nullable) — negative-cache expiry for the "ai-service returned 204 / no usable feedback yet" readiness result (PR #26 review fast-follow, 39.17); plain `AddColumn`, no index (read/written only via the single-row `GET /companies/{id}/readiness`). |
| `AddOrganizationId` | 2026-08-15 | Phase 40.12. `OrganizationId uuid NOT NULL` (placeholder default, then dropped) on `Companies`, `CallLogEntries`, `PracticeCalls`, `CompanyContacts`, `CompanyPersonas`, all with the **strict** `EnableTenantRls` — there is no global content in this database, so the content flavour would be wrong on every table. Read indexes are rebuilt by `docs/TENANCY/sql/40.12_company_organization_indexes_concurrently.sql`. |
| `RefreshTenantPoliciesForPlatformStaff` | 2026-08-16 | Re-applies every tenant policy in this database so its `USING` clause also admits validated platform staff (`app.platform_mode`), leaving `WITH CHECK` alone — visibility widens, authorship does not. The policy text is not written in the migration: it calls the same `EnableTenantRls`/`EnableTenantRlsForContent` helpers the original migration called, which now drop and re-create rather than fail on an existing policy, so there is never a second copy of the policy SQL to drift. `Down` passes `admitPlatformStaff: false` through the same helpers, making the rollback the exact pre-change policy rather than an approximation. **The model is untouched** — nothing is added, dropped or altered, so the snapshot is identical to the previous one, which is expected rather than an oversight. |

### `organization` (organization-service)

| Migration name | Date | Summary |
|---|---|---|
| `InitialOrganizationSchema` | 2026-08-14 | Standalone `organization` database: `Organizations` (tenant registry, unique `Slug`) and `OrganizationProfiles` (1:1 tenant-data row, RLS via `EnableTenantRls`). Owned by organization-service (port 5010), Phase 40.5. |
| `RefreshTenantPoliciesForPlatformStaff` | 2026-08-16 | Re-applies every tenant policy in this database so its `USING` clause also admits validated platform staff (`app.platform_mode`), leaving `WITH CHECK` alone — visibility widens, authorship does not. The policy text is not written in the migration: it calls the same `EnableTenantRls`/`EnableTenantRlsForContent` helpers the original migration called, which now drop and re-create rather than fail on an existing policy, so there is never a second copy of the policy SQL to drift. `Down` passes `admitPlatformStaff: false` through the same helpers, making the rollback the exact pre-change policy rather than an approximation. **The model is untouched** — nothing is added, dropped or altered, so the snapshot is identical to the previous one, which is expected rather than an oversight. |
| `AddDemoRequests` | 2026-08-19 | `DemoRequests` — landing-page lead capture, written by an anonymous endpoint. Deliberately **no** `EnableTenantRls` call and no `OrganizationId`: a lead precedes any tenant, so there is nothing to scope it to, and platform-admin authorization replaces RLS here. Non-unique index on `WorkEmail` (the cooldown lookup — one company may legitimately ask twice over time) and a descending index on `CreatedAt` (the admin list order). |
| `AddDemoRequestMarketingConsent` | 2026-08-19 | Additive `AddColumn` only: nullable `MarketingConsentGivenAt` on `DemoRequests`, splitting marketing consent off the required data-processing consent because 152-ФЗ/GDPR treat them as separate purposes. Written as a second migration rather than an edit to `AddDemoRequests` so the sequence stays an honest record of what happened. |
| `MakeDemoRequestPhoneRequired` | 2026-08-20 | `AlterColumn` only: `DemoRequests.Phone` from nullable to `NOT NULL` (owner decision — the sales motion this form feeds is phone-first). The `Qualified → Approved` rename the same day needed **no** migration: `Status` is `character varying(32)` via `HasConversion<string>()` with no `CHECK` constraint naming the allowed values, so nothing in the database referenced the old member name. |
| `AddDemoRequestProvisioning` | 2026-08-20 | Adds `OrganizationId` (uuid, nullable), `ProvisioningState` (`character varying(32)`, `HasConversion<string>()`, default `'NotProvisioned'`), `BootstrapInviteId` (uuid, nullable), `BootstrapAdminEmail` (`character varying(200)`, nullable) and `ProvisionedAt` (timestamptz, nullable) to `DemoRequests`, plus a **partial unique index** on `OrganizationId` (`WHERE "OrganizationId" IS NOT NULL`) — two leads can never claim the same organization, the database-level backstop behind the application-level row lock in `DemoRequestProvisioningService`. Generated only; not run against any database in this session (no local Postgres available here). |


---

## company database (company-service)

Standalone Postgres database `company`. Owned by `company-service` (port 5009). Connection string key: `ConnectionStrings:Postgres`.

### Table: `Companies`

| Column        | Type         | Constraints                      |
|---------------|--------------|----------------------------------|
| `Id`          | uuid         | PK                               |
| `UserId`      | uuid         | NOT NULL, INDEX                  |
| `Name`        | varchar(200) | NOT NULL                         |
| `Description` | varchar(8000)| NOT NULL, DEFAULT ''             |
| `Status`      | varchar(32)  | NOT NULL, DEFAULT 'Lead'         |
| `NextActionAt`| timestamptz  | NULL                             |
| `NextActionNote` | varchar(2000) | NULL                          |
| `FollowUpNotifiedAt` | timestamptz | NULL                      |
| `BriefingContent` | text     | NULL                             |
| `BriefingGeneratedAt` | timestamptz | NULL                     |
| `ReadinessJson` | text       | NULL                             |
| `ReadinessGeneratedAt` | timestamptz | NULL                   |
| `ReadinessNoFeedbackUntil` | timestamptz | NULL               |
| `CreatedAt`   | timestamptz  | NOT NULL                         |
| `UpdatedAt`   | timestamptz  | NOT NULL                         |

**Indexes:** `IX_Companies_UserId`, `IX_Companies_NextActionAt` (filtered `WHERE "NextActionAt" IS NOT NULL`)

`Status` (Phase 39.10) is one of `Lead | Contacted | MeetingScheduled | DealWon | DealLost`,
stored as its string name (`HasConversion<string>()`, not the numeric enum value) so the column
stays human-readable in the database.

`NextActionAt`/`NextActionNote`/`FollowUpNotifiedAt` (Phase 39.11 — follow-up reminders):
`NextActionAt` is the scheduled follow-up due date; `NextActionNote` a free-form note;
`FollowUpNotifiedAt` is set by the reminder background service once `company.followup.due` has
been published for the current `NextActionAt`, and is reset to `null` whenever `NextActionAt` is
rescheduled (see `docs/API_CONTRACTS.md`). All three are nullable and independent of `Status`.

`BriefingContent`/`BriefingGeneratedAt` (Phase 39.12 — AI pre-call briefing): a cache of the
markdown cheat sheet returned by ai-service's `POST /ai/companies/briefing`, written by
`POST /companies/{id}/briefing` and read back by `GET /companies/{id}/briefing`. Both null until
the first generation; overwritten (not versioned/appended) on every regeneration.

`ReadinessJson`/`ReadinessGeneratedAt` (Phase 39.16 — AI readiness score): a cache of the
`{score, strengths, gaps, recommendation}` JSON returned by ai-service's
`POST /ai/companies/readiness`, both written and read by the single `GET /companies/{id}/readiness`
endpoint (self-generates on a cache miss). Both null until first generated, and **cleared back to
null** whenever a new practice call is created (`POST /companies/{id}/practice-calls`) — the
cache-invalidation trigger for this feature — so the next `GET` regenerates from the fresh
practice-call list instead of serving a stale score.

`ReadinessNoFeedbackUntil` (39.17 PR #26 review fast-follow — negative readiness cache): set to
"now + 2 minutes" whenever ai-service fans out across the company's practice sessions and comes
back with `204` (no usable feedback text found yet). While this timestamp is set and in the
future, `GET /companies/{id}/readiness` short-circuits to the empty result without re-running the
fan-out. Cleared back to `null` alongside `ReadinessJson`/`ReadinessGeneratedAt` whenever a new
practice call is created, and also cleared once a real (non-204) readiness result is generated.
Left `null` (not written at all) for the *other* "no data" case — a company with zero practice
calls — since that path never reaches ai-service and has nothing expensive to avoid re-running.

### Table: `CallLogEntries`

| Column        | Type         | Constraints                                                |
|---------------|--------------|-------------------------------------------------------------|
| `Id`          | uuid         | PK                                                          |
| `CompanyId`   | uuid         | NOT NULL, FK → Companies(Id) ON DELETE CASCADE              |
| `UserId`      | uuid         | NOT NULL                                                    |
| `ContactId`   | uuid         | NULL, FK → CompanyContacts(Id) ON DELETE SET NULL           |
| `ContactName` | varchar(200) | NOT NULL                                                    |
| `Subject`     | varchar(4000)| NOT NULL                                                    |
| `Outcome`     | varchar(4000)| NOT NULL                                                    |
| `OccurredAt`  | timestamptz  | NOT NULL                                                    |
| `CreatedAt`   | timestamptz  | NOT NULL                                                    |
| `UpdatedAt`   | timestamptz  | NOT NULL                                                    |

**Indexes:** `IX_CallLogEntries_CompanyId_OccurredAt` (CompanyId ASC, OccurredAt DESC), `IX_CallLogEntries_ContactId`

`ContactId` is optional and independent of `ContactName`: the free-text name is always stored so the log stays readable even after the linked contact is deleted (deleting a `CompanyContact` sets `ContactId` to `NULL` on its logs, `ContactName` is untouched).

### Table: `PracticeCalls`

| Column            | Type          | Constraints                                        |
|-------------------|---------------|----------------------------------------------------|
| `Id`              | uuid          | PK                                                 |
| `CompanyId`       | uuid          | NOT NULL, FK → Companies(Id) ON DELETE CASCADE     |
| `UserId`          | uuid          | NOT NULL                                           |
| `DialogSessionId` | text          | NOT NULL                                           |
| `Goal`            | varchar(1000) | NOT NULL                                           |
| `CreatedAt`       | timestamptz   | NOT NULL                                           |

**Indexes:** `IX_PracticeCalls_CompanyId_CreatedAt` (CompanyId ASC, CreatedAt DESC)

### Table: `CompanyContacts` (Phase 39.9 — mini-CRM)

| Column       | Type          | Constraints                                    |
|--------------|---------------|-------------------------------------------------|
| `Id`         | uuid          | PK                                              |
| `CompanyId`  | uuid          | NOT NULL, FK → Companies(Id) ON DELETE CASCADE  |
| `UserId`     | uuid          | NOT NULL                                        |
| `Name`       | varchar(200)  | NOT NULL                                        |
| `Position`   | varchar(200)  | NOT NULL, DEFAULT ''                            |
| `Notes`      | varchar(2000) | NOT NULL, DEFAULT ''                            |
| `CreatedAt`  | timestamptz   | NOT NULL                                        |
| `UpdatedAt`  | timestamptz   | NOT NULL                                        |

**Indexes:** `IX_CompanyContacts_CompanyId_CreatedAt` (CompanyId ASC, CreatedAt DESC)

### Table: `CompanyPersonas` (Phase 39.14 — AI persona generation for practice calls)

| Column        | Type          | Constraints                                    |
|---------------|---------------|-------------------------------------------------|
| `Id`          | uuid          | PK                                              |
| `CompanyId`   | uuid          | NOT NULL, FK → Companies(Id) ON DELETE CASCADE  |
| `UserId`      | uuid          | NOT NULL                                        |
| `Name`        | varchar(200)  | NOT NULL                                        |
| `Position`    | varchar(200)  | NOT NULL                                        |
| `Personality` | varchar(4000) | NOT NULL                                        |
| `Difficulty`  | varchar(16)   | NOT NULL, DEFAULT 'Medium'                      |
| `CreatedAt`   | timestamptz   | NOT NULL                                        |

**Indexes:** `IX_CompanyPersonas_CompanyId_CreatedAt` (CompanyId ASC, CreatedAt DESC)

`Difficulty` is one of `Easy | Medium | Hard`, stored as its string name (`HasConversion<string>()`,
same pattern as `Companies.Status`) so the column stays human-readable. A `CompanyPersona` is
either hand-written or the result of a `POST /companies/{id}/personas/generate` draft the user
chose to save (see `docs/API_CONTRACTS.md`); it is not itself an AI call — generation is stateless
and proxies to ai-service, only the save step touches this table.

---

## organization database (organization-service)

Standalone Postgres database `organization`. Owned by `organization-service` (port 5010).
Connection string key: `ConnectionStrings:Postgres`. See [ORGANIZATION_SERVICE.md](ORGANIZATION_SERVICE.md)
and [TENANCY.md](TENANCY/TENANCY.md).

### Table: `Organizations` (the tenant registry — NOT tenant-scoped, no RLS)

| Column      | Type          | Constraints                     |
|-------------|---------------|----------------------------------|
| `Id`        | uuid          | PK                                |
| `Name`      | varchar(200)  | NOT NULL                          |
| `Slug`      | varchar(120)  | NOT NULL, UNIQUE (global)         |
| `Status`    | varchar(32)   | NOT NULL, DEFAULT 'Active'        |
| `CreatedAt` | timestamptz   | NOT NULL                          |
| `UpdatedAt` | timestamptz   | NOT NULL                          |

**Indexes:** `IX_Organizations_Slug` (unique)

`Status` is `Active | Suspended`, stored as its string name (`HasConversion<string>()`, same
pattern as `Companies.Status`). This table is the tenant registry itself, so it deliberately does
**not** implement `ITenantScoped` and is never wrapped in `EnableTenantRls` — see
[TENANCY.md §1.2](TENANCY/TENANCY.md) and `docs/DECISIONS.md`.

### Table: `DemoRequests` (landing-page leads — NOT tenant-scoped, no RLS)

| Column                    | Type          | Constraints                          |
|---------------------------|---------------|--------------------------------------|
| `Id`                      | uuid          | PK, minted in code                   |
| `FullName`                | varchar(120)  | NOT NULL                             |
| `WorkEmail`               | varchar(200)  | NOT NULL, stored trimmed + lowercased|
| `Phone`                   | varchar(40)   | NOT NULL (was NULL — required 2026-08-20, `MakeDemoRequestPhoneRequired`) |
| `CompanyName`             | varchar(200)  | NOT NULL                             |
| `JobTitle`                | varchar(120)  | NULL                                 |
| `SalesTeamSize`           | varchar(32)   | NOT NULL                             |
| `Comment`                 | varchar(2000) | NULL                                 |
| `Status`                  | varchar(32)   | NOT NULL, DEFAULT 'New'              |
| `ConsentGivenAt`          | timestamptz   | NOT NULL                             |
| `MarketingConsentGivenAt` | timestamptz   | NULL — null means not given          |
| `CreatedAt`               | timestamptz   | NOT NULL                             |
| `UpdatedAt`               | timestamptz   | NOT NULL                             |
| `OrganizationId`          | uuid          | NULL — set once `/provision` creates the organization (`AddDemoRequestProvisioning`, 2026-08-20) |
| `ProvisioningState`       | varchar(32)   | NOT NULL, DEFAULT 'NotProvisioned' — `NotProvisioned \| OrganizationCreated \| AdminInvited` |
| `BootstrapInviteId`       | uuid          | NULL — the invite `identity-service` minted for this lead's first administrator |
| `BootstrapAdminEmail`     | varchar(200)  | NULL — resolved and fixed once, at organization-creation time (see field comment on the entity) |
| `ProvisionedAt`           | timestamptz   | NULL — stamped once `ProvisioningState` reaches `AdminInvited` |

**Indexes:** `IX_DemoRequests_WorkEmail` (non-unique — the per-email cooldown lookup, and one company
may legitimately ask twice over time), `IX_DemoRequests_CreatedAt` (descending — the admin list order),
`IX_DemoRequests_OrganizationId` (**unique, partial** — `WHERE "OrganizationId" IS NOT NULL` — two
leads can never claim the same organization; a plain unique index would instead reject every second
and third never-provisioned lead's shared `NULL`)

`SalesTeamSize` is `UpToFive | SixToTwenty | TwentyOneToFifty | FiftyOneToTwoHundred |
MoreThanTwoHundred` and `Status` is `New | Contacted | Approved | Declined` (`Approved`, not
`Qualified` — renamed 2026-08-20, no migration needed since the column carries no `CHECK`
constraint), both stored as their
string names (`HasConversion<string>()`, same pattern as `Organizations.Status`).
`ProvisioningState` follows the same convention for the same reason — a value stays readable in the
database after the enum is reordered in code.

Not `ITenantScoped` and never wrapped in `EnableTenantRls`, for a **different reason** than
`Organizations` above: the registry is un-scoped because it *is* the tenant list, whereas a demo
request is un-scoped because it precedes any tenant existing at all — the submitter has no user, no
organization and no membership. Reads are gated by `RequirePlatformAdministrator` instead of by RLS.

Both consents are timestamps rather than booleans on purpose: a boolean records that a box was ticked,
a timestamp records when, which is the part that matters if it is ever asked about. `WorkEmail` is
**not** unique — the cooldown is enforced in `DemoRequestService` against `CreatedAt`, deliberately
not by a constraint, because a company coming back six months later is a new lead, not a conflict.
Feature: [DEMO_REQUEST.md](DEMO_REQUEST.md).

### Table: `OrganizationProfiles` (tenant-scoped — RLS enabled)

| Column              | Type   | Constraints                              |
|---------------------|--------|--------------------------------------------|
| `OrganizationId`    | uuid   | PK, no default generation (assigned by the service from `ITenantContext`) |
| `Product`           | text   | nullable                                    |
| `Icp`                | text   | nullable                                    |
| `ObjectionsJson`     | jsonb  | NOT NULL, DEFAULT `'[]'`                    |
| `ScriptJson`         | jsonb  | NOT NULL, DEFAULT `'[]'`                    |
| `Tone`               | text   | nullable                                    |
| `GlossaryJson`       | jsonb  | NOT NULL, DEFAULT `'{}'`                    |
| `BannedClaimsJson`   | jsonb  | NOT NULL, DEFAULT `'[]'`                    |
| `CreatedAt`          | timestamptz | NOT NULL                               |
| `UpdatedAt`          | timestamptz | NOT NULL                               |

`ObjectionsJson`/`ScriptJson`/`GlossaryJson`/`BannedClaimsJson` are `jsonb`-typed `string`
columns holding raw JSON text (same convention as `Exercise.SerializedContent` in
`learning-service`) — the service layer serializes/deserializes them via `System.Text.Json`, never
raw SQL string concatenation. Shape per
[CONTENT_MODEL.md §3](TENANCY/CONTENT_MODEL.md#3-the-organization-profile--the-part-that-removes-most-forks):
`ObjectionsJson` is `[{text, frequency?, bestResponse?}]`, `ScriptJson` is an ordered array of call
stage names, `GlossaryJson` is a flat string→string map.

**Phase 40.29 added no column, no constraint and no migration to this table**, which is worth stating
because the block it belongs to («профиль компании как интервью, а не форма») looks like a schema
change and is not one. What was added lives entirely above the row: a merge policy, a closed list of
seven questions, and four routes on the existing controller. In particular the interview keeps no
state of its own — which question was asked, which was skipped, whether a draft was ever promoted. All
of that is derivable from the columns above by looking at which of them are empty, and a table
recording it would be a second source of truth about the same seven fields, drifting the first time
somebody edited the profile through the plain `PUT`. **There is therefore no
`docs/TENANCY/sql/40.29_*.sql`**: no index to build concurrently, nothing to backfill, and no
migration to sequence against a deployment.

**RLS:** `EnableTenantRls("OrganizationProfiles")` in `InitialOrganizationSchema` — `ENABLE`/`FORCE`
row-level security with a `USING`/`WITH CHECK` policy on
`OrganizationId = NULLIF(current_setting('app.organization_id', true), '')::uuid`. Since the
`RefreshTenantPoliciesForPlatformStaff` migration (2026-08-16) the `USING` half **also** admits
validated platform staff via `app.platform_mode` — see the tenancy-settings note above; `WITH CHECK`
is unchanged, so Sellevate staff can read this profile without being able to write it.
Also protected by the EF query filter and the Stage A `TenantSaveChangesInterceptor` write guard.
`OrganizationProfileController` gates access with `[TenantScoped]`, so a request with no
`X-Organization-Id` header never reaches the service layer at all.

**Replicated since Phase 40.19.** Every successful write of this row publishes
`organization.profile.updated` (after the commit, with the whole profile in the payload) — since
40.29 that is three routes, not one: `PUT /organizations/profile`, `PATCH /organizations/profile` and
`POST /organizations/profile/draft/apply`, all of which go through a single write path in
`OrganizationProfileService` for exactly this reason. learning-service and
learning-service and ai-service each project it into a local `OrganizationProfileReplicas` table with
the same columns. That is what lets `{{organization.*}}` placeholders resolve without a cross-service
call on the read path of every lesson and every persona reply — see
[CONTENT_PARAMETERIZATION.md](CONTENT_PARAMETERIZATION.md) §5 for why a replica and not a call, and
what eventual consistency costs here.

### Table: `OrganizationProfileReplicas` (learning-db and ai-db — tenant-scoped, RLS enabled)

Phase 40.19. The same shape in both databases, and **read-only in both**: the only writer is
`OrganizationProfileConsumer`; no controller, service or migration writes a row.

| Column              | Type        | Constraints                                 |
|---------------------|-------------|---------------------------------------------|
| `OrganizationId`    | uuid        | PK, no default generation (from the Kafka envelope) |
| `Product`           | text        | nullable                                    |
| `Icp`               | text        | nullable                                    |
| `Tone`              | text        | nullable                                    |
| `ObjectionsJson`    | jsonb       | NOT NULL, DEFAULT `'[]'`                    |
| `ScriptJson`        | jsonb       | NOT NULL, DEFAULT `'[]'`                    |
| `GlossaryJson`      | jsonb       | NOT NULL, DEFAULT `'{}'`                    |
| `BannedClaimsJson`  | jsonb       | NOT NULL, DEFAULT `'[]'`                    |
| `UpdatedAt`         | timestamptz | NOT NULL — the **source** row's timestamp, not the projection's |

Three things about this table are decisions, not defaults.

- **`EnableTenantRls` (plain equality), not `EnableTenantRlsForContent`.** Everything else the content
  model added to these two databases uses `OrganizationId IS NULL OR = current`, because a null owner
  means "the shared library". A null owner *here* would mean one customer's product name and one
  customer's `banned_claims` applying to everybody — the opposite of what a profile is. In ai-db this
  is the first table that is not content, so the mistake of copying the neighbouring policy is a live
  one; section 2b of `docs/TENANCY/sql/40.19_organization_profile_verify.sql` checks for it explicitly.
- **The tenant column is the whole primary key.** No surrogate id, so a second row for the same tenant
  cannot exist and a row with no owner cannot be written. It also makes the projection idempotent for
  free: a redelivered Kafka message updates the row it already wrote.
- **`UpdatedAt` carries the source timestamp.** Comparing it against `OrganizationProfiles.UpdatedAt`
  is how an operator tells "never published" (row absent) from "projection is behind" (row older) —
  two different problems with two different fixes.

The table is duplicated across two databases on purpose. Both services resolve placeholders on a hot
path and neither can afford a hop into organization-service to do it; the alternative to duplication
is a shared database, which is the thing the service split exists to prevent.

---

### Table: `ContentGenerationJobs` (learning-db — Phase 40.27, strict tenant data, RLS enabled)

One run of the admin content pipeline: material in, a structure a human confirms, then a lesson.
Full description: [CONTENT_PIPELINE.md](CONTENT_PIPELINE.md).

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid | PK |
| `OrganizationId` | uuid | **NOT NULL** — plain-equality RLS, never the content policy |
| `CreatedBy` | uuid | nullable — the РОП who started it |
| `Title` | varchar(200) | NOT NULL, non-blank; becomes the generated topic's title |
| `SourceMaterial` | text | NOT NULL, non-blank; the pasted deck/script, verbatim. Bounded at 60 000 characters by the service, not by the column |
| `Status` | varchar(20) | NOT NULL, DEFAULT `'structuring'`; one of `structuring` / `awaiting_review` / `generating` / `completed` / `failed` / `insufficient` (40.28) |
| `GapSourceRef` | varchar(200) | **40.31** — nullable; `skill-gap:<stage>@<yyyy-MM-dd>` on a run the dashboard proposed, null on one somebody started by pasting material. `CK_ContentGenerationJobs_GapSourceRef` keeps it inside that namespace. Same width as `Assignments.SourceRef`, because it is copied there verbatim |
| `Structure` | jsonb | nullable — the extracted structure, in the organization profile's shape |
| `Insufficiency` | jsonb | **40.28** — nullable; the recorded refusal: `{stage, gaps: [{code, message}], note}`. Non-null **exactly** when `Status = 'insufficient'` |
| `StructuredAt` | timestamptz | nullable |
| `StructuredMaterialLength` | integer | **40.28** — NOT NULL, ≥ 0, ≤ `length(SourceMaterial)`; how much of the material has already been read and paid for. A resumed run is sent only the tail |
| `ApprovedAt` | timestamptz | nullable — **the checkpoint, recorded** |
| `ApprovedBy` | uuid | nullable |
| `ProducedLessonId` | uuid | nullable — **the idempotency key of the expensive half** |
| `ProducedLessonVersionId` | uuid | nullable — the frozen snapshot of what was generated |
| `ProducedExerciseCount` | integer | NOT NULL, ≥ 0 — how many survived validation |
| `GeneratedAt` | timestamptz | nullable |
| `FailureReason` | varchar(1000) | nullable |
| `Attempts` | integer | NOT NULL, ≥ 0 — spent in the current half; reset on approve and on retry |
| `ClaimedAt` | timestamptz | nullable — the worker's lease, stamped and committed **before** the LLM call |
| `CreatedAt` / `UpdatedAt` | timestamptz | NOT NULL |

Indexes: `(OrganizationId, Status, CreatedAt)` (the worker's query), `(OrganizationId, CreatedAt)`
(the administrator's list), `(OrganizationId, ProducedLessonId)` («where did this lesson come from»,
which 40.31 will ask), and `(OrganizationId, GapSourceRef)` — **partial** on `GapSourceRef IS NOT
NULL`, 40.31, for «is a run already working on this gap», asked once per red cell every time the
suggestion panel is drawn. Partial because the overwhelming majority of runs were started by a
person pasting material and have nothing to say about a gap.

Constraints, and two of them are the block rather than hygiene:

- **`CK_ContentGenerationJobs_Checkpoint`** — a row may not be in `generating` without both a
  `Structure` and an `ApprovedAt`. **This is 40.27 stated in the database:** no lesson is ever
  generated from a structure no human confirmed. The service enforces the same rule and would
  otherwise be the only thing enforcing it.
- **`CK_ContentGenerationJobs_Produced`** — `ProducedLessonId` may not exist outside the `completed`
  state. That is what makes "a run holding a lesson id has already been paid for" a fact the cost
  guard can rely on rather than a convention.
- **`CK_ContentGenerationJobs_Insufficiency`** (40.28) — `Insufficiency IS NOT NULL` **if and only
  if** `Status = 'insufficient'`. A refused run with no list of what is missing is a dead end on the
  screen; a list left behind on a run that has moved on reads as a warning about the lesson it is
  about to produce. The refusal and the state are one fact, so the database keeps them one fact.
- `CK_ContentGenerationJobs_Status` (the vocabulary), `CK_ContentGenerationJobs_Structure` (a run at
  the checkpoint has something to review; an approval names a structure),
  `CK_ContentGenerationJobs_Counters` (non-negative, and since 40.28 `StructuredMaterialLength ≤
  length(SourceMaterial)` — it is a substring offset the worker slices with, and past the end of the
  string that slice throws inside a background job), `CK_ContentGenerationJobs_Input` (non-blank
  title and material — an empty one can only fail, and would fail after paying for a call).

**`ProducedLessonId` is not a foreign key and never can be.** `Lessons` is a content table under an
`IS NULL OR = current` policy and this is strict tenant data under plain equality; 40.16 already
refused to join those two with a constraint validated with the writer's privileges. What makes the
value trustworthy is that only one code path writes it, in the same transaction that creates the
lesson.

Migration: `AddContentGenerationJobs` (2026-08-18). Creates the table empty — no backfill, no
maintenance window, no concurrent-index script, for the sixth block in a row and for the same reason.

Migration: `AddContentGenerationSufficiency` (2026-08-18, Phase 40.28). Adds the two columns,
recreates `CK_..._Status` (sixth state) and `CK_..._Counters` (the new column) — Postgres has no
`ALTER CONSTRAINT` for a `CHECK` expression — and adds `CK_..._Insufficiency`. **No index and no
concurrent-index script**, and that is a decision: the only query filtering on the new state is the
administrator's list, already served by `(OrganizationId, Status, CreatedAt)`, and nothing queries
*inside* the `Insufficiency` document — it is read whole, so a GIN index on it would be maintenance
nobody reads. Both `ADD COLUMN`s are metadata-only (a nullable column, and a non-volatile default),
so the table is not rewritten. The `Down` migration moves any `insufficient` row back to
`awaiting_review` or `structuring` before narrowing the status vocabulary, because a down migration
that leaves a table unable to accept its own constraint is one nobody can run.

---

### Table: `TeamSkillGapDismissals` (learning-db — Phase 40.31, strict tenant data, RLS enabled)

One suggestion the РОП said no to. **The only table 40.31 adds, and that is the block's central
decision:** the suggestions themselves are computed on every read from the same heat map the screen
draws, so a gap that closes stops being offered without anything having to extinguish a row. A table
of proposed gaps would have needed a writer, an expiry sweep and a rule for what happens to a row
whose number has since recovered — all to hold a fact the matrix already answers. What genuinely
cannot be derived is that a person said no.

Full description: [ASSIGNMENTS.md §3.4](TENANCY/ASSIGNMENTS.md).

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid | PK |
| `OrganizationId` | uuid | **NOT NULL** — plain-equality RLS. A null owner would silence one customer's panel for every other |
| `StageKey` | varchar(64) | NOT NULL — the `Skill.Stage` key that was dismissed, without the observation date. `CK_TeamSkillGapDismissals_StageKey`: non-blank and containing neither `:` nor `@`, the two separators of `skill-gap:<stage>@<date>` |
| `DismissedBy` | uuid | nullable — the РОП, or null when the token carried no user id |
| `DismissedAt` | timestamptz | NOT NULL |
| `ExpiresAt` | timestamptz | NOT NULL — `CK_TeamSkillGapDismissals_Window`: strictly after `DismissedAt`. The service sets it to +90 days |
| `AccuracyPercentAtDismissal` | integer | NOT NULL, 0–100 (`CK_TeamSkillGapDismissals_Measurement`) — the team's accuracy on that stage at the moment of the refusal |
| `AttemptCountAtDismissal` | integer | NOT NULL, ≥ 0 — how much practice that number was built on |
| `Note` | varchar(500) | nullable — why, in the РОП's words. Never shown to the team |

Index: `IX_TeamSkillGapDismissals_OrganizationId_StageKey` — **unique**. One live refusal per stage
per organization; a second «не сейчас» on the same red cell replaces the first rather than stacking,
and two tabs racing collide here rather than leaving two rows that expire on different days. The same
use of a unique index 40.24 made for repeat waves.

> **Why a refusal expires, and why it can be broken early.** A permanent dismissal is a worse product
> than no button at all: the team that was weak at closing in August is still being measured in
> November, and a refusal that outlived its own evidence would quietly turn the panel off for the one
> stage most in need of it. The life is **90 days** — the heat map's own default window — so a refusal
> lasts exactly as long as the measurement that provoked it could still be the same measurement.
> `AccuracyPercentAtDismissal` is what lets the world overrule the calendar: «мы это знаем» was said
> about 58% and is not an answer to 41%, so the suggestion returns as soon as the number falls ten
> points below what was recorded. Neither rule needs a sweep — both are read-time comparisons.

Migration: `AddTeamSkillGaps` (2026-08-18, Phase 40.31). Creates this table empty and adds
`ContentGenerationJobs.GapSourceRef` with its partial index and CHECK. **No backfill, no maintenance
window, no concurrent-index script** — the seventh block in a row, and for the same reason: the unique
index is built over zero rows, the partial index covers a column that is NULL on every existing row,
and the `ADD COLUMN` is metadata-only in Postgres 11+ (nullable, no default), so nothing rewrites the
table. Read-only verification: `docs/TENANCY/sql/40.31_skill_gaps_verify.sql`.

---

### Table: `ContentAdaptationJobs` (learning-db — Phase 40.32, strict tenant data, RLS enabled)

One batch: «перепиши все упражнения этапа "закрытие" под наш продукт и тон», or the same sweep asking
what is methodically wrong with them instead. **Strict tenant data, plain equality** — a batch names
one customer's stage and its items hold their exercises rewritten in their own voice; a null owner
would publish both to every other organization.

Full description: [CONTENT_PIPELINE.md §6a](CONTENT_PIPELINE.md).

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid | PK |
| `OrganizationId` | uuid | **NOT NULL** — plain-equality RLS, never the content flavour |
| `CreatedBy` | uuid | nullable — the РОП who pressed the button |
| `Mode` | varchar(32) | NOT NULL, default `tone_rewrite`. `CK_ContentAdaptationJobs_Mode`: `tone_rewrite` \| `quality_review` |
| `StageKey` | varchar(64) | NOT NULL, non-blank (`CK_ContentAdaptationJobs_StageKey`) — a `Skill.Stage` value. Same width as `TeamSkillGapDismissals.StageKey`, deliberately: a mismatch would let one table accept a key the other truncates |
| `Status` | varchar(20) | NOT NULL, default `preparing`. `CK_ContentAdaptationJobs_Status`: `preparing` \| `awaiting_review` \| `completed` \| `failed`. **A projection of the items, recomputed inside every writing transaction — never a counter** |
| `ItemCount` | integer | NOT NULL, ≥ 0 — frozen at creation. The denominator of «сделано N из M», and a denominator that moves while somebody reviews is worse than one slightly out of date |
| `ClaimedAt` | timestamptz | nullable — the worker's lease, stamped and committed **before** any LLM call |
| `FailureReason` | varchar(1000) | nullable — why the last tick failed. Per-item failures live on the item |
| `CreatedAt` / `UpdatedAt` | timestamptz | NOT NULL |
| `CompletedAt` | timestamptz | nullable. `CK_ContentAdaptationJobs_Completed`: non-null **exactly** when the status is `completed` or `failed` |

Indexes: `(OrganizationId, Status, CreatedAt)` — the worker's query; `(OrganizationId, CreatedAt)` —
the admin list; and `UX_ContentAdaptationJobs_Live` on `(OrganizationId, Mode, StageKey)`,
**unique, partial** over `Status IN ('preparing','awaiting_review')`. Plus
`UQ_ContentAdaptationJobs_Id_OrganizationId`, which exists only so the item table can point at it.

> **The partial unique index is a cost control, not tidiness.** Two clicks a second apart both read
> "no live batch" under READ COMMITTED and both start one, and the customer pays twice for the same
> sixty LLM calls. Partial over the two live statuses so a finished batch never blocks the next one —
> the whole point of the queue is that it eventually empties.

### Table: `ContentAdaptationItems` (learning-db — Phase 40.32, strict tenant data, RLS enabled)

One exercise inside a batch: what the model proposes for it, and what a person decided. **This is the
row the roadmap calls «дифф», and it is the unit of accept and reject.**

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid | PK |
| `OrganizationId` | uuid | **NOT NULL** — plain-equality RLS. Denormalized from the batch rather than inherited: EF query filters are not inherited through a navigation and an RLS policy is per table |
| `JobId` | uuid | NOT NULL, FK → `ContentAdaptationJobs.Id` (`ON DELETE CASCADE`). A **second, composite** FK on `(JobId, OrganizationId)` says an item belongs to the organization its batch belongs to — RLS checks each row against the session tenant and never against its parent |
| `ExerciseId` | uuid | NOT NULL — the exercise the proposal is about, **as resolved at collection time** (40.18): the organization's own copy when they have forked the lesson, the global library row when they have not. No FK: a global row and an owned row are the same table, and the item outlives a deleted exercise as a failure with a reason |
| `LessonId` | uuid | NOT NULL — that exercise's lesson |
| `LessonTitle` | varchar(300) | NOT NULL — human label, so a queue of sixty rows reads as content and not as identifiers |
| `ExerciseType` | varchar(50) | NOT NULL — frozen here; a proposal that changes the type is rejected outright |
| `OrderInLesson` | integer | NOT NULL — the learner's order, which is the order the queue is walked in |
| `BaseContentHash` | varchar(64) | NOT NULL, exactly 64 chars (`CK_ContentAdaptationItems_Counters`) — SHA-256 of the canonical body the model was shown (`ContentSnapshotSerializer.BuildCanonicalExerciseBody`, type included). Recomputed on accept; a mismatch is a 409 |
| `Status` | varchar(20) | NOT NULL, default `pending`. `CK_ContentAdaptationItems_Status`: `pending` \| `proposed` \| `unchanged` \| `accepted` \| `rejected` \| `failed` |
| `ProposedContent` | jsonb | nullable — the rewritten body, validated by `ExerciseContentValidator` before it is ever stored. Null in review mode |
| `ChangeSummary` | varchar(500) | nullable — the model's own sentence about what it changed. **The prose half of the diff**; the leaf-level half is computed on the server and can never supply the reason |
| `ChangedFieldCount` | integer | NOT NULL, ≥ 0 — how many JSON leaves the proposal moves. Stored, not cached: it describes the proposal (frozen), not the exercise (not) |
| `Findings` | jsonb | nullable — review mode, a list of `{code, detail}` from the closed vocabulary of seven. Only the code and the quoted fragment are stored; the sentence and the severity are resolved on read, so a stored document can never disagree with the code table |
| `AppliedExerciseId` | uuid | nullable — **the row actually written**, which is not always `ExerciseId`: accepting a rewrite of a global exercise forks the lesson first and the body lands in the organization's copy |
| `AppliedAt` | timestamptz | nullable |
| `ResolvedBy` / `ResolvedAt` | uuid / timestamptz | nullable — who answered this item, and when. No worker ever fills them in |
| `Attempts` | integer | NOT NULL, ≥ 0 — budgeted **per item**, so one bad exercise cannot exhaust the batch |
| `FailureReason` | varchar(1000) | nullable |
| `CreatedAt` / `UpdatedAt` | timestamptz | NOT NULL |

Indexes: `(OrganizationId, JobId, Status)` — the worker's query; `(OrganizationId, JobId, LessonId,
OrderInLesson)` — the queue in the learner's order; `(OrganizationId, ExerciseId, Status)` — «есть ли
на это упражнение живое предложение»; plus EF's plain `JobId` index behind the cascade FK.

> **`CK_ContentAdaptationItems_Proposal` is this block's checkpoint constraint**, the way
> `CK_ContentGenerationJobs_Checkpoint` was 40.27's. It says three things at once: an accepted item
> carries the proposal it applied *and* the row it was applied to; nothing outside the accepted state
> may claim an application; and an item cannot sit in the review queue with nothing for a person to
> look at. Together they make "auto-applied without a proposal" and "applied without being accepted"
> unrepresentable rather than merely unwritten. `CK_ContentAdaptationItems_Resolution` pairs
> `accepted`/`rejected` with `ResolvedAt` the same way.
>
> What SQL **cannot** say is «a human pressed it». That half is enforced by shape:
> `ContentAdaptationStepRunner` has no branch that writes `accepted` or `rejected`, and the only code
> in the system that writes an `Exercise` body out of a proposal is an admin request handler.

Migration: `AddContentAdaptationBatches` (2026-08-18, Phase 40.32). Creates both tables empty.
**No backfill, no maintenance window, no concurrent-index script** — the eighth block in a row, and
for the plainest reason yet: nothing is added to an existing table at all, so every index including
the partial unique one is built over zero rows. Read-only verification:
`docs/TENANCY/sql/40.32_content_adaptation_verify.sql` (never executed).

---

### Tables: `OrganizationQuotas` and `AiUsageRecords` (ai-db — Phase 40.33, strict tenant data, RLS enabled)

The meter. One table says what an organization may spend, the other says what it did. Full
description: [AI_QUOTAS.md](AI_QUOTAS.md).

#### `OrganizationQuotas`

| Column | Type | Notes |
|---|---|---|
| `OrganizationId` | uuid | **PK**, no default generation — the tenant column *is* the key |
| `VoiceDailyLimitMinutes` | int | nullable — organization-wide voice minutes per UTC day |
| `VoiceMonthlyLimitMinutes` | int | nullable — same, per UTC month |
| `LlmMonthlyTokenLimit` | bigint | nullable — prompt + completion tokens per UTC month, all models |
| `BatchReservePercent` | int | nullable — share of the LLM allowance background pipelines may not touch |
| `Note` | varchar(1000) | nullable — free text for the operator: which contract this number came from |
| `UpdatedAt` | timestamptz | NOT NULL |

**Every limit is nullable and null means "the platform default"** (`AiQuotas:Default…`), not
"unlimited". `0` means "this window is disabled" and is a different value on purpose. That
distinction is the whole answer to the fail-open / fail-closed question: an organization with no row
is metered against the defaults, which is exactly what ai-service did before this block, so no
missing row and no failed replication can hand anybody an unmetered night of voice.

**The table is in ai-db rather than organization-db**, unlike every other per-organization setting.
The alternative — columns on `OrganizationProfile`, replicated here over
`organization.profile.updated` as 40.19's replica is — was rejected on one concrete failure: the
moment an operator most needs to change a limit is the moment a customer is standing at it, and a
lagging or dead-lettering consumer would leave the raise invisible to the enforcer with nothing in
the raise's own response saying so.

#### `AiUsageRecords`

| Column | Type | Notes |
|---|---|---|
| `OrganizationId` | uuid | **PK part 1** |
| `PeriodKey` | varchar(7) | **PK part 2** — the UTC month, `yyyy-MM`. A string because it is a bucket label, not a date |
| `Model` | varchar(128) | **PK part 3** — the provider model for `llm` rows, the provider name for `tts`/`stt` |
| `Kind` | varchar(16) | NOT NULL — `llm` / `tts` / `stt`. Derived from `Model`, stored so the report need not guess |
| `PromptTokens` | bigint | NOT NULL |
| `CompletionTokens` | bigint | NOT NULL |
| `CallCount` | bigint | NOT NULL |
| `EstimatedCallCount` | bigint | NOT NULL — of `CallCount`, how many were counted from an estimate because the provider reported no `usage` |
| `SpeechCharacters` | bigint | NOT NULL |
| `UpdatedAt` | timestamptz | NOT NULL |

Four things about this table are decisions, not defaults.

- **Postgres, not Redis, unlike the voice gate right beside it.** The two have different jobs. Voice
  reserves seconds before a stream and refunds the tail milliseconds later, many times a minute, and
  already has a durable record of its own in Mongo `dialog_sessions`. LLM spend is written once per
  completion — next to a call that takes seconds — and its whole point is to be readable next month,
  before the invoice arrives. A counter a Redis eviction can silently zero is not that.
- **The write is one `INSERT … ON CONFLICT DO UPDATE SET x = x + excluded.x`.** No read-modify-write
  anywhere, so two concurrent completions on one organization cannot lose each other's tokens — the
  same property 40.27 required of the pipeline claim, and for the same reason: the alternative costs
  money rather than a retry.
- **One row per model, not one per organization.** Models differ in price by more than an order of
  magnitude, so a single blended token total cannot be turned into money without lying. The
  breakdown makes the cost estimate a sum of per-model products.
- **Money is not a column.** Tokens and characters are what the provider bills and what we can count
  exactly; a stored price is a guess frozen at write time. Cost is derived on read from
  `AiQuotas:PricePerMillionTokens`, which is also why editing that table moves no limit — the limit
  is counted in tokens. A model absent from the price table is reported as **unpriced**, never as
  free: zero would read as "this model costs nothing", which is the one reading of a spend report
  that must never be accidental.

Migration: `AddOrganizationQuotas` (2026-08-18, Phase 40.33). Creates both tables empty and applies
`EnableTenantRls` to each. **No backfill, no maintenance window, no concurrent-index script** — the
ninth block in a row.
