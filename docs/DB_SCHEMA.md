# DB Schema

Last updated: 2026-04-02

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
| `Role`         | `integer`                   | NOT NULL | 0=User, 1=Admin, 2=SuperAdmin      |
| `CreatedAt`    | `timestamp with time zone`  | NOT NULL |                                    |

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

| Column                  | Type      | Nullable | Notes                                              |
|-------------------------|-----------|----------|----------------------------------------------------|
| `Id`                    | `uuid`    | NOT NULL | PK                                                 |
| `Slug`                  | `text`    | NOT NULL | URL-friendly identifier                            |
| `Title`                 | `text`    | NOT NULL |                                                    |
| `IconName`              | `text`    | NOT NULL |                                                    |
| `SortOrder`             | `integer` | NOT NULL | Display order in skill tree                        |
| `PrerequisiteSkillId`   | `uuid`    | NULL     | Self-referencing FK for sequential unlocking       |
| `ApplicableSalesTypes`  | `text[]`  | NOT NULL | Array of sales type strings                        |

---

### `Lessons`

| Column           | Type      | Nullable | Notes                         |
|------------------|-----------|----------|-------------------------------|
| `Id`             | `uuid`    | NOT NULL | PK                            |
| `SkillId`        | `uuid`    | NOT NULL | FK → `Skills.Id`              |
| `Title`          | `text`    | NOT NULL |                               |
| `SortOrder`      | `integer` | NOT NULL |                               |
| `DifficultyLevel`| `integer` | NOT NULL |                               |
| `XpReward`       | `integer` | NOT NULL | XP awarded on lesson complete |

---

### `Exercises`

| Column              | Type      | Nullable | Notes                                                                         |
|---------------------|-----------|----------|-------------------------------------------------------------------------------|
| `Id`                | `uuid`    | NOT NULL | PK                                                                            |
| `LessonId`          | `uuid`    | NOT NULL | FK → `Lessons.Id`                                                             |
| `Type`              | `text`    | NOT NULL | `multiple_choice` / `fill_blank` / `free_text`                                |
| `SortOrder`         | `integer` | NOT NULL |                                                                               |
| `SerializedContent` | `jsonb`   | NOT NULL | Schema varies by type — see [Exercise Content Schemas](#exercise-content-schemas) |

---

### `ReferenceMaterials`

| Column            | Type      | Nullable | Notes              |
|-------------------|-----------|----------|--------------------|
| `Id`              | `uuid`    | NOT NULL | PK                 |
| `SkillId`         | `uuid`    | NOT NULL | FK → `Skills.Id`   |
| `Title`           | `text`    | NOT NULL |                    |
| `MarkdownContent` | `text`    | NOT NULL |                    |
| `SortOrder`       | `integer` | NOT NULL |                    |

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
| `SerializedAiFeedback`| `jsonb`                    | NULL     | Present for `free_text` type only  |
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
| `Source`   | `text`                     | NOT NULL | `exercise` / `streak_bonus` / `league_bonus`     |
| `EarnedAt` | `timestamp with time zone` | NOT NULL |                                                  |

---

### `Leagues`

| Column          | Type      | Nullable | Notes                                           |
|-----------------|-----------|----------|-------------------------------------------------|
| `Id`            | `uuid`    | NOT NULL | PK                                              |
| `Tier`          | `text`    | NOT NULL | `bronze` / `silver` / `gold` / `diamond`        |
| `WeekStartDate` | `date`    | NOT NULL |                                                 |
| `WeekEndDate`   | `date`    | NOT NULL |                                                 |

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

### `ExerciseTypePrompts`

| Column        | Type                       | Nullable | Notes                                          |
|---------------|----------------------------|----------|------------------------------------------------|
| `Id`          | `uuid`                     | NOT NULL | PK                                             |
| `ExerciseType`| `text`                     | NOT NULL | UNIQUE — type key (`find_error`, `ai_dialog`, etc.) |
| `SystemPrompt`| `text`                     | NOT NULL | Global system prompt for all exercises of type |
| `UpdatedAt`   | `timestamp with time zone` | NOT NULL |                                                |

Used for AI-powered exercise types: `find_error`, `rewrite_better`, `ai_dialog`, `rate_call`, `written_answer`.
Prompt is combined with per-exercise `aiPrompt` from `SerializedContent`.

---

## Exercise Content Schemas

The `Exercises.SerializedContent` (jsonb) varies by `Type`:

### `multiple_choice`
```json
{
  "situation": "string",
  "options": [
    { "text": "string", "isCorrect": true }
  ],
  "explanation": "string"
}
```

### `fill_blank`
```json
{
  "templateText": "string (use ___ for blank)",
  "correctAnswer": "string",
  "acceptableAnswers": ["string"]
}
```

### `free_text`
```json
{
  "prompt": "string",
  "characterName": "string",
  "characterReplica": "string",
  "evaluationCriteria": "string"
}
```

### `ordering`
```json
{
  "situation": "string",
  "items": [{"id": "string", "text": "string"}],
  "correctOrder": ["string"],
  "explanation": "string (optional)"
}
```

### `matching`
```json
{
  "situation": "string",
  "leftColumn": [{"id": "string", "text": "string"}],
  "rightColumn": [{"id": "string", "text": "string"}],
  "correctPairs": [{"left": "string", "right": "string"}],
  "explanation": "string (optional)"
}
```

### `categorizing`
```json
{
  "situation": "string",
  "items": [{"id": "string", "text": "string"}],
  "categories": [{"id": "string", "title": "string", "color": "string (hex)"}],
  "correctMapping": {"itemId": "categoryId"},
  "explanation": "string (optional)"
}
```

### `find_error`
```json
{
  "situation": "string",
  "dialogLines": [{"id": "string", "speaker": "string", "text": "string"}],
  "errorLineId": "string",
  "aiPrompt": "string (optional, per-exercise evaluation criteria)",
  "requireExplanation": "boolean (optional)",
  "suggestedFixes": [{"id": "string", "text": "string"}],
  "correctFixIds": ["string"]
}
```

### `rewrite_better`
```json
{
  "situation": "string",
  "originalText": "string",
  "context": "string (optional)",
  "aiPrompt": "string",
  "minLength": "number (optional)",
  "maxLength": "number (optional)"
}
```

### `ai_dialog`
```json
{
  "situation": "string",
  "persona": {"name": "string", "role": "string", "description": "string"},
  "chatSystemPrompt": "string",
  "aiPrompt": "string (final evaluation criteria)",
  "maxTurns": "number (optional, default 10)",
  "minTurnsForCompletion": "number (optional, default 4)"
}
```

### `rate_call`
```json
{
  "situation": "string",
  "transcript": [{"speaker": "string", "text": "string"}],
  "criteria": [{"id": "string", "name": "string", "description": "string"}],
  "ratingScale": {"min": "number", "max": "number"},
  "aiPrompt": "string"
}
```

### `written_answer`
```json
{
  "prompt": "string",
  "context": "string (optional)",
  "aiPrompt": "string",
  "minLength": "number (optional)",
  "maxLength": "number (optional)"
}
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

Planned future collections: `call_transcripts` (transcripts + scorecards).

---

## Redis

| Key pattern                        | Type   | TTL      | Purpose                              |
|------------------------------------|--------|----------|--------------------------------------|
| `session:{userId}`                 | Hash   | 24h      | Session data                         |
| `league:weekly:{leagueId}`         | Sorted | Until EOW| Weekly XP leaderboard                |
| `user:xp_total:{userId}`           | String | —        | Cached total XP (invalidated on earn)|

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

## Migrations history

| Migration name            | Date       | Summary                                      |
|---------------------------|------------|----------------------------------------------|
| `InitialSchema`           | 2026-03-31 | All base tables                              |
| `AddRefreshTokenUserFk`   | 2026-04-01 | FK + index on `RefreshTokens.UserId`         |
| `AddUserRole`             | 2026-04-01 | `Role` integer column on `Users` (default 0) |
| `AddAchievements`         | 2026-04-05 | `Achievements` and `UserAchievements` tables  |
| `AddPersonaToUserProfile` | 2026-04-05 | `Persona` nullable text column on `UserProfiles` |
