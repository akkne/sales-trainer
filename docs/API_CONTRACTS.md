# API_CONTRACTS.md

Base URL: `http://localhost:5000` (dev) | `http://backend:8080` (docker internal)

All endpoints except those marked `[public]` require `Authorization: Bearer <accessToken>`.

---

## Auth `[public]`

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /auth/register | `{email, password, displayName}` | `AuthTokenResponseDto` + cookie `refreshToken` |
| POST | /auth/login | `{email, password}` | `AuthTokenResponseDto` + cookie |
| POST | /auth/google | `{idToken}` | `AuthTokenResponseDto` + cookie |
| POST | /auth/refresh `[public]` | — (reads cookie) | `AuthTokenResponseDto` + new cookie |
| POST | /auth/logout | — | 204 |
| POST | /demo/token `[public]` | — | `{accessToken, expiresInSeconds}` |

`AuthTokenResponseDto`: `{accessToken, userId, displayName, isOnboardingCompleted}`

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
| PUT | /skills/enrolled | `{skillSlugs: string[]}` | 204 |

`PUT /skills/enrolled` — replaces the user's enrolled skill set.  
Skills in the list that are not yet enrolled are set to `available`.  
Skills currently enrolled but absent from the list are set to `locked` (progress preserved).  
`sales-basics` is always kept enrolled.

`SkillTreeResponseDto`: `{skillNodes[], currentStreakDayCount, totalXpAmount, weeklyXpAmount}`  
`SkillTreeNodeDto`: `{skillId, slug, title, iconName, sortOrder, status, completedLessonCount, totalLessonCount, isLocked, stage}`. `stage` is the funnel-stage bucket the skill belongs to — see `Skills.Stage` in [DB_SCHEMA](DB_SCHEMA.md).

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

**AI Dialog Chat Endpoint:**
`POST /exercises/:exerciseId/chat` — for `ai_dialog` type exercises only. Handles multi-turn conversation.
`ExerciseChatResponseDto`: `{response: string, isComplete: boolean, turnNumber: number, maxTurns: number}`

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

`UserProfileStatsDto`: `{displayName, email, currentStreakDayCount, longestStreakDayCount, totalXpAmount, completedSkillCount, totalSkillCount, averageExerciseScore, persona?}`

`persona` values: `sdr` | `account_executive` | `account_manager` | `founder` | `other`

`AchievementDto`: `{achievementId, key, title, description, iconEmoji, isUnlocked, unlockedAt}`

Achievement condition types: `first_lesson` | `lesson_count` | `xp_total` | `streak_days` | `skill_completed`

`ExerciseSubmissionResultDto` now includes `newlyUnlockedAchievementKeys: string[]` — keys of achievements unlocked in this submit.

---

## League

| Method | Path | Response |
|---|---|---|
| GET | /league | `CurrentLeagueResponseDto` |

`CurrentLeagueResponseDto`: `{leagueId, tier, weekStartDate, weekEndDate, participantsByRank[], currentUserRank, previousWeekOutcome: "promoted"|"demoted"|null}`
`LeagueParticipantDto`: `{userId, displayName, weeklyXpAmount, rank, isCurrentUser}`

Tiers (in order): `bronze → silver → gold → diamond`
- Top 10 per tier promoted to next tier next week
- Bottom 5 per tier demoted (minimum bronze)
- `previousWeekOutcome`: shown only if user had a membership last week; use for in-app banner

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

`AdminSkillDto`: `{id, iconicName, title, description, orderInTree, stage}`. `stage` is one of `preparation`, `discovery`, `engagement`, `closing`, `retention`, `general` (default). Drives the grouped sidebar on `/tree`.

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
| POST | /admin/lessons/:lessonId/exercises | `{type, orderInLesson, content: <jsonb>, customAiPrompt?}` | `AdminExerciseDto` |
| PUT | /admin/exercises/:id | same | `AdminExerciseDto` |
| DELETE | /admin/exercises/:id | — | 204 |

`AdminExerciseDto`: `{id, lessonId, type, orderInLesson, content, customAiPrompt}`

### Exercise Type Prompts
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/exercise-type-prompts | — | `ExerciseTypePromptDto[]` |
| GET | /admin/exercise-type-prompts/:exerciseType | — | `ExerciseTypePromptDto` |
| PUT | /admin/exercise-type-prompts/:exerciseType | `{systemPrompt}` | `ExerciseTypePromptDto` |

`ExerciseTypePromptDto`: `{id, exerciseType, systemPrompt, updatedAt}`

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

### Users (requires `RequireSuperAdmin` for role change)
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/users | — | `AdminUserDto[]` |
| PUT | /admin/users/:id/role | `{role: "User"\|"Admin"\|"SuperAdmin"}` | `AdminUserDto` |

### Seeder
| Method | Path | Body | Response |
|---|---|---|---|
| POST | /admin/seeder/skills | `multipart/form-data; file=<JSON>` | `SkillsImportResultDto` |
| POST | /admin/seeder/topics | `multipart/form-data; file=<JSON>` | `TopicsImportResultDto` |
| POST | /admin/seeder/lessons | `multipart/form-data; file=<JSON>` | `LessonsImportResultDto` |

**Skills JSON:** `[{ iconicName, title, description?, orderInTree }]`
**Topics JSON:** `[{ skillIconicName, iconicName, title, orderInSkill }]`
**Lessons JSON:** `[{ topicIconicName, title, orderInTopic, exercises: [{ type, orderInLesson, content, customAiPrompt? }] }]`

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
- Chat: `gpt-4.1-mini` (configurable via `OpenAI:ChatModel`)
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

`FriendDto`: `{userId, displayName, persona?, totalXpAmount, currentStreakDayCount, achievementCount}`

`FriendRequestDto`: `{friendshipId, userId, displayName, persona?, direction, createdAt}`
- `direction`: `"incoming"` | `"outgoing"`

`PublicProfileDto`: `{userId, displayName, persona?, totalXpAmount, currentStreakDayCount, achievementCount, averageExerciseScore, friendshipStatus}`
- `friendshipStatus`: `"none"` | `"pending_outgoing"` | `"pending_incoming"` | `"friends"`

`UserSearchResultDto`: `{userId, displayName, persona?, friendshipStatus}`

`FriendLeaderboardEntryDto`: `{userId, displayName, totalXpAmount, rank, isCurrentUser}`

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
