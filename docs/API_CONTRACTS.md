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

`LessonSummaryDto`: `{lessonId, title, orderInTopic, status, bestScore, kind}` where `kind` is `"theory"` (every exercise is a `theory_card`) or `"practice"`. Theory lessons are played as swipeable cards; the client submits the last card once to complete them.

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

| Method | Path | Response |
|---|---|---|
| GET | /profile | `UserProfileStatsDto` |
| GET | /profile/achievements | `AchievementDto[]` |
| PUT | /profile/persona | `{persona: string}` → 204 |

`UserProfileStatsDto`: `{displayName, email, currentStreakDayCount, longestStreakDayCount, totalXpAmount, completedSkillCount, totalSkillCount, averageExerciseScore, persona?, avatarUrl}`

`persona` values: `sdr` | `account_executive` | `account_manager` | `founder` | `other`

`AchievementDto`: `{achievementId, key, title, description, iconEmoji, isUnlocked, unlockedAt}`

Achievement condition types: `first_lesson` | `lesson_count` | `xp_total` | `streak_days` | `skill_completed`

`ExerciseSubmissionResultDto` now includes `newlyUnlockedAchievementKeys: string[]` — keys of achievements unlocked in this submit.

---

## League

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

`skill` query param filters by `Skills.IconicName` (same convention as the public route).

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

---

## Dialog (AI-powered conversation practice)

### Public endpoints

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /dialog/bundles | — | `DialogBundleDto[]` |
| GET | /dialog/bundles/:bundleId/modes | — | `DialogModeDto[]` |
| GET | /dialog/sessions | — | `DialogSessionSummaryDto[]` (user's history) |
| POST | /dialog/sessions | `{bundleId, modeId}` | `DialogSessionDto` |
| GET | /dialog/sessions/:sessionId | — | `DialogSessionDto` |
| POST | /dialog/sessions/:sessionId/messages | `{content: string}` | `DialogMessageDto` |
| POST | /dialog/sessions/:sessionId/complete | — | `{summary, content, generatedAt, xpEarned}`; `204 No Content` when the session has no user messages (marked `abandoned`, no feedback generated) |

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

**Dialog import JSON:** `{ bundles: [{ skillIconicName, title, description?, iconEmoji?, sortOrder?, isActive?, modes: [{ key, title, description?, chatSystemPrompt?, feedbackSystemPrompt?, sortOrder?, isActive?, voiceEnabled?, voiceId? }] }] }` (a bare bundles array is also accepted). `skillIconicName` must already exist. Idempotent upsert: bundles by `(skillId, title)`, modes by `(bundleId, key)`; modes with an unknown skill or empty key/title are skipped into `errors[]`. UI: import panel on `/admin/dialog`.
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

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /friends | — | `FriendDto[]` |
| GET | /friends/requests | — | `FriendRequestDto[]` |
| POST | /friends/requests | `{addresseeId}` | 201 `{friendshipId}` |
| PUT | /friends/requests/{friendshipId}/accept | — | 204 |
| PUT | /friends/requests/{friendshipId}/decline | — | 204 |
| DELETE | /friends/{friendUserId} | — | 204 |
| GET | /friends/search?query={q} | — | `UserSearchResultDto[]` |
| GET | /friends/leaderboard | — | `FriendLeaderboardEntryDto[]` |
| GET | /friends/activity | — | `FriendActivityDto[]` |
| GET | /friends/profile/{userId} | — | `PublicProfileDto` |

`FriendDto`: `{userId, displayName, persona?, totalXpAmount, currentStreakDayCount, achievementCount, avatarUrl}`

`FriendRequestDto`: `{friendshipId, userId, displayName, persona?, direction, createdAt}`
- `direction`: `"incoming"` | `"outgoing"`

`PublicProfileDto`: `{userId, displayName, persona?, totalXpAmount, currentStreakDayCount, achievementCount, averageExerciseScore, friendshipStatus, avatarUrl}`
- `friendshipStatus`: `"none"` | `"pending_outgoing"` | `"pending_incoming"` | `"friends"`

`UserSearchResultDto`: `{userId, displayName, persona?, friendshipStatus, avatarUrl}`

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

`ChatConversationSummaryDto`: `{conversationId, friendUserId, friendDisplayName, lastMessagePreview?, lastMessageAt?}`

`ChatMessageDto`: `{id, senderId, content, sentAt, isOwn}`

**Business rules:**
- Chat only available between accepted friends
- Creating a conversation validates active friendship
- Messages are stored in MongoDB `chat_conversations` collection
- Participant IDs are always sorted for canonical document identity

---

## Notifications

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
- Hangfire recurring job `notification-cleanup` deletes read notifications older than 30 days (daily at 00:30 UTC)

---

## Discuss (community forum)

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
S3/MinIO (bucket `salestrainer-avatars`, key prefix `discuss/`) + the `DiscussPhotos` table.
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

## Tracking / Usage Metrics

| Method | Path | Auth | Body | Response |
|---|---|---|---|---|
| POST | /tracking/events | Bearer | `{event, page}` | `204` on success; `400` on unknown event/page; `401` if unauthenticated |

- Feeds the Prometheus counters `app_page_views_total` / `app_events_total`. `event="page_view"` is recorded as a page view (uses only `page`); any other event uses both labels.
- `event` and `page` are validated against a **server-side whitelist** (`Features/Metrics/Constants/TrackedEvents.cs`) to cap label cardinality — unknown values are rejected with `400`, never silently accepted.
- All product metrics are scraped from the existing `/metrics` endpoint (job `sallevate-backend`); there is no read API for them — query them in Prometheus/Grafana. See [MONITORING.md](MONITORING.md).
