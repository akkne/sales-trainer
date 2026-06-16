# DB Schema

Last updated: 2026-06-12

## Databases overview

| Store      | Purpose                                      |
|------------|----------------------------------------------|
| PostgreSQL | Primary — all structured data                |
| MongoDB    | Chat messages and unstructured dialogue data |
| Redis      | Cache, sessions, leaderboard rankings        |

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
| `Stage`       | `text`    | NOT NULL | Funnel stage bucket (DEFAULT `general`). Known values: `preparation`, `discovery`, `engagement`, `closing`, `retention`. |

Indexes: `IX_Skills_IconicName` (unique), `IX_Skills_Stage`.

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

| Column        | Type                       | Nullable | Notes                              |
|---------------|----------------------------|----------|------------------------------------|
| `Id`          | `uuid`                     | NOT NULL | PK                                 |
| `RequesterId` | `uuid`                     | NOT NULL | FK → `Users.Id` — who sent        |
| `AddresseeId` | `uuid`                     | NOT NULL | FK → `Users.Id` — who received    |
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
