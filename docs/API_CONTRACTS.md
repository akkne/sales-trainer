# API_CONTRACTS.md

Base URL: `http://localhost:5000` (dev) | `http://backend:8080` (docker internal)

All endpoints except those marked `[public]` require `Authorization: Bearer <accessToken>`.

> **Microservices migration:** `/auth/*`, `/demo/*`, `/profile/*`, `/onboarding/*` and
> `/avatars/*` are now served by the extracted **Identity service** (gateway base URL
> `http://localhost:5000`), not the monolith. Paths and request/response shapes are
> unchanged. One transitional caveat: `GET /profile` returns the streak / XP / completed-
> skill / average-score aggregates as **0** because those are owned by Gamification/Learning
> (not extracted yet, roadmap phases 7 & 8); the identity fields (displayName, email,
> persona, avatarUrl) are real. See [IDENTITY_SERVICE.md](IDENTITY_SERVICE.md).

---

## Auth `[public]`

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /auth/register | `{email, password, displayName}` | `RegistrationResultDto` (no tokens) |
| POST | /auth/verify-email | `{email, code}` | `AuthTokenResponseDto` + cookie `refreshToken` |
| POST | /auth/resend-code | `{email}` | 204 |
| POST | /auth/login | `{email, password}` | `AuthTokenResponseDto` + cookie |
| POST | /auth/google | `{idToken}` | `AuthTokenResponseDto` + cookie |
| POST | /auth/refresh `[public]` | — (reads cookie) | `AuthTokenResponseDto` + new cookie |
| POST | /auth/logout | — | 204 |
| POST | /demo/token `[public]` | — | `{accessToken, expiresInSeconds}` |

`AuthTokenResponseDto`: `{accessToken, userId, displayName, isOnboardingCompleted, role}`
`RegistrationResultDto`: `{email, requiresEmailVerification}`

**Email verification flow.** `/auth/register` creates an unverified user and emails a
short-lived numeric code (MailerSend); it returns `RegistrationResultDto` instead of tokens.
The client then calls `/auth/verify-email` with the code to receive tokens. `/auth/resend-code`
re-issues a code (silent 204 for unknown/already-verified emails to avoid account enumeration;
`429` with `Retry-After` + `{retryAfterSeconds}` while a resend cooldown is active).
`/auth/verify-email` returns `401` on an invalid/expired/exhausted code.
`/auth/login` returns `403 {message, requiresEmailVerification: true, email}` when the address
is not yet verified. Google sign-in is auto-verified. See [EMAIL_VERIFICATION.md](EMAIL_VERIFICATION.md).

---

## Onboarding

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /onboarding | `{salesType, experienceLevel, selectedSkillSlugs, persona?}` | 204 |

`salesType`: `b2b_saas` / `retail` / `real_estate` / `finance` / `b2c`  
`experienceLevel`: `beginner` / `experienced` / `manager`  
`selectedSkillSlugs`: array of skill slugs the user wants to enroll in (e.g. `["sales-basics","cold-calls"]`).
`sales-basics` is always included by the backend regardless of the payload.

---

## Skill Tree

> **Microservices (Phase 8):** all learner + admin content routes below — `/skills/*`,
> `/skill-tree`, `/lessons/*`, `/topics/*`, `/exercises/*`, `/reference/*`,
> `/techniques/*`, `/daily-quote`, and the content `/admin/*` routes — are served by the
> extracted **[learning-service](LEARNING_SERVICE.md)** through the gateway. Paths and
> shapes are unchanged. Two shape-preserving notes: the exercise-submission DTO returns
> `xpEarned: 0` and an empty `newlyUnlockedAchievementKeys` (XP/achievements now belong
> to gamification, granted asynchronously from the `exercise.completed` event), and
> `/skill-tree` returns the streak/XP/goal aggregate fields as `0` (owned by
> gamification). AI-graded exercise types are scored by the learning-service calling the
> ai-service `POST /ai/evaluate`.

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /skill-tree | — | `SkillTreeResponseDto` |
| GET | /skills | — | `SkillTreeNodeDto[]` (all skills, `locked` if not enrolled) |
| GET | /skills/stages | — | `SkillStageDto[]` (admin-configured funnel stages, ordered) |
| PUT | /skills/enrolled | `{skillSlugs: string[]}` | 204 |

`PUT /skills/enrolled` — replaces the user's enrolled skill set.  
Skills in the list that are not yet enrolled are set to `available`.  
Skills currently enrolled but absent from the list are set to `locked` (progress preserved).  
`sales-basics` is always kept enrolled.

`SkillTreeResponseDto`: `{skillNodes[], currentStreakDayCount, totalXpAmount, weeklyXpAmount, dailyXpAmount, dailyXpGoal, weeklyXpGoal}`  
`dailyXpAmount`/`weeklyXpAmount` = XP earned today / this week (UTC); `dailyXpGoal`/`weeklyXpGoal` = targets from the admin-editable `GamificationSettings` table (defaults 100 / 500), not hardcoded config.  
`SkillTreeNodeDto`: `{skillId, slug, title, iconName, sortOrder, status, completedLessonCount, totalLessonCount, isLocked, stage}`. `stage` is the funnel-stage bucket the skill belongs to — see `Skills.Stage` in [DB_SCHEMA](DB_SCHEMA.md).  
`SkillStageDto`: `{key, label, accent, order}` — the admin-editable display metadata for a funnel stage (label, CSS accent color, sort order). The frontend groups `/tree` by `stage` and resolves each bucket's label/color via this list, falling back to built-in defaults while it loads. Stages are managed via the admin endpoints below; `general` is the implicit fallback bucket for unassigned skills and is not a stored row.

---

## Lessons & Exercises

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /skills/:slug/lessons | — | `LessonSummaryDto[]` |
| GET | /lessons | — | `LessonSummaryDto[]` (all skills) |
| GET | /lessons/:lessonId/exercises | — | `ExerciseDto[]` |
| GET | /lessons/:lessonId/next | — | `NextLessonDto` or 204 if no next lesson |
| POST | /exercises/:exerciseId/submit | `{answer: <jsonb>}` | `ExerciseSubmissionResultDto` |
| POST | /exercises/:exerciseId/chat | `{message: string}` | `ExerciseChatResponseDto` |
| POST | /exercises/:exerciseId/voice/stream | `{message: string}` | `application/octet-stream` — length-prefixed frames |

`LessonSummaryDto`: `{lessonId, title, orderInTopic, topicOrder, status, bestScore, kind}` where `kind` is `"theory"` (every exercise is a `theory_card`) or `"practice"`. Theory lessons are played as swipeable cards; the client submits the last card once to complete them. Across a skill, lessons are ordered by `topicOrder` (the topic's `OrderInSkill`) first, then by `orderInTopic` — so topics stay grouped instead of interleaving; the client sorts by `(topicOrder, orderInTopic)`.

**AI Dialog Chat Endpoint:**
`POST /exercises/:exerciseId/chat` — for `ai_dialog` type exercises only. Handles multi-turn conversation.
`ExerciseChatResponseDto`: `{response: string, isComplete: boolean, turnNumber: number, maxTurns: number}`
The **user speaks first** — an empty `message` returns an empty turn (no AI greeting); the AI only replies after the user's opening line.

**AI Dialog Voice Endpoint:**
`POST /exercises/:exerciseId/voice/stream` — voice mode for `ai_dialog` exercises. Streams the same length-prefixed `[flags u32][textLen u32][text][audioLen u32][audioMp3]` frames as the live-call voice stream (`flags` bit0 = isFinal, bit1 = isStopSignal/endCall). Shares chat history with `/chat`, so text and voice turns interleave. Uses the same TTS pipeline as calls.

**Lesson unlock behavior:**
- First call to `GET /skills/:slug/lessons` lazy-seeds `UserLessonProgress` rows: lesson 1 → `available`, rest → `locked`.
- Submitting a correct answer marks the lesson `completed` and sets the next lesson (by `sortOrder`) to `available`.
- See [LESSON_UNLOCK.md](LESSON_UNLOCK.md) for full details.

`ExerciseDto.content` shape by type:

**multiple_choice**: `{situation, question, options[], correctOptionIndex, explanation?}`
**fill_blank**: `{characterName, characterLine, options[], correctOptionIndex, explanation?}`
**free_text**: `{situation, prompt, evaluationCriteria}`
**ordering**: `{situation, items[{id, text}], correctOrder[], explanation?}`
**matching**: `{situation, leftColumn[{id, text}], rightColumn[{id, text}], correctPairs[{left, right}], explanation?}`
**categorizing**: `{situation, items[{id, text}], categories[{id, title, color}], correctMapping{itemId: categoryId}, explanation?}`
**find_error**: `{situation, dialogLines[{id, speaker, text}], errorLineId, aiPrompt?, requireExplanation?, suggestedFixes?[{id, text}], correctFixIds?[]}`
**rewrite_better**: `{situation, originalText, context?, aiPrompt, minLength?, maxLength?}`
**ai_dialog**: `{situation, persona{name, role, description}, chatSystemPrompt, aiPrompt, maxTurns?, minTurnsForCompletion?}`
**rate_call**: `{situation, transcript[{speaker, text}], criteria[{id, name, description}], ratingScale{min, max}, aiPrompt}`
**written_answer**: `{prompt, context?, aiPrompt, minLength?, maxLength?}`

`answer` shape by type:

**multiple_choice / fill_blank**: `{selectedOptionIndex: number}`
**free_text**: `{text: string}`
**ordering**: `{order: string[]}` — item IDs in user's order
**matching**: `{pairs: [{left, right}]}`
**categorizing**: `{mapping: {itemId: categoryId}}`
**find_error**: `{selectedLineId, explanation?, selectedFixId?}`
**rewrite_better**: `{rewrittenText: string}`
**ai_dialog**: `{messages: [{role, content}], completedNaturally: boolean}`
**rate_call**: `{ratings: {criterionId: number}, overallComment?: string}`
**written_answer**: `{text: string}`

`NextLessonDto`: `{lessonId, title, xpReward}` — next lesson in same skill with status `available` or `in_progress`. Returns 204 when no next lesson exists.

---

## Reference

| Method | Path | Response |
|---|---|---|
| GET | /skills/:slug/reference | `ReferenceMaterialDto[]` |

`ReferenceMaterialDto`: `{materialId, title, markdownContent, sortOrder}`

---

## Techniques (Handbook / "Коллекция")

All routes require auth. Card response includes per-user mastery state; `/meta` aggregates per-user counts. See [HANDBOOK_REDESIGN.md](HANDBOOK_REDESIGN.md).

| Method | Path | Query / Body | Response |
|---|---|---|---|
| GET | /techniques | `?skill=&search=&tag=` (repeatable) | `TechniqueCardDto[]` |
| GET | /techniques/meta | — | `TechniqueMetaDto` |
| GET | /techniques/:slug | — | `TechniqueDetailDto` |
| POST | /techniques/:slug/seen | `{}` | 204 (sets `FirstSeenAt`, clears `isNew`) |

`skill` filter matches `Skills.IconicName` (not id) so URLs stay human-readable. `tag` can be repeated (`?tag=objection&tag=discovery`) — AND semantics. `search` matches (case-insensitive) on `Name`, `Summary`, `Body`, and `Tags`.

`TechniqueCardDto`: `{id, slug, name, summary, tags: string[], primarySkillIconicName?, primarySkillTitle?, difficulty, difficultyName, sortOrder, masteryLevel, masteryPercent, hasDialog, hasCase, hasCoach, isNew}`

`difficulty`: 1=Novice, 2=Practitioner, 3=Expert, 4=Master — static per-technique property. `difficultyName` is its display form. `masteryLevel` / `masteryPercent` are per-user. `hasDialog` / `hasCase` / `hasCoach` let the card show the right tabs without the detail round-trip.

`TechniqueDetailDto`: `{card: TechniqueCardDto, body, skillIconicNames: string[], dialogTurns: TechniqueDialogTurnDto[], case?: TechniqueCaseDto, coach?: TechniqueCoachDto}`

`TechniqueDialogTurnDto`: `{orderIndex, side: "me"|"them", text, annotations: [{label, tone?}]}`
`TechniqueCaseDto`: `{title, body, metrics?}` — `metrics` is a free JSON object (e.g. `{deal: "$124k", cycleDays: 41}`). At most one case per technique.
`TechniqueCoachDto`: `{avatarSeed, name, role, quote, challenges: [{label, kind?, targetSlug?}]}`

`TechniqueMetaDto`: `{skills: [{iconicName, title, techniqueCount}], totalCount, userCounts: {mastered, master, unseen}}`. Only skills that have at least one technique appear in `skills`.

---

## Profile

> **Microservices (Phase 7):** `/profile/achievements` is served by the extracted
> **[gamification-service](GAMIFICATION_SERVICE.md)** through the gateway (more specific
> than Identity's `/profile/*`); the response shape is unchanged. The streak/XP/skill
> aggregates inside `GET /profile` (Identity) are composed from gamification's
> `GET /gamification/progress` once Identity consumes it (Phase 2 caveat).

| Method | Path | Response |
|---|---|---|
| GET | /profile | `UserProfileStatsDto` |
| GET | /profile/achievements | `AchievementDto[]` |
| PUT | /profile/persona | `{persona: string}` → 204 |
| PUT | /profile | `{displayName: string (1–100, required), persona?: string}` → 204 |

> `PUT /profile` updates the user's display name (and, when `persona` is provided and
> valid, upserts the persona in one call). `displayName` is trimmed; empty → `400`,
> `>100` chars → `400`, `persona` outside the allow-list (`sdr`, `account_executive`,
> `account_manager`, `founder`, `other`) → `400`, unknown user → `404`. A successful
> update publishes `UserUpdatedEvent` so replica-holding services (ai, notification, …)
> refresh their cached display name.

### Gamification progress (Phase 7)

Served by the gamification-service through the gateway.

| Method | Path | Response |
|---|---|---|
| GET | /gamification/progress | `GamificationProgressDto` |

`GamificationProgressDto`: `{currentStreakDayCount, longestStreakDayCount, totalXpAmount, dailyXpAmount, weeklyXpAmount, dailyXpGoal, weeklyXpGoal}`

`UserProfileStatsDto`: `{displayName, email, currentStreakDayCount, longestStreakDayCount, totalXpAmount, completedSkillCount, totalSkillCount, averageExerciseScore, persona?, avatarUrl}`

`persona` values: `sdr` | `account_executive` | `account_manager` | `founder` | `other`

`AchievementDto`: `{achievementId, key, title, description, iconEmoji, isUnlocked, unlockedAt}`

Achievement condition types: `first_lesson` | `lesson_count` | `xp_total` | `streak_days` | `skill_completed`

`ExerciseSubmissionResultDto` now includes `newlyUnlockedAchievementKeys: string[]` — keys of achievements unlocked in this submit.

---

## League

> **Microservices (Phase 7):** `/league` (and `/admin/leagues/*`, `/admin/gamification/*`)
> are served by the extracted **[gamification-service](GAMIFICATION_SERVICE.md)** through
> the gateway — paths and DTO shapes unchanged. League data is DB-backed on the
> `gamification` Postgres database; participant display names/avatars come from a local
> `UserReplica` (no join into Identity). The weekly closure job runs on the
> gamification service's own Hangfire schema.

| Method | Path | Response |
|---|---|---|
| GET | /league | `CurrentLeagueResponseDto` |

`CurrentLeagueResponseDto`: `{leagueId, tier, tierName, tierColor, weekStartDate, weekEndDate, periodEndsAt, participantsByRank[], currentUserRank, previousWeekOutcome: "promoted"|"demoted"|null, promotionZoneSize, demotionZoneSize, maximumLeagueParticipantCount}`

- `promotionZoneSize`/`demotionZoneSize`/`maximumLeagueParticipantCount`: live from `LeagueSettings` (admin-configurable). The user league page must render zones from these, not hardcoded constants.
- `tierName`/`tierColor`: presentation for `tier`, resolved from the admin-editable `LeagueTiers` table (fall back to the tier key + neutral color if the tier was deleted).
- `periodEndsAt` (ISO-8601 instant): exact moment the current period closes. The league-tab countdown MUST target this, not the day-start of `weekEndDate`.
`LeagueParticipantDto`: `{userId, displayName, weeklyXpAmount, rank, isCurrentUser, avatarUrl}`

Tiers: configurable via the `LeagueTiers` table (admin CRUD below). Default ladder `bronze → silver → gold → diamond`; the promotion ladder follows `Order` ascending (entry tier = lowest order).
- Top N per tier promoted to next tier next period, bottom M demoted (cannot drop below the lowest-order tier) — zone sizes come from the `LeagueSettings` table (defaults: promotion 10, demotion 5, max participants 30), editable via `/admin/leagues/settings`
- Period scheduling: the current period start/end live in `LeagueSettings` (`CurrentPeriodStartDate`, `CurrentPeriodEndsAt`, `PeriodLengthDays`). A recurring job (every 15 min) closes the period and creates the next only once `CurrentPeriodEndsAt` has passed, so an admin-set end date drives the schedule.
- `previousWeekOutcome`: shown only if user had a membership last week; use for in-app banner

---

## Daily Quote

| Method | Path | Response |
|---|---|---|
| GET | /daily-quote?date=YYYY-MM-DD | `DailyQuoteDto` or 204 if no quotes exist yet |

`DailyQuoteDto`: `{text, author, date}`. `date` query param is optional (defaults to UTC today; the frontend passes the client's local date). Returns the quote for the requested date, falling back to the most recent quote at or before it — so the widget keeps showing the last scheduled quote on days without a dedicated one. Requires auth (any role); managed via `/admin/daily-quotes` (see ADMIN_PANEL.md).

---

## Auth — updated response

`AuthTokenResponseDto` now includes `role: "User" | "Admin" | "SuperAdmin"`.

---

## Admin (requires `RequireAdmin` policy — role Admin or SuperAdmin)

All routes prefixed `/admin`. Unauthorized → 403.

### Skills
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/skills | — | `AdminSkillDto[]` |
| POST | /admin/skills | `{iconicName, title, description?, orderInTree, stage?}` | `AdminSkillDto` |
| PUT | /admin/skills/:id | `{iconicName?, title?, description?, orderInTree?, stage?}` | `AdminSkillDto` |
| DELETE | /admin/skills/:id | — | 204 |

`AdminSkillDto`: `{id, iconicName, title, description, orderInTree, stage}`. `stage` is the `key` of a configured Skill Stage (see below) — built-in keys are `preparation`, `discovery`, `engagement`, `closing`, `retention`; `general` is the fallback default. Drives the grouped sidebar on `/tree`.

### Skill Stages
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/skill-stages | — | `AdminSkillStageDto[]` |
| POST | /admin/skill-stages | `{key, label, accent, order}` | `AdminSkillStageDto` |
| PUT | /admin/skill-stages/:id | `{label, accent, order}` | `AdminSkillStageDto` |
| DELETE | /admin/skill-stages/:id | — | 204 |

`AdminSkillStageDto`: `{id, key, label, accent, order}`. The funnel stages used to group skills on `/tree`, replacing the previously frontend-hardcoded list. `key` is the immutable slug stored on `Skills.Stage`; only `label`, `accent` (CSS color, e.g. `#7C3AED` or `var(--indigo)`), and `order` are editable. `key` is lowercased and must be unique. **Create** rejects a duplicate key (`400`); **Update** ignores any key change. **Delete** is blocked (`400`) while any skill is still assigned to the stage — reassign those skills first. Seeded defaults match the original 5 stages; `general` is the implicit fallback and is intentionally not a stored row.

### Topics
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/topics | — | `AdminTopicWithSkillDto[]` |
| GET | /admin/skills/:skillIconicName/topics | — | `AdminTopicDto[]` |
| POST | /admin/skills/:skillIconicName/topics | `{iconicName, title, orderInSkill}` | `AdminTopicDto` |
| PUT | /admin/topics/:id | `{iconicName?, title?, orderInSkill?}` | `AdminTopicDto` |
| DELETE | /admin/topics/:id | — | 204 |

`AdminTopicDto`: `{id, skillId, iconicName, title, orderInSkill}`
`AdminTopicWithSkillDto`: `{id, skillId, skillIconicName, skillTitle, iconicName, title, orderInSkill}`

### Lessons
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/lessons | — | `AdminLessonWithTopicDto[]` |
| GET | /admin/topics/:topicIconicName/lessons | — | `AdminLessonDto[]` |
| POST | /admin/topics/:topicIconicName/lessons | `{title, orderInTopic}` | `AdminLessonDto` |
| PUT | /admin/lessons/:id | `{title, orderInTopic}` | `AdminLessonDto` |
| DELETE | /admin/lessons/:id | — | 204 |

`AdminLessonDto`: `{id, topicId, title, orderInTopic}`
`AdminLessonWithTopicDto`: `{id, topicId, topicIconicName, topicTitle, title, orderInTopic}`

### Exercises
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/lessons/:lessonId/exercises | — | `AdminExerciseDto[]` |
| POST | /admin/lessons/:lessonId/exercises | `{type, orderInLesson, content: <jsonb>, customAiPrompt?}` | `AdminExerciseDto` (400 if content invalid per type) |
| POST | /admin/lessons/:lessonId/exercises/import | `[{type, orderInLesson, content, customAiPrompt?}, …]` (array) | `ExercisesImportResultDto` (per-item validation; bad items skipped, reported in errors) |
| PUT | /admin/exercises/:id | same | `AdminExerciseDto` (400 if content invalid per type) |
| DELETE | /admin/exercises/:id | — | 204 |

**Content validation:** The `content` field is validated server-side per exercise type. Single create/update return 400 with joined error messages on invalid content. Import validates each exercise; bad ones are skipped and reported in the `errors` array with per-item messages. See [NEW_EXERCISE_TYPES.md](NEW_EXERCISE_TYPES.md) for per-type content schema.

`ExercisesImportResultDto`: `{exercisesCreated, exercisesUpdated, errors[]}`. Bulk upsert by `orderInLesson` within the lesson; empty array → 400, unknown lesson → 404. The admin exercises page exports the lesson's exercises in exactly this array shape (re-importable).

`AdminExerciseDto`: `{id, lessonId, type, orderInLesson, content, customAiPrompt}`

### Exercise Type Prompts
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/exercise-type-prompts | — | `ExerciseTypePromptDto[]` |
| GET | /admin/exercise-type-prompts/:exerciseType | — | `ExerciseTypePromptDto` |
| PUT | /admin/exercise-type-prompts/:exerciseType | `{systemPrompt}` | `ExerciseTypePromptDto` |

`ExerciseTypePromptDto`: `{id, exerciseType, systemPrompt, updatedAt}`

### Gamification (XP)
All XP-economy knobs are DB-driven and admin-editable (no hardcoded constants).

> **Microservices (Phase 7):** `/admin/gamification/*` is served by the
> **[gamification-service](GAMIFICATION_SERVICE.md)** through the gateway — shapes
> unchanged. On `PUT /admin/gamification/settings` the service additionally emits a
> `gamification.dialog-weights.updated` Kafka event so the ai-service refreshes its
> cached dialog scoring weights (replacing the old in-process read).

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/gamification/settings | — | `GamificationSettingsDto` |
| PUT | /admin/gamification/settings | `UpdateGamificationSettingsRequestDto` | `GamificationSettingsDto` |
| GET | /admin/gamification/exercise-rewards | — | `ExerciseTypeRewardDto[]` |
| PUT | /admin/gamification/exercise-rewards/:exerciseType | `{baseXpReward}` | `ExerciseTypeRewardDto` (upsert) |
| GET | /admin/gamification/streak-milestones | — | `StreakMilestoneDto[]` |
| POST | /admin/gamification/streak-milestones | `{dayCount, xpReward}` | `StreakMilestoneDto` (400 on duplicate `dayCount`) |
| PUT | /admin/gamification/streak-milestones/:id | `{dayCount, xpReward}` | `StreakMilestoneDto` |
| DELETE | /admin/gamification/streak-milestones/:id | — | 204 |

`GamificationSettingsDto` / `UpdateGamificationSettingsRequestDto`: `{dailyXpGoal, weeklyXpGoal, dialogXpMultiplier, dialogWeightConfidence, dialogWeightStructure, dialogWeightObjection, dialogWeightGoal}`
- `dailyXpGoal`/`weeklyXpGoal` must be positive; `dialogXpMultiplier` must be positive; criterion weights are non-negative and must sum to > 0.
- **Dialog XP**: the AI scores a completed dialog on four criteria, each capped at its weight (raw score range `0..Σweights`). Earned XP = `round(rawScore × dialogXpMultiplier)`. The criterion maximums are injected into the feedback prompt, so editing weights re-shapes how the AI distributes points.
- **Exercise XP**: `baseXpReward` per exercise type is awarded on a correct/passed answer (historic flat value 10; seeded for all 10 types). Unknown/unseeded types fall back to 10.
- **Streak milestones**: a one-off bonus when the daily streak first reaches `dayCount`. When the table is non-empty it is authoritative; when empty the historic ladder (7→50, 30→200) applies.

`ExerciseTypeRewardDto`: `{id, exerciseType, baseXpReward}`  
`StreakMilestoneDto`: `{id, dayCount, xpReward}`

### Reference Materials
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/reference | — | `AdminReferenceMaterialDto[]` (all, with optional `?skillId=&category=&search=`) |
| GET | /admin/reference/categories | — | `string[]` |
| GET | /admin/skills/:skillId/reference | — | `AdminReferenceMaterialDto[]` |
| POST | /admin/skills/:skillId/reference | `{title, markdownContent, sortOrder, category?, tags?}` | `AdminReferenceMaterialDto` |
| PUT | /admin/reference/:id | `{title, markdownContent, sortOrder, category?, tags?}` | `AdminReferenceMaterialDto` |
| DELETE | /admin/reference/:id | — | 204 |

`AdminReferenceMaterialDto`: `{id, skillId, skillTitle, skillSlug, title, markdownContent, sortOrder, category, tags: string[]}`

### Techniques
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/techniques | — (`?skill=&search=`) | `AdminTechniqueDto[]` |
| GET | /admin/techniques/:id | — | `AdminTechniqueDto` |
| POST | /admin/techniques | `AdminTechniqueWriteRequestDto` | `AdminTechniqueDto` (409 on slug conflict, 400 on unknown `primarySkillId` or out-of-range `difficulty`) |
| PUT | /admin/techniques/:id | `AdminTechniqueWriteRequestDto` | `AdminTechniqueDto` (replaces additional skills + coach) |
| DELETE | /admin/techniques/:id | — | 204 |
| POST | /admin/techniques/import | `AdminTechniqueWriteRequestDto[]` | `AdminTechniqueImportResultDto` — upserts by `slug` |
| GET | /admin/techniques/export | — | `AdminTechniqueWriteRequestDto[]` — all techniques, re-importable verbatim |

`skill` query param filters by `Skills.IconicName` (same convention as the public route).
`GET /admin/techniques/export` returns every technique (ignores `skill`/`search` filters) shaped exactly like the `import` request body, so an export file feeds straight back into `POST /admin/techniques/import`. UI: "Export JSON" button on `/admin/techniques`.

`AdminTechniqueDto`: `{id, slug, name, summary, body, tags: string[], primarySkillId?, primarySkillIconicName?, primarySkillTitle?, additionalSkillIds: Guid[], difficulty, difficultyName, sortOrder, createdAt, updatedAt, dialog?: JsonNode, case?: JsonNode, coach?: AdminTechniqueCoachDto}`

`AdminTechniqueCoachDto`: `{avatarSeed, name, role, quote, challenges?: JsonNode}`

`AdminTechniqueWriteRequestDto`: same shape minus `id`/timestamps and server-derived fields. `dialog`, `case`, and `coach.challenges` accept any JSON value — the server persists them to the `DialogJson` / `CaseJson` / `ChallengesJson` columns verbatim. `difficulty` must be 1..4.

`AdminTechniqueImportResultDto`: `{createdCount, updatedCount, failedCount, errors: string[]}` — import upserts each entry by `slug`, validates it, and rolls through the list, returning per-slug errors instead of aborting the whole batch.

### Leagues
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/leagues | — (`?weekStart=YYYY-MM-DD&tier=gold`) | `AdminLeagueListItemDto[]` |
| GET | /admin/leagues/weeks | — | `string[]` (distinct week start dates, desc) |
| GET | /admin/leagues/:id | — | `AdminLeagueDetailDto` |
| POST | /admin/leagues/close-current | — | 204 — manually runs the weekly closure job |
| POST | /admin/leagues/:id/resync | — | `AdminLeagueDetailDto` — recomputes weekly XP from `UserXpRecords` |
| PUT | /admin/leagues/memberships/:membershipId/tier | `{tier}` | `AdminLeagueDetailDto` of the target league (same week; created if missing) |
| PUT | /admin/leagues/memberships/:membershipId/xp | `{delta}` (non-zero int, may be negative) | `AdminLeagueDetailDto` |
| DELETE | /admin/leagues/memberships/:membershipId | — | 204 |
| GET | /admin/leagues/settings | — | `LeagueSettingsDto` (initializes the period on first access) |
| PUT | /admin/leagues/settings | `UpdateLeagueSettingsRequestDto` | `LeagueSettingsDto` (400 if values non-positive, zones exceed max, or period length ≤ 0) |
| GET | /admin/leagues/tiers | — | `AdminLeagueTierDto[]` (ordered by `order`) |
| POST | /admin/leagues/tiers | `{key, name, color, order}` | `AdminLeagueTierDto` (400 on blank fields or duplicate key) |
| PUT | /admin/leagues/tiers/:id | `{name, color, order}` | `AdminLeagueTierDto` (key is immutable; 404 if missing) |
| DELETE | /admin/leagues/tiers/:id | — | 204 (400 if it is the last tier or has existing leagues) |

`AdminLeagueListItemDto`: `{id, tier, weekStartDate, weekEndDate, memberCount}`
`AdminLeagueDetailDto`: `{id, tier, weekStartDate, weekEndDate, members: AdminLeagueMemberDto[]}`
`AdminLeagueMemberDto`: `{membershipId, userId, displayName, email, weeklyXpAmount, rank, promotionOutcome}`
`AdminLeagueTierDto`: `{id, key, name, color, order}`
`LeagueSettingsDto`: `{maximumLeagueParticipantCount, promotionZoneSize, demotionZoneSize, currentPeriodEndsAt, periodLengthDays}`
`UpdateLeagueSettingsRequestDto`: same as above but `currentPeriodEndsAt`/`periodLengthDays` are optional — when omitted the period is left unchanged, so zones can be edited alone. Setting `currentPeriodEndsAt` also realigns the active period's leagues' `WeekEndDate` so the XP window tracks the new end.

XP adjustment is recorded as a `UserXpRecords` row with `Source = "admin_correction"` and `EarnedAt` stamped at the league's week start — a direct `WeeklyXpAmount` write would be erased by the next XP sync, while a correction record survives every re-sync and stays auditable.

### Users (`RequireAdmin`; role change requires `RequireSuperAdmin`)

> Owned by the extracted **[identity-service](IDENTITY_SERVICE.md)** (it owns
> Users/Roles). The gateway flips `/admin/users/*` to the identity cluster; paths and
> shapes are unchanged. The `AdminUserDetailDto` activity stats (streak/XP/skills/score)
> are owned by gamification/learning, so identity returns them as `0` for now — same
> caveat as `GET /profile`.

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/users | — | `AdminUserDto[]` |
| GET | /admin/users/:id | — | `AdminUserDetailDto` (404 if missing) |
| PUT | /admin/users/:id | `{displayName}` | `AdminUserDto` (400 if name not 2–50 chars, 404 if missing) — moderation rename |
| DELETE | /admin/users/:id/avatar | — | 204 (404 if missing) — moderation: reset uploaded photo to default |
| PUT | /admin/users/:id/role | `{role: "User"\|"Admin"\|"SuperAdmin"}` | `AdminUserDto` (SuperAdmin only) |

`AdminUserDto`: `{id, email, displayName, role, createdAt, isEmailVerified, authProvider ("Google"|"Password"), hasCustomAvatar, avatarUrl}`
`AdminUserDetailDto`: `AdminUserDto` + `{currentStreakDayCount, longestStreakDayCount, totalXpAmount, completedSkillCount, totalSkillCount, averageExerciseScore, persona}`

Rename and avatar moderation are available to any admin (inappropriate nicknames/photos); role changes stay SuperAdmin-only. `DELETE /admin/users/:id/avatar` reuses the avatar reset flow (deletes the uploaded S3 object and falls back to the default avatar).

### Seeder
| Method | Path | Body | Response |
|---|---|---|---|
| POST | /admin/seeder/skills | `multipart/form-data; file=<JSON>` | `SkillsImportResultDto` |
| POST | /admin/seeder/topics | `multipart/form-data; file=<JSON>` | `TopicsImportResultDto` |
| POST | /admin/seeder/lessons | `multipart/form-data; file=<JSON>` | `LessonsImportResultDto` |
| POST | /admin/seeder/bundle | `multipart/form-data; file=<JSON>` (≤20 MB) | `BundleImportResultDto` |
| GET | /admin/seeder/skills/export | — | `SkillExportDto[]` — re-importable via POST /admin/seeder/skills |
| GET | /admin/seeder/topics/export | — | `TopicExportDto[]` — re-importable via POST /admin/seeder/topics |
| GET | /admin/seeder/lessons/export | — | `LessonExportDto[]` (with nested exercises) — re-importable via POST /admin/seeder/lessons |
| GET | /admin/seeder/bundle/export | — | `BundleExportDto` (`{ skills: [...] }`) — re-importable via POST /admin/seeder/bundle |

Each `GET …/export` returns the full content set shaped exactly like the matching import body, so an export file feeds straight back into its import (exercise `content` is emitted as a JSON object, not a string). Ordered by the relevant order field; skill/topic icon names are resolved from ids. UI: "Export JSON" buttons on `/admin/skills`, `/admin/topics`, `/admin/lessons`; "Export tree" on `/admin/import`.

**Skills JSON:** `[{ iconicName, title, description?, orderInTree, stage? }]`
**Topics JSON:** `[{ skillIconicName, iconicName, title, orderInSkill }]`
**Lessons JSON:** `[{ topicIconicName, title, orderInTopic, exercises: [{ type, orderInLesson, content, customAiPrompt? }] }]`
**Bundle JSON:** `{ skills: [{ iconicName, title, description?, orderInTree, stage?, topics: [{ iconicName, title, orderInSkill, lessons: [{ title, orderInTopic, exercises: [{ type, orderInLesson, content, customAiPrompt? }] }] }] }] }` (a bare skills array is also accepted). Whole skill tree in one file; idempotent upsert (skills/topics by `iconicName`, lessons by `(topicId, title)`, exercises by `(lessonId, orderInLesson)`); per-type content validation; invalid exercises are skipped into `errors[]`. UI: `/admin/import`.
`BundleImportResultDto = { skillsCreated, skillsUpdated, topicsCreated, topicsUpdated, lessonsCreated, lessonsUpdated, exercisesCreated, exercisesUpdated, errors[] }`

`SkillsImportResultDto`: `{skillsCreated, skillsUpdated, errors: string[]}`
`TopicsImportResultDto`: `{topicsCreated, topicsUpdated, errors: string[]}`
`LessonsImportResultDto`: `{lessonsCreated, lessonsUpdated, exercisesCreated, exercisesUpdated, errors: string[]}`

Max file size: 10 MB.


---

## Transcription

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /transcription/transcribe | `multipart/form-data; file=<audio>` | `TranscriptionResponseDto` |

`TranscriptionResponseDto`: `{text: string, language: string|null}`

**Supported formats:** mp3, mp4, m4a, mpeg, mpga, wav, webm, ogg  
**Max file size:** configurable via `Whisper:MaxFileSizeMb` (default 25 MB)  
**Model:** configurable via `Whisper:Model` (default `whisper-1`)  
**Language:** configurable via `Whisper:Language` (default `ru`)

Errors:
- `400` — file missing, too large, or unsupported format
- `502` — Whisper API returned an error

**Reused by Companies (Phase 39.15):** the call-log form's voice memo recorder
(`features/companies/hooks/use-voice-memo-recorder.ts`) posts the recorded `webm` blob to this
same endpoint via the existing `useTranscribeAudio` hook — no new endpoint, no company-service
involvement.

---

## Dialog (AI-powered conversation practice)

> **Microservices (Phase 6):** `/dialog/*`, `/transcription/*`, `/admin/dialog/*` and
> `/admin/voice/*` are served by the extracted **[ai-service](AI_SERVICE.md)** through the
> YARP gateway — paths unchanged. On `/complete` the service now emits a `dialog.evaluated`
> Kafka event (Gamification grants the XP) instead of writing `UserXpRecords` directly.
> Internal-only (not via the gateway): `POST /ai/evaluate` `{exerciseType, systemPrompt?,
> exerciseContent, userAnswer}` → `{isCorrect, score, explanation?, aiFeedback?}`, called
> by Learning to grade AI exercise types. `DialogBundleDto.skillTitle` is now empty
> (`Skills` are owned by Learning; only `skillId` is kept).
> Internal-only (Phase 39.12): `POST /ai/companies/briefing`
> `{companyDescription, goal?, recentCalls: [{contactName?, subject, outcome, occurredAt}],
> feedbackSummaries: string[]}` → `{content, generatedAt}` (`content` is markdown), called by
> company-service to generate the pre-call cheat sheet. Stateless — reads nothing from Mongo or
> Postgres itself, just composes a Russian system prompt from the request body and asks the
> configured LLM. `503` if OpenAI isn't configured or the provider call fails.
> Internal-only (Phase 39.13): `POST /ai/companies/parse-log` `{rawText}` (max 16000 chars, `400`
> if exceeded) → `{contactName?, subject, outcome, occurredAt?}`, called by company-service to
> extract a structured call-log draft from pasted notes/transcript. Stateless — composes a Russian
> system prompt instructing the model to return strict JSON, then parses it. `subject`/`outcome`
> default to an empty string if the model omits them; `contactName`/`occurredAt` are `null` if not
> mentioned or unparseable (never fails the whole parse just for a missing date). `503` if OpenAI
> isn't configured, the provider call fails, or the AI response isn't valid JSON. Both internal
> endpoints share `InternalServiceAuthFilter` (`X-Internal-Service-Secret` header, checked against
> `InternalAuth:ServiceSecret`; left open when that config key is unset, i.e. dev/single-service
> mode).
> Internal-only (Phase 39.14): `POST /ai/companies/persona` `{companyDescription (max 16000 chars,
> `400` if exceeded), contactName?, contactPosition?, difficulty: "Easy"|"Medium"|"Hard"}` →
> `{name, position, personality}`, called by company-service to invent a buyer persona for a
> practice call. Stateless — composes a Russian system prompt (difficulty tunes how
> tough/skeptical the persona is; `contactName`/`contactPosition` are an optional seed, not copied
> verbatim) instructing strict JSON output, then parses it. `503` if OpenAI isn't configured, the
> provider call fails, or any of `name`/`position`/`personality` is missing/empty in the response
> (unlike parse-log, personas have no valid "N/A" field — an incomplete persona is treated as a
> parse failure). Shares `InternalServiceAuthFilter` with the other internal endpoints.
> Internal-only (Phase 39.16): `POST /ai/companies/readiness` `{userId, goal?, sessionIds: string[]}`
> (max 50 session ids, `400` if exceeded) → `{score (0-100), strengths: string[], gaps: string[],
> recommendation}`, called by company-service to score a user's readiness for a real call. Unlike
> the other internal endpoints, this one **does** read from ai-service's own Mongo store: for each
> `sessionIds` entry it loads the `DialogSession` **scoped to `userId`** (via
> `IDialogService.GetSessionForUserAsync`, so ai-service independently verifies the caller-supplied
> ids belong to that user — defense in depth beyond `InternalServiceAuthFilter` + company-service's
> ownership check) and pulls `Feedback.Summary`, skipping sessions with no feedback yet (abandoned/incomplete calls).
> If zero sessions have usable feedback, returns **`204 No Content`** without calling the LLM — the
> "no data yet" signal company-service turns into its own `204`. Otherwise composes a Russian
> system prompt from the collected summaries (+ optional `goal`) instructing strict JSON output;
> `score` is clamped to `[0, 100]` after parsing (tolerates a numeric string too). `503` if OpenAI
> isn't configured, the provider call fails, or the response is unparseable/missing
> `score`/`recommendation` (`strengths`/`gaps` default to `[]` if omitted). Shares
> `InternalServiceAuthFilter` with the other internal endpoints.

### Public endpoints

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /dialog/bundles | — | `DialogBundleDto[]` (hidden bundles excluded) |
| GET | /dialog/bundles/:bundleId/modes | — | `DialogModeDto[]` |
| GET | /dialog/company-call-mode | — | `{bundleId, modeId}` — IDs of the seeded company-call mode; `404` if not yet seeded |
| GET | /dialog/sessions | — | `DialogSessionSummaryDto[]` (user's history) |
| POST | /dialog/sessions | `{bundleId, modeId, companyContext?}` | `DialogSessionDto` |
| GET | /dialog/sessions/:sessionId | — | `DialogSessionDto` |
| POST | /dialog/sessions/:sessionId/messages | `{content: string}` | `DialogMessageDto` |
| POST | /dialog/sessions/:sessionId/complete | — | `{summary, content, generatedAt, xpEarned}`; `204 No Content` when the session has no user messages (marked `abandoned`, no feedback generated) |

**Company context:** `companyContext` is optional on `POST /dialog/sessions`. Shape: `{companyName: string (required, ≤200), companyDescription: string (required, ≤8000), callGoal?: string (≤500), personaName?: string (≤200), personaPosition?: string (≤200), personaPersonality?: string (≤4000), personaDifficulty?: string (≤16, "Easy"|"Medium"|"Hard")}`. When present, the service appends a structured block to the mode's `ChatSystemPrompt` and `FeedbackSystemPrompt` at runtime (not stored in PostgreSQL — only persisted in the MongoDB `DialogSession` document as `companyCallContext`). The `GET /dialog/company-call-mode` endpoint returns the fixed `{bundleId, modeId}` that callers must pass when starting a company-practice session. **Constraint:** `companyContext` may only be used with the seeded company-call mode (key `company-call`); passing it with any other mode returns `400 Bad Request`.

**Persona role-play (Phase 39.14):** the four `persona*` fields are all optional/nullable — a call may have no persona (e.g. `personaName` absent or blank), in which case the prompt output is byte-for-byte identical to pre-39.14 behavior. When `personaName` is non-blank, `CompanyContextPromptBuilder` appends a second block to the chat system prompt instructing the model to **role-play as** that persona (name, position, personality, and a difficulty-derived toughness description), and a related but distinctly-worded "grade with this persona in mind" block to the feedback system prompt. See [AI_DIALOG.md](AI_DIALOG.md) for the full prompt shapes.

**DTOs:**
- `DialogBundleDto`: `{id, skillId, skillSlug, skillTitle, title, description, iconEmoji, sortOrder, isActive}`
- `DialogModeDto`: `{id, bundleId, key, title, description, sortOrder, isActive}`
- `DialogSessionDto`: `{id, bundleId, modeId, status, messages[], feedback?, xpEarned, createdAt, completedAt}`
- `DialogSessionSummaryDto`: `{id, bundleId, modeId, modeTitle, bundleTitle, status, messageCount, xpEarned, createdAt, completedAt}`
- `DialogMessageDto`: `{role: "assistant"|"user", content, timestamp, isStopSignal}`
- `DialogFeedbackDto`: `{content, generatedAt, xpEarned}`

**Session status:** `active` | `completed` | `abandoned`

**Stop signal:** AI adds `[DIALOG_END]` tag when conversation should end. Tag is parsed and `isStopSignal: true` set on message.

**XP reward:** AI generates XP (0-100) via `[XP:number]` tag in feedback. Saved to `UserXpRecords` with source `"dialog"`.

**Graceful degradation:**
- If `OpenAI:ApiKey` is not configured, `GET /dialog/bundles` returns `[]`
- Session endpoints return `503 Service Unavailable` if OpenAI not configured

### Admin endpoints (RequireAdmin)

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/dialog/bundles | — | `DialogBundleDto[]` |
| GET | /admin/dialog/bundles/:bundleId | — | `DialogBundleDto` |
| POST | /admin/dialog/bundles | `CreateBundleRequestDto` | `DialogBundleDto` |
| PUT | /admin/dialog/bundles/:bundleId | `UpdateBundleRequestDto` | `DialogBundleDto` |
| DELETE | /admin/dialog/bundles/:bundleId | — | 204 |
| GET | /admin/dialog/bundles/:bundleId/modes | — | `AdminDialogModeDto[]` |
| GET | /admin/dialog/modes/:modeId | — | `AdminDialogModeDto` |
| POST | /admin/dialog/bundles/:bundleId/modes | `CreateModeRequestDto` | `AdminDialogModeDto` |
| PUT | /admin/dialog/modes/:modeId | `UpdateModeRequestDto` | `AdminDialogModeDto` |
| DELETE | /admin/dialog/modes/:modeId | — | 204 |
| POST | /admin/dialog/import | `multipart/form-data; file=<JSON>` (≤20 MB) | `DialogImportResultDto` |
| GET | /admin/dialog/export | — | `DialogExportDto` — all bundles with nested modes, re-importable verbatim |

**Dialog export JSON:** `GET /admin/dialog/export` returns `{ bundles: [{ skillId, title, description, iconEmoji, sortOrder, isActive, modes: [{ key, title, description, chatSystemPrompt, feedbackSystemPrompt, sortOrder, isActive, voiceEnabled, voiceId }] }] }` — exactly the shape `POST /admin/dialog/import` accepts, so an export file re-imports verbatim. UI: "Export JSON" button on `/admin/dialog`.

**Dialog import JSON:** `{ bundles: [{ skillId, title, description?, iconEmoji?, sortOrder?, isActive?, modes: [{ key, title, description?, chatSystemPrompt?, feedbackSystemPrompt?, sortOrder?, isActive?, voiceEnabled?, voiceId? }] }] }` (a bare bundles array is also accepted). The endpoint keys bundles by `skillId` (a `Guid`) because the ai-service does not own the `Skills` table. Humans, however, paste `skillIconicName`: the `/admin/dialog` import panel resolves `skillIconicName → skillId` client-side (from `/admin/skills`) before upload, and a bundle that already has a `skillId` (e.g. a re-imported export) is left as-is — so both shapes work. Unknown `skillIconicName` is rejected client-side with nothing uploaded. Idempotent upsert: bundles by `(skillId, title)`, modes by `(bundleId, key)`; bundles with a missing/invalid `skillId` and modes with empty key/title are skipped into `errors[]`. UI: import panel on `/admin/dialog`.
`DialogImportResultDto = { bundlesCreated, bundlesUpdated, modesCreated, modesUpdated, errors[] }`

**Admin DTOs:**
- `AdminDialogModeDto`: extends `DialogModeDto` with `chatSystemPrompt, feedbackSystemPrompt`
- `CreateBundleRequestDto`: `{skillId, title, description, iconEmoji, sortOrder, isActive}`
- `UpdateBundleRequestDto`: all fields optional
- `CreateModeRequestDto`: `{key, title, description, chatSystemPrompt, feedbackSystemPrompt, sortOrder, isActive}`
- `UpdateModeRequestDto`: all fields optional

**Storage:**
- Bundles & Modes: PostgreSQL (`DialogBundles`, `DialogModes` tables, linked to `Skills`)
- Sessions: MongoDB (`dialog_sessions` collection)

**AI models:**
- Chat: `gpt-4.1-nano` (configurable via `OpenAI:ChatModel`)
- Feedback: `gpt-4.1` (configurable via `OpenAI:FeedbackModel`)

---

## Voice (Voice Roleplay)

### Public endpoints

| Method | Path | Body | Response |
|--------|------|------|----------|
| GET | /dialog/voice/config | — | `VoiceConfigDto` |
| GET | /dialog/voice/usage | — | `{dailyUsedSeconds, dailyLimitSeconds, dailyExceeded, monthlyUsedSeconds, monthlyLimitSeconds, monthlyExceeded}` |
| POST | /dialog/sessions/{sessionId}/voice/stream | `{transcript}` | `application/octet-stream` — length-prefixed frames (see below) |

### Admin endpoints

| Method | Path | Body | Response |
|--------|------|------|----------|
| GET | /admin/voice/usage | — | `AdminVoiceUsageDto` (RequireAdmin) |

```jsonc
// AdminVoiceUsageDto
{
  "dailyLimitSeconds": 600,
  "monthlyLimitSeconds": 7200,
  "users": [
    {
      "userId": "guid",
      "email": "user@example.com",
      "displayName": "User",
      "dailyUsedSeconds": 120,
      "monthlyUsedSeconds": 1800,
      "totalSeconds": 5400,
      "sessionCount": 12,
      "lastCallAt": "2026-06-05T10:00:00Z"
    }
  ]
}
// Sorted by monthlyUsedSeconds desc. Aggregated from MongoDB dialog sessions (voiceSeconds > 0).
```

**Voice stream frame format** (big-endian):
```
uint32 flags        // bit 0 = isFinal (sentinel, end of stream), bit 1 = isStopSignal (endCall)
uint32 textLength
byte[] text         // utf-8 sentence of the AI reply
uint32 audioLength
byte[] audio        // mp3 for that sentence
```
The final sentinel frame has empty text/audio and carries the `isStopSignal` flag.
`429` with `{period, usedSeconds, limitSeconds}` when the daily/monthly voice limit is exceeded.

> Removed (legacy, unused by frontend): `POST /dialog/sessions/{sessionId}/voice`,
> `GET /dialog/sessions/{sessionId}/voice/response`, Deepgram endpoints.

**VoiceConfigDto:**
```json
{
  "enabled": true,
  "vadSilenceMs": 600,
  "maxRecordingSeconds": 60,
  "deepgram": {
    "configured": true,
    "model": "nova-3",
    "language": "ru",
    "smartFormat": true,
    "punctuate": true
  }
}
```

**VoiceResponseDto:** `{content, isStopSignal, timestamp}`

**Voice endpoint behavior:**
- Accepts user transcript
- Generates AI response via GPT
- Synthesizes audio via ElevenLabs
- Returns audio stream (mp3)
- Saves both messages to MongoDB session

**Graceful degradation:**
- Returns 503 if Deepgram or ElevenLabs not configured
- `/dialog/voice/config` returns `enabled: false` if keys missing

### Admin voice fields

`AdminDialogModeDto` extended with:
- `voiceEnabled: boolean` — whether voice mode available for this mode
- `voiceId: string | null` — ElevenLabs voice ID override

`CreateModeRequestDto` / `UpdateModeRequestDto` accept:
- `voiceEnabled?: boolean`
- `voiceId?: string | null`

---

## Friends

> Served by the **social-service** (Phase 5) — the gateway flips `/friends/*` and
> `/chat/*` to the `social` cluster. Paths and DTO shapes are unchanged. The
> leaderboard/profile/activity XP-and-achievement aggregate fields currently return
> `0`/empty until Gamification/Learning are extracted (see [SOCIAL_SERVICE.md](SOCIAL_SERVICE.md)).

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /friends | — | `FriendDto[]` |
| GET | /friends/requests | — | `FriendRequestDto[]` |
| POST | /friends/requests | `{addresseeId}` | 201 `{friendshipId}` |
| PUT | /friends/requests/{friendshipId}/accept | — | 204 |
| PUT | /friends/requests/{friendshipId}/decline | — | 204 |
| DELETE | /friends/requests/{friendshipId} | — | 204 (requester cancels own pending request) |
| DELETE | /friends/{friendUserId} | — | 204 |
| GET | /friends/search?query={q} | — | `UserSearchResultDto[]` |
| GET | /friends/leaderboard | — | `FriendLeaderboardEntryDto[]` |
| GET | /friends/activity | — | `FriendActivityDto[]` (returns `[]` until Gamification/Learning emit activity events) |
| GET | /friends/profile/{userId} | — | `PublicProfileDto` |

`FriendDto`: `{userId, displayName, persona?, totalXpAmount, currentStreakDayCount, achievementCount, avatarUrl}`

`FriendRequestDto`: `{friendshipId, userId, displayName, persona?, direction, createdAt}`
- `direction`: `"incoming"` | `"outgoing"`

Request lifecycle: only the **addressee** may `accept`/`decline`; only the **requester** may `DELETE /friends/requests/{friendshipId}` to cancel a still-pending request. Decline keeps a `Declined` row (so the requester can later revive it by re-sending); cancel hard-deletes the row, returning the pair to the `none` state. Both `accept`/`decline`/`cancel` return `400` if the request is no longer pending and `404` if it does not exist; `cancel` returns `400` if the caller is not the requester. No event is emitted on decline or cancel.

`PublicProfileDto`: `{userId, displayName, persona?, totalXpAmount, currentStreakDayCount, achievementCount, averageExerciseScore, friendshipStatus, avatarUrl, friendshipId?}`
- `friendshipStatus`: `"none"` | `"pending_outgoing"` | `"pending_incoming"` | `"friends"`
- `friendshipId`: the underlying friendship row id when one exists (`null` for `"none"` / self). Lets the UI cancel an outgoing request directly from the "request sent" button without first fetching `/friends/requests`.

`UserSearchResultDto`: `{userId, displayName, persona?, friendshipStatus, avatarUrl, friendshipId?}` (`friendshipId` as above)

`FriendLeaderboardEntryDto`: `{userId, displayName, totalXpAmount, rank, isCurrentUser, avatarUrl}`

`FriendActivityDto`: `{userId, displayName, activityType, description, occurredAt}`
- `activityType`: `"earned_achievement"` | `"earned_xp"` | `"completed_lesson"` | `"streak_milestone"`

---

## Chat

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /chat/conversations | — | `ChatConversationSummaryDto[]` |
| POST | /chat/conversations | `{friendUserId}` | `ChatConversationSummaryDto` |
| GET | /chat/conversations/{id}/messages?limit=50&before={msgId} | — | `ChatMessageDto[]` |
| POST | /chat/conversations/{id}/messages | `{content}` | `ChatMessageDto` |
| POST | /chat/conversations/{id}/read | — | `204 No Content` |

`POST /chat/conversations/{id}/read` records the caller's read watermark on the conversation
(`lastReadAt[userId]`) and publishes `chat.message.read`, which cancels any pending
unread-message email for that conversation (see [EMAIL_NOTIFICATIONS.md](EMAIL_NOTIFICATIONS.md)).

`ChatConversationSummaryDto`: `{conversationId, friendUserId, friendDisplayName, lastMessagePreview?, lastMessageAt?}`

`ChatMessageDto`: `{id, senderId, content, sentAt, isOwn}`

**Business rules:**
- Chat only available between accepted friends
- Creating a conversation validates active friendship
- Messages are stored in MongoDB `chat_conversations` collection
- Participant IDs are always sorted for canonical document identity

---

## Notifications

> **Served by `notification-service` (Phase 4)** through the gateway — the paths and
> contracts below are unchanged so the frontend is unaffected. Storage is Redis
> (per-user capped list + unread counter), not PostgreSQL. See
> [NOTIFICATION_SERVICE.md](NOTIFICATION_SERVICE.md).

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /notifications?limit=20&includeRead=true | — | `NotificationDto[]` |
| GET | /notifications/unread-count | — | `UnreadNotificationCountDto` |
| PUT | /notifications/{notificationId}/read | — | 204 |
| PUT | /notifications/read-all | — | 204 |

`NotificationDto`: `{id, notificationType, title, body, actionUrl?, relatedEntityId?, isRead, createdAt, readAt?}`
- `notificationType`: `"FriendRequestReceived"` | `"FriendRequestAccepted"` | `"ChatMessageReceived"` | `"AchievementUnlocked"` | `"StreakMilestone"`

`UnreadNotificationCountDto`: `{count}`

**Business rules:**
- Notifications are scoped to the authenticated recipient
- `actionUrl` is a relative frontend route (e.g. `/friends?tab=requests`, `/friends/chat/{conversationId}`, `/profile`)
- `relatedEntityId` stores source-entity id as string (friendship id, conversation id, achievement key, etc.)
- Marking a single notification as read is idempotent; already-read notifications return 204
- Retention is a **30-day Redis TTL** per inbox (replaces the monolith's `notification-cleanup` Hangfire job); inboxes are capped (default 100 per user)
- Triggers arrive as Kafka events the service consumes (`achievement.unlocked`, `streak.milestone`, `friend.request.received`, `friend.request.accepted`, `chat.message.sent`)

---

## Discuss (community forum)

> Served by the **social-service** (Phase 5) — the gateway flips `/discuss/*` and
> `/admin/discuss/*` to the `social` cluster. Paths and DTO shapes are unchanged; the
> tables move to the `social` Postgres database and photos stay on S3/MinIO
> (see [SOCIAL_SERVICE.md](SOCIAL_SERVICE.md)).

All endpoints require auth. Threads, replies and votes are PostgreSQL; votes are upvote-only
(a row's existence = upvoted), de-duplicated by a unique `(userId, targetType, targetId)` index.

### User endpoints

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /discuss/threads?sort=&search=&tag=&page=&pageSize= | — | `PagedResult<DiscussThreadSummaryDto>` |
| GET | /discuss/threads/{threadId} | — | `DiscussThreadDetailDto` (also increments view count) |
| POST | /discuss/threads | `{title, body, tags: string[]}` | `DiscussThreadDetailDto` (201) |
| POST | /discuss/threads/{threadId}/replies | `{body}` | `DiscussReplyDto` (201) |
| POST | /discuss/threads/{threadId}/upvote | — | `VoteResultDto` |
| DELETE | /discuss/threads/{threadId}/upvote | — | `VoteResultDto` |
| POST | /discuss/replies/{replyId}/upvote | — | `VoteResultDto` |
| DELETE | /discuss/replies/{replyId}/upvote | — | `VoteResultDto` |
| POST | /discuss/threads/{threadId}/accepted-reply | `{replyId}` | `DiscussThreadDetailDto` (author or admin; else 403) |
| DELETE | /discuss/threads/{threadId}/accepted-reply | — | `DiscussThreadDetailDto` (clears solved) |
| GET | /discuss/tags?curatedOnly= | — | `DiscussTagDto[]` |
| GET | /discuss/tags/popular?limit= | — | `PopularTagDto[]` |
| GET | /discuss/stats | — | `DiscussStatsDto` |

`sort`: `hot` (default; pinned first, then time-decayed score, manual `isHot` boosts) | `new` (by lastActivityAt) | `unanswered` (zero-reply only).

- `DiscussThreadSummaryDto`: `{id, title, bodyPreview, authorId, authorName, authorAvatarUrl, upvoteCount, replyCount, viewCount, isPinned, isHot, isSolved, tags: [{slug, name}], createdAt, lastActivityAt, viewerHasUpvoted}`
- `DiscussThreadDetailDto`: summary fields + `{body, acceptedReplyId, replies: DiscussReplyDto[]}`
- `DiscussReplyDto`: `{id, threadId, authorId, authorName, authorAvatarUrl, body, upvoteCount, isAccepted, createdAt, viewerHasUpvoted}`
- `DiscussTagDto`: `{id, slug, name, isCurated}`
- `PopularTagDto`: `{slug, name, threadCount}`
- `DiscussStatsDto`: `{totalThreads, totalReplies, topAuthorsOfWeek: [{authorId, authorName, authorAvatarUrl, upvotesReceived}]}` (upvotes received on the author's threads+replies in the last 7 days)
- `VoteResultDto`: `{upvoteCount, hasUpvoted}`
- `PagedResult<T>`: `{items: T[], page, pageSize, totalCount}`

Tags are hybrid: a thread's `tags` array mixes existing curated/free slugs and brand-new labels;
unknown labels are created on the fly as non-curated tags (slug = lowercased, whitespace→`-`).

### Admin endpoints (`RequireAdmin`)

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/discuss/threads?search=&page=&pageSize= | — | `PagedResult<DiscussThreadSummaryDto>` |
| DELETE | /admin/discuss/threads/{threadId} | — | 204 (cascades replies, thread-tags, votes) |
| POST | /admin/discuss/threads/{threadId}/pin | `{isPinned}` | `DiscussThreadSummaryDto` |
| POST | /admin/discuss/threads/{threadId}/hot | `{isHot}` | `DiscussThreadSummaryDto` |
| DELETE | /admin/discuss/replies/{replyId} | — | 204 (clears accepted-reply if it pointed here, decrements count) |
| GET | /admin/discuss/tags | — | `DiscussTagDto[]` |
| POST | /admin/discuss/tags | `{name, slug?}` | `DiscussTagDto` (201; 409 on duplicate slug) |
| PUT | /admin/discuss/tags/{tagId} | `{name?, slug?}` | `DiscussTagDto` (409 on duplicate slug) |
| DELETE | /admin/discuss/tags/{tagId} | — | 204 (cascades thread-tags) |

### Photos

Photos (up to 10) attach to a thread or a reply via a two-step flow: create the thread/reply
with the existing JSON endpoints above, then upload images to its photo sub-resource. Stored in
S3/MinIO (key prefix `discuss/`) + the `DiscussPhotos` table. Bucket: `salestrainer-avatars` in the monolith, `sellevate-social` in the extracted social-service.
All require auth except the content GET.

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /discuss/threads/{threadId}/photos | `multipart/form-data`, field `files` (1..N images) | `200 DiscussPhotoListDto` |
| POST | /discuss/replies/{replyId}/photos | `multipart/form-data`, field `files` (1..N images) | `200 DiscussPhotoListDto` |
| DELETE | /discuss/photos/{photoId} | — | 204 (author only; else 403) |
| GET | /discuss/photos/{photoId}/content `[public]` | — | `200` image bytes |

- Upload errors: `400` (no files / >10 total / unsupported type / file >5 MB), `403` (not the author), `404` (thread/reply missing).
- `GET /discuss/photos/{photoId}/content` returns the image bytes with `Content-Type` from the stored value, `Cache-Control: public, max-age=60`, and `X-Content-Type-Options: nosniff`; `404` if missing.
- Allowed types: PNG / JPEG / WEBP (magic-byte validated). Per-file max 5 MB. Max 10 photos per owner (service-enforced).
- Photo `url` is the relative path `/discuss/photos/{id}/content`.
- Deleting a thread or reply (including admin delete) removes its photo rows and best-effort-deletes the S3 objects.

- `DiscussPhotoListDto`: `{photos: DiscussPhotoDto[]}`
- `DiscussPhotoDto`: `{id, url, orderIndex}`

DTO additions on the Discuss user endpoints above:
- `DiscussThreadDetailDto` gains `photos: DiscussPhotoDto[]`
- `DiscussReplyDto` gains `photos: DiscussPhotoDto[]`
- `DiscussThreadSummaryDto` gains `photoCount: number` and `firstPhotoUrl: string | null`

---

## Avatars

| Method | Path | Auth | Body | Response |
|---|---|---|---|---|
| POST | /avatars | Bearer | `multipart/form-data` with `file` field (PNG/JPG/JPEG/WEBP, max 5 MB) | `200 { "avatarUrl": "/avatars/{userId}" }` |
| DELETE | /avatars | Bearer | — | 204 |
| GET | /avatars/{userId:guid} `[public]` | — | — | `200` image bytes with `Content-Type: image/png\|jpeg\|webp`; `404` if user or avatar object not found |

- `POST /avatars` stores the image in S3 under `users/{userId}/avatar{ext}` and sets `AvatarType = Uploaded` on the user row.
- The S3/MinIO bucket (`Storage:S3:Bucket`) is created at startup via `IObjectStorage.EnsureBucketExistsAsync()` (best-effort, before default-avatar seeding). Without this the bucket never exists in fresh MinIO and `POST /avatars` fails with HTTP 500 (`NoSuchBucket`).
- `DELETE /avatars` best-effort deletes the uploaded object from S3, then resets `AvatarType = Default`, `AvatarKey = null`.
- `GET /avatars/{userId}` returns the uploaded object if `AvatarType == Uploaded`, otherwise the `DefaultAvatars` row matching `user.DefaultAvatarIndex`. Returns `404` if the user/avatar object cannot be resolved (so the client falls back to the generated avatar instead of a 500).
- `GET /avatars/{userId}` uses **validation-based caching**: it returns the object's `ETag` and `Cache-Control: public, no-cache` (clients cache but must revalidate every load). A matching `If-None-Match` yields `304 Not Modified` with no body. This makes a freshly uploaded avatar appear immediately after a page refresh (and in the nav bar) while unchanged images cost only a 304 round-trip. Do **not** restore a long `max-age` here — it reintroduces the stale-avatar-after-refresh bug.
- Subtask 5 will expose `avatarUrl` (value: `/avatars/{userId}`) on profile/user DTOs throughout the API.

---

## Company service (Phase 39)

> **New microservice `company-service`** (host port **5009**). Routes `/companies/*` via YARP gateway cluster `company` (wired in Phase 39.4). All endpoints require Bearer auth; `userId` extracted from `ClaimTypes.NameIdentifier`. Every query is scoped to the authenticated user — foreign/unknown ids return `404`.

### Companies

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /companies | `?search=` (optional) | `CompanySummaryDto[]` sorted newest-updated first |
| POST | /companies | `{name, description?}` | `201 CompanyDetailDto` |
| GET | /companies/{id} | — | `CompanyDetailDto` or `404` |
| PUT | /companies/{id} | `{name, description}` | `CompanyDetailDto` or `404` |
| PUT | /companies/{id}/status | `{status}` | `CompanyDetailDto` or `404` |
| PUT | /companies/{id}/follow-up | `{nextActionAt, nextActionNote?}` | `CompanyDetailDto` or `404` |
| POST | /companies/{id}/briefing | — | `CompanyBriefingDto`, `404`, or `503` if ai-service is unavailable |
| GET | /companies/{id}/briefing | — | `CompanyBriefingDto` or `204` if never generated, or `404` |
| GET | /companies/{id}/readiness | — | `CompanyReadinessDto`, `204` if no data yet, `404`, or `503` if ai-service is unavailable |
| DELETE | /companies/{id} | — | `204` or `404` (cascade-deletes logs + practice calls + contacts + personas) |

`CompanySummaryDto`: `{id, name, descriptionExcerpt (≤160 chars), status, callLogCount, practiceCallCount, contactCount, nextActionAt, createdAt, updatedAt}`
`CompanyDetailDto`: `{id, name, description, status, callLogCount, practiceCallCount, contactCount, nextActionAt, nextActionNote, followUpNotifiedAt, createdAt, updatedAt}`

Validation: `name` required, max 200; `description` max 8000.

`status` (Phase 39.10) is one of `Lead | Contacted | MeetingScheduled | DealWon | DealLost`
(string enum), defaulting to `Lead` on creation. `PUT /companies/{id}/status` sets it directly —
no server-side transition constraints, any status may be set from any other. `404` on missing
company or wrong owner, same ownership pattern as every other company endpoint.

`nextActionAt`/`nextActionNote`/`followUpNotifiedAt` (Phase 39.11 — follow-up reminders):
`PUT /companies/{id}/follow-up` with a non-null `nextActionAt` (re)schedules the follow-up
(`nextActionNote` optional, max 2000 chars, defaults to empty) and **resets
`followUpNotifiedAt` to `null`**, so a rescheduled due date is eligible to notify again even if
the previous one already fired. A request with `nextActionAt: null` **clears** the follow-up —
`nextActionNote` and `followUpNotifiedAt` are cleared with it. `followUpNotifiedAt` is
read-only/server-managed (set by the reminder background service, see below) and is exposed on
`CompanyDetailDto` for observability, not on the list DTO. `nextActionAt` is included on
`CompanySummaryDto` so the `/companies` list can render a due/overdue badge per row without an
extra request per company.

**Follow-up reminder background service (Kafka producer):** `company-service` runs a hosted
background service (`FollowUpReminderBackgroundService`) that polls every
`FollowUpReminder:PollIntervalMinutes` (default 5) for companies where `NextActionAt <= now AND
FollowUpNotifiedAt IS NULL`, claims them (sets `FollowUpNotifiedAt`, commits), and publishes one
`company.followup.due` Kafka event per claimed company — see `docs/MICROSERVICES.md §4.1` for the
topic/payload and `docs/ARCHITECTURE.md` for the claim-before-publish trade-off. Consumed by
notification-service → `NotificationType.CompanyFollowUpDue`, an in-app-only notification (no
email) titled *«Пора связаться с {companyName}»*, `actionUrl` `/companies/{id}`.

**Pre-call briefing / "Шпаргалка" (Phase 39.12):** `POST /companies/{id}/briefing` gathers
context — the company's `description`, the most recent non-empty `PracticeCall.Goal` (single
newest, not the last-5-distinct list used by `/recent-goals`), and the last 5 `CallLogEntry` rows
(newest first) — and forwards it to ai-service's internal `POST /ai/companies/briefing` (see
below), which returns a markdown cheat sheet. company-service caches the result on
`Company.BriefingContent`/`BriefingGeneratedAt` and returns it. `GET /companies/{id}/briefing`
returns the cached value without calling ai-service; `204` if the company exists but a briefing
has never been generated. Both endpoints follow the same ownership/`404` pattern as every other
company endpoint; `POST` returns `503` if ai-service is unreachable or misconfigured (mirrors the
Evaluation feature's error handling). **Feedback summaries are not included** — company-service
has no cross-service read into ai-service's Mongo feedback store (out of scope for 39.12), so the
`feedbackSummaries` list sent to ai-service is always empty; the briefing prompt degrades
gracefully (skips that section's data) when empty.

`CompanyBriefingDto`: `{content, generatedAt}` — both `null` when never generated.

**Readiness score (Phase 39.16):** `GET /companies/{id}/readiness` **self-generates and caches** —
unlike briefing, there is no separate `POST`. On a cache miss it gathers the company's practice-call
`DialogSessionId`s (newest first, capped to 50 — mirrors ai-service's own cap) and the single most
recent non-empty `PracticeCall.Goal`, and forwards them to ai-service's internal
`POST /ai/companies/readiness` (see above). The result is cached on
`Company.ReadinessJson`/`ReadinessGeneratedAt` and returned; a subsequent `GET` returns the cache
without calling ai-service again. Two distinct "no data" cases both collapse to `204`, with all
`CompanyReadinessDto` fields `null`, but they are **not** treated identically for caching: (1) the
company has no practice calls yet — the ai-service call is skipped entirely and **nothing is
cached** (every `GET` re-checks cheaply, since there's no fan-out to avoid); (2) the company has
practice calls but ai-service signalled `204` (none of them have usable feedback yet, e.g. sessions
still in progress or abandoned) — this **is negative-cached** on `Company.ReadinessNoFeedbackUntil`
for a short TTL (2 minutes) so repeated requests within the TTL short-circuit to the empty result
instead of re-running the fan-out (up to 50 sequential `DialogSessionId` lookups against
ai-service/Mongo) on every request (PR #26 review fast-follow, 39.17). `404` on missing
company/wrong owner; `503` if ai-service is unreachable, misconfigured, or returns an
unparseable/incomplete response (same pattern as briefing/persona) — a failure of this kind is
**never** cached (positive or negative), so the next `GET` retries against ai-service instead of
being stuck behind a stale/incorrect cache entry.

**Cache invalidation:** creating a practice call (`POST /companies/{id}/practice-calls`) is this
codebase's practice-completion signal (dialog-session completion itself is tracked only in
ai-service's Mongo, not in company-service) — it clears `ReadinessJson`/`ReadinessGeneratedAt`
**and** `ReadinessNoFeedbackUntil` on the company so the next `GET /readiness` regenerates from the
fresh session list instead of being held back by a stale negative cache. There is no other path in
company-service that marks a practice call complete.

`CompanyReadinessDto`: `{score, strengths, gaps, recommendation, generatedAt}` — all fields `null`
when there's no data yet (see above).

### Call Log

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /companies/{id}/logs | — | `CallLogEntryDto[]` sorted by `occurredAt DESC` |
| POST | /companies/{id}/logs | `{contactName, subject, outcome, occurredAt, contactId?}` | `201 CallLogEntryDto`, `404` if company not found, or `400` if `contactId` does not belong to the company |
| PUT | /companies/{id}/logs/{logId} | `{contactName, subject, outcome, occurredAt, contactId?}` | `CallLogEntryDto`, `404`, or `400` if `contactId` does not belong to the company |
| DELETE | /companies/{id}/logs/{logId} | — | `204` or `404` |
| POST | /companies/{id}/logs/parse | `{rawText}` | `ParsedCallLogDto`, `404`, or `503` if ai-service is unavailable |

**AI log parsing / "Вставить заметки" (Phase 39.13):** `POST /companies/{id}/logs/parse` proxies
`rawText` (pasted notes/transcript) to ai-service's internal `POST /ai/companies/parse-log` (see
above) and returns the extracted draft **without persisting anything** — the client prefills the
existing log-create form for the user to review/edit, then saves it through the normal
`POST /companies/{id}/logs`. Same ownership/`404` pattern as every other company endpoint; `503`
if ai-service is unreachable, misconfigured, or returns an unparseable response.

`ParsedCallLogDto`: `{contactName: string|null, subject, outcome, occurredAt: DateTime|null}`.

`CallLogEntryDto`: `{id, companyId, contactName, subject, outcome, occurredAt, createdAt, updatedAt, contactId}`

Validation: `contactName` required, max 200; `subject`, `outcome` optional (empty string allowed), max 4000. `contactId` is optional; when present it must reference a `CompanyContact` belonging to the same company (otherwise `400`). The free-text `contactName` is always stored regardless of `contactId`, so the log keeps a readable label even after the linked contact is deleted (see Contacts below).

**`400` on a bad `contactId` (39.17 hardening):** company-service raises a typed
`ContactNotFoundInCompanyException` — not a generic `InvalidOperationException` — both when the
ownership check fails up front and when a concurrently-deleted contact trips the `ContactId`
foreign key at `SaveChangesAsync` time (the check-then-act race between the ownership check and the
save; the FK-violation `DbUpdateException` is only translated when it's specifically a Postgres
`23503` on the `FK_CallLogEntries_CompanyContacts_ContactId` constraint — any other
`DbUpdateException` propagates unchanged as a `500`). Both cases map to the same
`400 { code: "CONTACT_NOT_FOUND", message }` response, where `code` is a machine-readable
discriminator distinguishing this from other `400`s on the same endpoints (e.g. ASP.NET
model-validation failures on `contactName`/`subject`/`outcome` length, which have no `code` field).
The frontend only clears the stale `contactId` from the call-log form when it sees
`code === "CONTACT_NOT_FOUND"`, so retrying resubmits as free text instead of repeating the same
failing request, while other `400`s leave the form untouched.

### Practice Calls

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /companies/{id}/practice-calls | `{dialogSessionId, goal?}` | `201 PracticeCallDto` or `404` |
| GET | /companies/{id}/practice-calls | — | `PracticeCallDto[]` sorted by `createdAt DESC` |
| GET | /companies/{id}/recent-goals | — | `string[]` — last 5 distinct non-empty goals, newest first |

`PracticeCallDto`: `{id, companyId, dialogSessionId, goal, createdAt}`

`goal` is **optional** (`≤1000`); when omitted/empty it is stored as `""` and excluded from
recent-goals. The client records the practice call only once the session **completes and
feedback is formed** (on hang-up / stop-signal), not at call start — so an abandoned session
leaves no practice-call record.

Validation: `goal` max 1000; `dialogSessionId` required.

### Contacts (Phase 39.9 — mini-CRM)

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /companies/{id}/contacts | — | `CompanyContactDto[]` sorted by `createdAt DESC` |
| POST | /companies/{id}/contacts | `{name, position?, notes?}` | `201 CompanyContactDto` or `404` if company not found |
| PUT | /companies/{id}/contacts/{contactId} | `{name, position?, notes?}` | `CompanyContactDto` or `404` |
| DELETE | /companies/{id}/contacts/{contactId} | — | `204` or `404`. Any `CallLogEntry.ContactId` referencing this contact is set to `null`; the log's free-text `ContactName` is preserved. |

`CompanyContactDto`: `{id, companyId, name, position, notes, createdAt, updatedAt}`

Validation: `name` required, max 200; `position` optional (nullable, defaults to empty), max 200; `notes` optional (nullable, defaults to empty), max 2000. Create and Update use the same nullability for `position`/`notes` (39.17 hardening — they previously diverged: Update declared them as non-nullable with an empty-string default instead of nullable).

### Personas (Phase 39.14 — AI persona generation for practice calls)

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /companies/{id}/personas | — | `CompanyPersonaDto[]` sorted by `createdAt DESC` |
| POST | /companies/{id}/personas | `{name, position, personality, difficulty}` | `201 CompanyPersonaDto` or `404` |
| DELETE | /companies/{id}/personas/{personaId} | — | `204` or `404` |
| POST | /companies/{id}/personas/generate | `{contactName?, contactPosition?, difficulty}` | `GeneratedCompanyPersonaDto`, `404`, or `503` if ai-service is unavailable |

`CompanyPersonaDto`: `{id, companyId, name, position, personality, difficulty, createdAt}`
`GeneratedCompanyPersonaDto`: `{name, position, personality}` — not persisted.

Validation: `name` required, max 200; `position` required, max 200; `personality` required, max 4000; `difficulty` one of `Easy | Medium | Hard` (string enum, same conversion pattern as `Company.Status`).

**Generate-then-save flow:** `POST /companies/{id}/personas/generate` gathers the company's
`description` and forwards it — plus the optional `contactName`/`contactPosition` seed and the
requested `difficulty` — to ai-service's internal `POST /ai/companies/persona` (see above), and
returns the draft `{name, position, personality}` **without persisting anything**, so the caller
can regenerate before committing. Saving is a separate `POST /companies/{id}/personas` call with
the (possibly edited) draft plus the chosen `difficulty`. The roadmap's "seeded from an existing
contact" note is purely a frontend UX affordance (prefilling `contactName`/`contactPosition` from
a contact the user picks) — there is no backend coupling between `CompanyContact` and
`CompanyPersona`. Same ownership/`404` pattern as every other company endpoint; `503` if
ai-service is unreachable, misconfigured, or returns an unparseable/incomplete response.

**Injection into practice calls:** the frontend's persona selector (chips + «Без персоны» +
generate) lets the caller pick a saved `CompanyPersona` (or none) before starting a voice/chat
practice call; the selected persona's `name`/`position`/`personality`/`difficulty` are sent as the
`persona*` fields of `companyContext` on `POST /dialog/sessions` (see the Dialog section above).

---

## Tracking / Usage Metrics

> **Served by the analytics-service** (Phase 1). The gateway routes `/tracking/*` to the
> analytics cluster; the monolith's `MetricsController` is left in place as reference but no
> longer receives this traffic. Frontend paths are unchanged. See
> [ANALYTICS_SERVICE.md](ANALYTICS_SERVICE.md).

| Method | Path | Auth | Body | Response |
|---|---|---|---|---|
| POST | /tracking/events | Bearer | `{event, page}` | `204` on success; `400` on unknown event/page; `401` if unauthenticated |
| POST | /tracking/presence/ping | Bearer | _(none)_ | `204` on success; `401` if no resolvable user identity |

- `/tracking/events` feeds the Prometheus counters `app_page_views_total` / `app_events_total`. `event="page_view"` is recorded as a page view (uses only `page`); any other event uses both labels.
- `event` and `page` are validated against a **server-side whitelist** (`analytics-service/Analytics/Features/Tracking/Constants/TrackedEvents.cs`) to cap label cardinality — unknown values are rejected with `400`, never silently accepted.
- `/tracking/presence/ping` marks the caller present (Redis sorted set) and bumps `app_authenticated_requests_total`. Identity is taken from the gateway-injected `X-User-Id` header, falling back to the validated JWT subject.
- All product metrics are scraped from the `/metrics` endpoint (jobs `sallevate-backend` + `sellevate-analytics`); there is no read API for them — query them in Prometheus/Grafana. See [MONITORING.md](MONITORING.md).
