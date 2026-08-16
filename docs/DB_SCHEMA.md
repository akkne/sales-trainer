# DB Schema

Last updated: 2026-06-12

## Databases overview

| Store      | Purpose                                      |
|------------|----------------------------------------------|
| PostgreSQL | Primary — all structured data                |
| MongoDB    | Chat messages and unstructured dialogue data |
| Redis      | Cache, sessions, team-progress rankings      |

> **Microservices migration:** as each service is extracted it owns its own logical
> Postgres database on the shared cluster, with its own EF migrations and
> `DatabaseBootstrapper`. So far: `identity` (Phase 2), `ai` (Phase 6) and
> **`gamification` (Phase 7)**. The `gamification` database owns `UserXpRecords`,
> `UserStreaks`, `GamificationSettings`, `ExerciseTypeRewards`, `StreakMilestones`,
> `Achievements`, `UserAchievements`, `Leagues`, `LeagueTiers`, `LeagueMemberships`,
> `LeagueSettings` (schemas below, ported verbatim), plus a local `UserReplica`, a
> `UserLearningProgress` projection (completed-lesson count + has-completed-any-skill,
> fed by `lesson.completed`/`skill.completed`), and its own Hangfire schema. The
> monolith's copies of these tables remain as reference until Phase 9. See
> [GAMIFICATION_SERVICE.md](GAMIFICATION_SERVICE.md).
>
> **`learning` (Phase 8)** — the last extraction. The `learning` database owns the
> content tree and progress: `Skills`, `SkillStages`, `Topics`,
> `UserSkillProgressRecords`, `Lessons`, `Exercises`, `UserLessonProgressRecords`,
> `UserExerciseAttempts`, `ExerciseTypePrompts`, `ReferenceMaterials`, `DailyQuotes`,
> `Techniques`, `TechniqueSkills`, `TechniqueCoaches`, `UserTechniqueProgress` (schemas
> ported verbatim from the monolith `AppDbContext`), plus a local `UserReplicas`
> read-model fed by `user.*` events. Created by `DatabaseBootstrapper` + EF migration
> `InitialLearningSchema`. The monolith's copies remain as reference until Phase 9. See
> [LEARNING_SERVICE.md](LEARNING_SERVICE.md).
>
> **`organization` (Phase 40.5)** — the tenant registry, new (not extracted from the
> monolith). Owns `Organizations` (the registry — deliberately not tenant-scoped, no RLS)
> and `OrganizationProfiles` (tenant-scoped, RLS enabled via `EnableTenantRls`). See
> [ORGANIZATION_SERVICE.md](ORGANIZATION_SERVICE.md) and [TENANCY.md](TENANCY/TENANCY.md).

---

## PostgreSQL

All tables managed by EF Core migrations (`Infrastructure/Data/Migrations/`).

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
| `TopicId`     | `uuid`    | NOT NULL | FK → `Topics.Id`     |
| `OrderInTopic`| `integer` | NOT NULL |                      |
| `Title`       | `text`    | NOT NULL |                      |

Indexes: `IX_Lessons_TopicId_OrderInTopic`

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

### `Notifications`

| Column              | Type                       | Nullable | Notes                                                             |
|---------------------|----------------------------|----------|-------------------------------------------------------------------|
| `Id`                | `uuid`                     | NOT NULL | PK                                                                |
| `RecipientUserId`   | `uuid`                     | NOT NULL | FK → `Users.Id`                                                   |
| `NotificationType`  | `integer`                  | NOT NULL | 1=FriendRequestReceived, 2=FriendRequestAccepted, 3=ChatMessageReceived, 4=AchievementUnlocked, 5=StreakMilestone |
| `Title`             | `varchar(200)`             | NOT NULL |                                                                   |
| `Body`              | `varchar(1000)`            | NOT NULL |                                                                   |
| `ActionUrl`         | `varchar(500)`             | NULL     | Relative frontend route for deep link                             |
| `RelatedEntityId`   | `varchar(64)`              | NULL     | Source entity id (friendship id, conversation id, achievement key)|
| `IsRead`            | `boolean`                  | NOT NULL | Default false                                                     |
| `CreatedAt`         | `timestamp with time zone` | NOT NULL |                                                                   |
| `ReadAt`            | `timestamp with time zone` | NULL     | Set when notification is marked as read                           |

**Indexes:**
- `(RecipientUserId, IsRead)` — unread lookup per user
- `(RecipientUserId, CreatedAt)` — reverse-chronological listing per user

**Cleanup:** Hangfire recurring job `notification-cleanup` deletes rows where `IsRead = true AND CreatedAt < now() - 30 days` (runs daily at 00:30 UTC).

---

### `ReferenceMaterials`

Legacy markdown glossary, kept to serve old skill-detail pages. Superseded for the "Коллекция" redesign by the `Techniques` cluster below — see [HANDBOOK_REDESIGN.md](HANDBOOK_REDESIGN.md).

| Column            | Type      | Nullable | Notes              |
|-------------------|-----------|----------|--------------------|
| `Id`              | `uuid`    | NOT NULL | PK                 |
| `OrganizationId` | `uuid` | NULL | Phase 40.10 — `NULL` = global library shared by every organization; non-null = one organization's own copy (40.18). RLS policy `ReferenceMaterials_tenant_isolation` (content variant: `IS NULL OR = current`). |
| `SkillId`         | `uuid`    | NOT NULL | FK → `Skills.Id`   |
| `Title`           | `text`    | NOT NULL |                    |
| `MarkdownContent` | `text`    | NOT NULL |                    |
| `SortOrder`       | `integer` | NOT NULL |                    |
| `Category`        | `text`    | NULL     |                    |
| `Tags`            | `text`    | NULL     | Comma-separated    |

---

### `Techniques`

Techniques replace `ReferenceMaterials` as the handbook's primary entity. Dialog samples and case studies now live in single `jsonb` columns on this table (not separate sub-tables) — the admin writes the JSON directly.

| Column           | Type                       | Nullable | Notes                                                                            |
|------------------|----------------------------|----------|----------------------------------------------------------------------------------|
| `Id`             | `uuid`                     | NOT NULL | PK                                                                               |
| `OrganizationId` | `uuid` | NULL | Phase 40.10 — `NULL` = global library shared by every organization; non-null = one organization's own copy (40.18). RLS policy `Techniques_tenant_isolation` (content variant: `IS NULL OR = current`). |
| `Slug`           | `text`                     | NOT NULL | UNIQUE                                                                           |
| `Name`           | `text`                     | NOT NULL |                                                                                  |
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

Indexes: `IX_Techniques_Slug` (unique), `IX_Techniques_PrimarySkillId`.

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
| `UserId`         | `uuid`                     | NOT NULL | FK → `Users.Id` ON DELETE CASCADE                  |
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
| `UserId`              | `uuid`    | NOT NULL | FK → `Users.Id`                                     |
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
| `UserId`      | `uuid`                     | NOT NULL | FK → `Users.Id`                            |
| `LessonId`    | `uuid`                     | NOT NULL | FK → `Lessons.Id`                          |
| `Status`      | `text`                     | NOT NULL | `not_started` / `in_progress` / `completed`|
| `BestScore`   | `integer`                  | NOT NULL |                                            |
| `CompletedAt` | `timestamp with time zone` | NULL     |                                            |

---

### `UserExerciseAttempts`

| Column                | Type                       | Nullable | Notes                              |
|-----------------------|----------------------------|----------|------------------------------------|
| `Id`                  | `uuid`                     | NOT NULL | PK                                 |
| `OrganizationId` | `uuid` | NOT NULL | Phase 40.10 — owning tenant. RLS policy `UserExerciseAttempts_tenant_isolation`. |
| `UserId`              | `uuid`                     | NOT NULL | FK → `Users.Id`                    |
| `ExerciseId`          | `uuid`                     | NOT NULL | FK → `Exercises.Id`                |
| `SerializedAnswer`    | `jsonb`                    | NOT NULL | User's answer payload              |
| `IsCorrect`           | `boolean`                  | NOT NULL |                                    |
| `Score`               | `integer`                  | NOT NULL |                                    |
| `SerializedAiFeedback`| `jsonb`                    | NULL     | Present for AI-evaluated types     |
| `AttemptedAt`         | `timestamp with time zone` | NOT NULL |                                    |

---

### `UserStreaks`

> **Microservices (Phase 7):** owned by the **gamification-service** Postgres database
> (`gamification`). See [GAMIFICATION_SERVICE.md](GAMIFICATION_SERVICE.md).

| Column                  | Type      | Nullable | Notes                  |
|-------------------------|-----------|----------|------------------------|
| `Id`                    | `uuid`    | NOT NULL | PK                     |
| `OrganizationId`        | `uuid`    | NOT NULL | Phase 40.13 — owning tenant. `ITenantScoped`, strict RLS. |
| `UserId`                | `uuid`    | NOT NULL | FK → `Users.Id`        |
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
| `UserId`   | `uuid`                     | NOT NULL | FK → `Users.Id`                                  |
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
| `UserId`          | `uuid`    | NOT NULL | FK → `Users.Id`                              |
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
| `UserId`        | `uuid`                     | NOT NULL | FK → `Users.Id` CASCADE   |
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

| Migration name                        | Date       | Summary                                      |
|---------------------------------------|------------|----------------------------------------------|
| `InitialSchema`                       | 2026-03-31 | All base tables                              |
| `AddRefreshTokenUserFk`               | 2026-04-01 | FK + index on `RefreshTokens.UserId`         |
| `AddUserRole`                         | 2026-04-01 | `Role` integer column on `Users` (default 0) |
| `AddAchievements`                     | 2026-04-05 | `Achievements` and `UserAchievements` tables |
| `AddPersonaToUserProfile`             | 2026-04-05 | `Persona` nullable text column on `UserProfiles` |
| `AddCategoryTagsToReference`          | 2026-04-05 | `Category` and `Tags` columns on `ReferenceMaterials` |
| `AddDialogTables`                     | 2026-04-06 | `DialogBundles` and `DialogModes` tables     |
| `AddVoiceFieldsToDialogMode`          | 2026-04-06 | Voice fields on `DialogModes`                |
| `AddOpenQuestionGlobalContext`        | 2026-04-06 | `OpenQuestionGlobalContexts` table           |
| `ResetSkillsAndAddNewOnes`            | 2026-04-13 | Reset skills data                            |
| `AddExerciseTypePrompts`              | 2026-04-13 | `ExerciseTypePrompts` table                  |
| `AddIconicNameToSkillsAndTopics`      | 2026-04-14 | Add IconicName (unique) to Skills and Topics |
| `AddFriendships`                      | 2026-04-18 | `Friendships` table with unique composite index |
| `AddNotifications`                    | 2026-04-18 | `Notifications` table with recipient+read and recipient+createdAt indexes |
| `AlignExerciseTypePromptKeys`         | 2026-04-21 | Aligns `ExerciseTypePrompts` keys with `ExerciseTypes` constants |
| `AddTechniques`                       | 2026-04-21 | 7 Technique-cluster tables + backfill from `ReferenceMaterials` + 4 seed techniques |
| `AddUserAvatars`                      | 2026-06-12 | 3 avatar columns on `Users` + new `DefaultAvatars` table with unique index on `Index`; backfills `DefaultAvatarIndex` for existing users via `abs(hashtext(Id::text)) % 6` |
| `AddDiscussPhotos`                    | 2026-06-12 | `DiscussPhotos` table (polymorphic owner) for Discuss thread/reply photo attachments |
| `InitialSocialSchema` (social-service) | 2026-06-21 | Standalone `social` database: `Friendships`, all `Discuss*` tables, and `UserReplicas` (read-model). Owned by social-service, not the monolith `AppDbContext`. |
| `InitialLearningSchema` (learning-service) | 2026-06-21 | Standalone `learning` database: `Skills`, `SkillStages`, `Topics`, `UserSkillProgressRecords`, `Lessons`, `Exercises`, `UserLessonProgressRecords`, `UserExerciseAttempts`, `ExerciseTypePrompts`, `ReferenceMaterials`, `DailyQuotes`, `Techniques`, `TechniqueSkills`, `TechniqueCoaches`, `UserTechniqueProgress`, and `UserReplicas` (read-model). Owned by learning-service, not the monolith `AppDbContext`. |
| `AddLeagueTiersAndSchedule`           | 2026-06-16 | `LeagueTiers` table (seeded bronze/silver/gold/diamond) + period schedule columns on `LeagueSettings` |
| `AddGamificationSettings`             | 2026-06-16 | `GamificationSettings` (singleton), `ExerciseTypeRewards`, `StreakMilestones` tables — DB-driven progress-points economy, all seeded with historic defaults |
| `AddSkillStages`                      | 2026-06-16 | `SkillStages` table (seeded preparation/discovery/engagement/closing/retention) — DB-driven, admin-editable funnel stages for the skill tree |
| `InitialCompanySchema` (company-service) | 2026-07-09 | Standalone `company` database: `Companies`, `CallLogEntries`, `PracticeCalls` tables. Owned by company-service (port 5009). |
| `AddCompanyContacts` (company-service)   | 2026-07-09 | `CompanyContacts` table (mini-CRM, Phase 39.9); `CallLogEntries.ContactId` nullable FK → `CompanyContacts(Id)` ON DELETE SET NULL. |
| `AddCompanyStatus` (company-service)     | 2026-07-10 | `Companies.Status` varchar(32) NOT NULL DEFAULT 'Lead' (status pipeline, Phase 39.10); plain `AddColumn` with a Postgres column default, so existing rows read as `Lead` without a separate `UPDATE`. |
| `AddCompanyFollowUp` (company-service)   | 2026-07-10 | `Companies.NextActionAt` (timestamptz, nullable), `NextActionNote` (varchar(2000), nullable), `FollowUpNotifiedAt` (timestamptz, nullable) (follow-up reminders, Phase 39.11); sparse index `IX_Companies_NextActionAt` (filtered `WHERE "NextActionAt" IS NOT NULL`) keeps the reminder poll cheap. |
| `AddCompanyBriefing` (company-service)   | 2026-07-10 | `Companies.BriefingContent` (text, nullable), `BriefingGeneratedAt` (timestamptz, nullable) (AI pre-call briefing cache, Phase 39.12); plain `AddColumn`, no index (read only via the single-row `GET/POST /companies/{id}/briefing`). |
| `AddCompanyPersonas` (company-service)   | 2026-07-10 | `CompanyPersonas` table (AI persona generation, Phase 39.14); FK → `Companies(Id)` ON DELETE CASCADE. |
| `AddCompanyReadiness` (company-service)  | 2026-07-10 | `Companies.ReadinessJson` (text, nullable), `ReadinessGeneratedAt` (timestamptz, nullable) (AI readiness-score cache, Phase 39.16); plain `AddColumn`, no index (read/written only via the single-row `GET /companies/{id}/readiness`). |
| `AddCompanyReadinessNoFeedbackCache` (company-service) | 2026-07-11 | `Companies.ReadinessNoFeedbackUntil` (timestamptz, nullable) — negative-cache expiry for the "ai-service returned 204 / no usable feedback yet" readiness result (PR #26 review fast-follow, 39.17); plain `AddColumn`, no index (read/written only via the single-row `GET /companies/{id}/readiness`). |
| `AddOrganizationId` (learning-service) | 2026-08-15 | Phase 40.10, first Stage-C service. `OrganizationId uuid NOT NULL` on `UserSkillProgressRecords`, `UserLessonProgressRecords`, `UserExerciseAttempts`, `UserTechniqueProgress` (added with an all-zeros placeholder default, which is then dropped) and `OrganizationId uuid NULL` on `Skills`, `Topics`, `Lessons`, `Exercises`, `Techniques`, `ReferenceMaterials` (`NULL` = global library). RLS on all ten: `EnableTenantRls` for the progress tables, `EnableTenantRlsForContent` for the content ones. Contains **no `CREATE INDEX` and no backfill** on purpose — both are operational steps (`docs/TENANCY/sql/40.10_learning_organization_backfill.sql`, `..._indexes_concurrently.sql`), because the migration runs from `Database.Migrate()` at startup where a long index build would stall readiness. |
| `InitialOrganizationSchema` (organization-service) | 2026-08-14 | Standalone `organization` database: `Organizations` (tenant registry, unique `Slug`) and `OrganizationProfiles` (1:1 tenant-data row, RLS via `EnableTenantRls`). Owned by organization-service (port 5010), Phase 40.5. |
| `AddOrganizationId` (gamification-service) | 2026-08-15 | Phase 40.13. `OrganizationId uuid NOT NULL` (placeholder default) on `UserXpRecords`, `UserStreaks`, `UserAchievements`, `UserLearningProgress`, `Leagues`, `LeagueMemberships`, `LeagueSettings` — strict `EnableTenantRls` on all seven (no global content flavour in this database). `Achievements`, `LeagueTiers`, `GamificationSettings`, `StreakMilestones`, `ExerciseTypeRewards`, `UserReplicas`, `OutboxMessages` get nothing. Unlike 40.10–40.12, **this migration does swap four unique constraints in place** (`Leagues.(WeekStartDate,Tier)`, `UserStreaks.(UserId)`, `UserAchievements.(UserId,AchievementId)`, `UserLearningProgress`'s primary key) because each was load-bearing for correctness in the deploy-to-script window and every affected table holds at most a row per user/week. Read indexes on `UserXpRecords`/`LeagueMemberships` stay out, rebuilt by `docs/TENANCY/sql/40.13_gamification_organization_indexes_concurrently.sql`. |
| `AddOrganizationId` (social-service) | 2026-08-16 | Phase 40.13. `OrganizationId uuid NOT NULL` (placeholder default) on `Friendships`, `DiscussThreads`, `DiscussReplies`, `DiscussVotes`, `DiscussThreadTags`, `DiscussPhotos` — strict `EnableTenantRls` on all six. `OrganizationId uuid NULL` on `DiscussTags` (`NULL` = curated vocabulary shared by every organization), `EnableTenantRlsForContent`. `UserReplicas` gets nothing. Two unique swaps happen in-migration, not the concurrent-rebuild script: `DiscussTags.Slug` → `(OrganizationId, Slug)` + a partial unique index over the global rows, and `Friendships.(RequesterId,AddresseeId)` (+ the canonical-pair index) → organization-first. Every read index stays out, rebuilt by `docs/TENANCY/sql/40.13_social_organization_indexes_concurrently.sql`. Mongo `chat_conversations` gets `organizationId` via a separate script (`docs/TENANCY/mongo/40.13_chat_conversations_organization_backfill.js`), not this migration. |

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

**RLS:** `EnableTenantRls("OrganizationProfiles")` in `InitialOrganizationSchema` — `ENABLE`/`FORCE`
row-level security with a `USING`/`WITH CHECK` policy on `OrganizationId = current_setting('app.organization_id', ...)`.
Also protected by the EF query filter and the Stage A `TenantSaveChangesInterceptor` write guard.
`OrganizationProfileController` gates access with `[TenantScoped]`, so a request with no
`X-Organization-Id` header never reaches the service layer at all.
