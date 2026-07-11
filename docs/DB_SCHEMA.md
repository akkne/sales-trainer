# DB Schema

Last updated: 2026-06-12

## Databases overview

| Store      | Purpose                                      |
|------------|----------------------------------------------|
| PostgreSQL | Primary — all structured data                |
| MongoDB    | Chat messages and unstructured dialogue data |
| Redis      | Cache, sessions, leaderboard rankings        |

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
| `Role`                | `integer`                   | NOT NULL | 0=User, 1=Admin, 2=SuperAdmin      |
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
| `IconicName`  | `text`    | NOT NULL | UNIQUE — English identifier    |
| `OrderInTree` | `integer` | NOT NULL | Display order in tree          |
| `Title`       | `text`    | NOT NULL | Localized display name         |
| `Description` | `text`    | NULL     |                                |
| `Stage`       | `text`    | NOT NULL | Funnel stage bucket (DEFAULT `general`). References `SkillStages.Key`; built-in keys: `preparation`, `discovery`, `engagement`, `closing`, `retention`. Free string (no FK) — `general` and unknown keys fall back to a generic bucket. |

Indexes: `IX_Skills_IconicName` (unique), `IX_Skills_Stage`.

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
| `SkillId`     | `uuid`    | NOT NULL | FK → `Skills.Id`               |
| `IconicName`  | `text`    | NOT NULL | UNIQUE — English identifier    |
| `OrderInSkill`| `integer` | NOT NULL |                                |
| `Title`       | `text`    | NOT NULL | Localized display name         |

Indexes: `IX_Topics_IconicName`, `IX_Topics_SkillId_OrderInSkill`

---

### `Lessons`

| Column        | Type      | Nullable | Notes                |
|---------------|-----------|----------|----------------------|
| `Id`          | `uuid`    | NOT NULL | PK                   |
| `TopicId`     | `uuid`    | NOT NULL | FK → `Topics.Id`     |
| `OrderInTopic`| `integer` | NOT NULL |                      |
| `Title`       | `text`    | NOT NULL |                      |

Indexes: `IX_Lessons_TopicId_OrderInTopic`

---

### `Exercises`

| Column              | Type                       | Nullable | Notes                                                                         |
|---------------------|----------------------------|----------|-------------------------------------------------------------------------------|
| `Id`                | `uuid`                     | NOT NULL | PK                                                                            |
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
| `RequesterId` | `uuid`                     | NOT NULL | FK → `Users.Id` — who sent (loose `Guid` in `social`) |
| `AddresseeId` | `uuid`                     | NOT NULL | FK → `Users.Id` — who received (loose `Guid` in `social`) |
| `Status`      | `integer`                  | NOT NULL | 0=Pending, 1=Accepted, 2=Declined |
| `CreatedAt`   | `timestamp with time zone` | NOT NULL |                                    |
| `AcceptedAt`  | `timestamp with time zone` | NULL     |                                    |

**Indexes:**
- UNIQUE `(RequesterId, AddresseeId)` — no duplicate requests
- Individual on `RequesterId` and `AddresseeId`

**Constraints:**
- CHECK `RequesterId != AddresseeId` — cannot friend yourself

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
| `UserId`              | `uuid`                     | NOT NULL | FK → `Users.Id`                    |
| `ExerciseId`          | `uuid`                     | NOT NULL | FK → `Exercises.Id`                |
| `SerializedAnswer`    | `jsonb`                    | NOT NULL | User's answer payload              |
| `IsCorrect`           | `boolean`                  | NOT NULL |                                    |
| `Score`               | `integer`                  | NOT NULL |                                    |
| `SerializedAiFeedback`| `jsonb`                    | NULL     | Present for AI-evaluated types     |
| `AttemptedAt`         | `timestamp with time zone` | NOT NULL |                                    |

---

### `UserStreaks`

| Column                  | Type      | Nullable | Notes                  |
|-------------------------|-----------|----------|------------------------|
| `Id`                    | `uuid`    | NOT NULL | PK                     |
| `UserId`                | `uuid`    | NOT NULL | FK → `Users.Id`        |
| `CurrentStreakDayCount` | `integer` | NOT NULL |                        |
| `LongestStreakDayCount` | `integer` | NOT NULL |                        |
| `LastActivityDate`      | `date`    | NULL     |                        |

---

### `UserXpRecords`

| Column     | Type                       | Nullable | Notes                                            |
|------------|----------------------------|----------|--------------------------------------------------|
| `Id`       | `uuid`                     | NOT NULL | PK                                               |
| `UserId`   | `uuid`                     | NOT NULL | FK → `Users.Id`                                  |
| `Amount`   | `integer`                  | NOT NULL |                                                  |
| `Source`   | `text`                     | NOT NULL | `exercise` / `streak_bonus` / `league_bonus` / `admin_correction` |
| `EarnedAt` | `timestamp with time zone` | NOT NULL |                                                  |

---

### `Leagues`

| Column          | Type      | Nullable | Notes                                           |
|-----------------|-----------|----------|-------------------------------------------------|
| `Id`            | `uuid`    | NOT NULL | PK                                              |
| `Tier`          | `text`    | NOT NULL | tier key → `LeagueTiers.Key` (e.g. `bronze`)    |
| `WeekStartDate` | `date`    | NOT NULL | start of the period (named "week" for history)  |
| `WeekEndDate`   | `date`    | NOT NULL | end of the period (period length is configurable) |

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
| `UserId`          | `uuid`    | NOT NULL | FK → `Users.Id`                              |
| `LeagueId`        | `uuid`    | NOT NULL | FK → `Leagues.Id`                            |
| `WeeklyXpAmount`  | `integer` | NOT NULL |                                              |
| `Rank`            | `integer` | NOT NULL |                                              |
| `PromotionOutcome`| `text`    | NULL     | `promoted` / `demoted` / `stayed` / NULL (active) |

---

### `LeagueSettings`

Single-row table (same pattern as `OpenQuestionGlobalContexts`). Seeded by migration `20260607000000_AddLeagueSettings`; period columns added by `20260616120000_AddLeagueTiersAndSchedule`. Read by `LeagueService` at runtime; edited via `/admin/leagues/settings`. The period columns are initialized on first access (to the current Monday-based week) if null.

| Column                          | Type                       | Nullable | Notes                                            |
|---------------------------------|----------------------------|----------|--------------------------------------------------|
| `Id`                            | `uuid`                     | NOT NULL | PK                                               |
| `MaximumLeagueParticipantCount` | `integer`                  | NOT NULL | default 30                                       |
| `PromotionZoneSize`             | `integer`                  | NOT NULL | default 10                                       |
| `DemotionZoneSize`              | `integer`                  | NOT NULL | default 5                                        |
| `CurrentPeriodStartDate`        | `date`                     | NULL     | start of the running period                      |
| `CurrentPeriodEndsAt`           | `timestamptz`              | NULL     | exact close moment; drives countdown + rollover  |
| `PeriodLengthDays`              | `integer`                  | NOT NULL | default 7; applied to each new period on rollover |

---

### `GamificationSettings`

Single-row table holding the admin-editable XP economy (daily/weekly goals + dialog scoring) that was previously hardcoded. Created and seeded by migration `20260616130000_AddGamificationSettings`. Loaded-or-created on first access by `GamificationService`; edited via `/admin/gamification/settings`. Consumed by `SkillTreeService` (goals) and `DialogService`/`OpenAiChatService` (dialog scoring).

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

Per-exercise-type base XP, replacing the hardcoded flat 10. Seeded with all 10 exercise types → 10 by `20260616130000_AddGamificationSettings`. Read by `GamificationService.GetExerciseBaseXpAsync` (falls back to 10 for unknown types); edited via `/admin/gamification/exercise-rewards/:exerciseType` (upsert).

| Column         | Type                     | Nullable | Notes                                  |
|----------------|--------------------------|----------|----------------------------------------|
| `Id`           | `uuid`                   | NOT NULL | PK                                     |
| `ExerciseType` | `character varying(40)`  | NOT NULL | UNIQUE — see `ExerciseTypes` constants |
| `BaseXpReward` | `integer`                | NOT NULL | XP on correct/passed answer            |

### `StreakMilestones`

Admin-editable streak-bonus ladder, replacing the hardcoded `7→50, 30→200` switch. Seeded with those two rows. Read by `GamificationService.GetStreakBonusXpAsync` — authoritative when non-empty, otherwise the historic ladder is used. Managed via `/admin/gamification/streak-milestones` (CRUD).

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
| `IconEmoji`          | `text`    | NOT NULL | Emoji shown in the badge UI                                 |
| `ConditionType`      | `text`    | NOT NULL | `first_lesson` / `lesson_count` / `xp_total` / `streak_days` / `skill_completed` |
| `ConditionThreshold` | `integer` | NOT NULL | Numeric threshold; 0 for event-based conditions             |
| `SortOrder`          | `integer` | NOT NULL |                                                             |

---

### `UserAchievements`

| Column          | Type                       | Nullable | Notes                     |
|-----------------|----------------------------|----------|---------------------------|
| `Id`            | `uuid`                     | NOT NULL | PK                        |
| `UserId`        | `uuid`                     | NOT NULL | FK → `Users.Id` CASCADE   |
| `AchievementId` | `uuid`                     | NOT NULL | FK → `Achievements.Id` CASCADE |
| `UnlockedAt`    | `timestamp with time zone` | NOT NULL |                           |

---

### `DiscussPhotos`

Photo attachments for Discuss threads and replies. Polymorphic owner (no FK), mirroring `DiscussVotes`. See [DISCUSS.md](DISCUSS.md#photos).

| Column        | Type            | Nullable | Notes                                                  |
|---------------|-----------------|----------|--------------------------------------------------------|
| `Id`          | `uuid`          | NOT NULL | PK                                                     |
| `OwnerType`   | `integer`       | NOT NULL | 0=Thread, 1=Reply                                      |
| `OwnerId`     | `uuid`          | NOT NULL | thread id or reply id (polymorphic, no FK — mirrors `DiscussVotes`) |
| `ObjectKey`   | `varchar(512)`  | NOT NULL | S3 object key                                          |
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

**Index:** `participantIds` for efficient conversation lookup by user.

---

## Redis

| Key pattern                        | Type   | TTL      | Purpose                              |
|------------------------------------|--------|----------|--------------------------------------|
| `session:{userId}`                 | Hash   | 24h      | Session data                         |
| `league:weekly:{leagueId}`         | Sorted | Until EOW| Weekly XP leaderboard                |
| `user:xp_total:{userId}`           | String | —        | Cached total XP (invalidated on earn)|
| `presence:online`                  | Sorted | —        | Online-presence (member=userId, score=last-seen unix sec); pruned to a 5-min window by the metrics updater — see [MONITORING.md](MONITORING.md) |

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
| `AddGamificationSettings`             | 2026-06-16 | `GamificationSettings` (singleton), `ExerciseTypeRewards`, `StreakMilestones` tables — DB-driven XP economy, all seeded with historic defaults |
| `AddSkillStages`                      | 2026-06-16 | `SkillStages` table (seeded preparation/discovery/engagement/closing/retention) — DB-driven, admin-editable funnel stages for the skill tree |
| `InitialCompanySchema` (company-service) | 2026-07-09 | Standalone `company` database: `Companies`, `CallLogEntries`, `PracticeCalls` tables. Owned by company-service (port 5009). |
| `AddCompanyContacts` (company-service)   | 2026-07-09 | `CompanyContacts` table (mini-CRM, Phase 39.9); `CallLogEntries.ContactId` nullable FK → `CompanyContacts(Id)` ON DELETE SET NULL. |
| `AddCompanyStatus` (company-service)     | 2026-07-10 | `Companies.Status` varchar(32) NOT NULL DEFAULT 'Lead' (status pipeline, Phase 39.10); plain `AddColumn` with a Postgres column default, so existing rows read as `Lead` without a separate `UPDATE`. |
| `AddCompanyFollowUp` (company-service)   | 2026-07-10 | `Companies.NextActionAt` (timestamptz, nullable), `NextActionNote` (varchar(2000), nullable), `FollowUpNotifiedAt` (timestamptz, nullable) (follow-up reminders, Phase 39.11); sparse index `IX_Companies_NextActionAt` (filtered `WHERE "NextActionAt" IS NOT NULL`) keeps the reminder poll cheap. |
| `AddCompanyBriefing` (company-service)   | 2026-07-10 | `Companies.BriefingContent` (text, nullable), `BriefingGeneratedAt` (timestamptz, nullable) (AI pre-call briefing cache, Phase 39.12); plain `AddColumn`, no index (read only via the single-row `GET/POST /companies/{id}/briefing`). |
| `AddCompanyPersonas` (company-service)   | 2026-07-10 | `CompanyPersonas` table (AI persona generation, Phase 39.14); FK → `Companies(Id)` ON DELETE CASCADE. |
| `AddCompanyReadiness` (company-service)  | 2026-07-10 | `Companies.ReadinessJson` (text, nullable), `ReadinessGeneratedAt` (timestamptz, nullable) (AI readiness-score cache, Phase 39.16); plain `AddColumn`, no index (read/written only via the single-row `GET /companies/{id}/readiness`). |
| `AddCompanyReadinessNoFeedbackCache` (company-service) | 2026-07-11 | `Companies.ReadinessNoFeedbackUntil` (timestamptz, nullable) — negative-cache expiry for the "ai-service returned 204 / no usable feedback yet" readiness result (PR #26 review fast-follow, 39.17); plain `AddColumn`, no index (read/written only via the single-row `GET /companies/{id}/readiness`). |

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
