# API_CONTRACTS.md

Base URL: `http://localhost:5000` (dev) | `http://backend:8080` (docker internal)

Все эндпоинты кроме помеченных `[public]` требуют `Authorization: Bearer <accessToken>`.

---

## Auth `[public]`

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /auth/register | `{email, password, displayName}` | `AuthTokenResponseDto` + cookie `refreshToken` |
| POST | /auth/login | `{email, password}` | `AuthTokenResponseDto` + cookie |
| POST | /auth/google | `{idToken}` | `AuthTokenResponseDto` + cookie |
| POST | /auth/refresh `[public]` | — (читает cookie) | `AuthTokenResponseDto` + новый cookie |
| POST | /auth/logout | — | 204 |
| POST | /demo/token `[public]` | — | `{accessToken, expiresInSeconds}` |

`AuthTokenResponseDto`: `{accessToken, userId, displayName, isOnboardingCompleted}`

---

## Onboarding

| Method | Path | Body | Response |
|---|---|---|---|
| POST | /onboarding | `{salesType, experienceLevel, goal}` | 204 |

`salesType`: `b2b_saas` / `retail` / `real_estate` / `finance` / `b2c`
`experienceLevel`: `beginner` / `experienced` / `manager`
`goal`: `close_deals` / `cold_calls` / `objections` / `everything`

---

## Skill Tree

| Method | Path | Response |
|---|---|---|
| GET | /skill-tree | `SkillTreeResponseDto` |

`SkillTreeResponseDto`: `{skillNodes[], currentStreakDayCount, totalXpAmount, weeklyXpAmount}`
`SkillTreeNodeDto`: `{skillId, slug, title, iconName, sortOrder, status, completedLessonCount, totalLessonCount, isLocked}`

---

## Lessons & Exercises

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /skills/:slug/lessons | — | `LessonSummaryDto[]` |
| GET | /lessons/:lessonId/exercises | — | `ExerciseDto[]` |
| POST | /exercises/:exerciseId/submit | `{answer: <jsonb>}` | `ExerciseSubmissionResultDto` |

`ExerciseDto.content` shape by type:

**multiple_choice**: `{situation, question, options[], correctOptionIndex, explanation?}`
**fill_blank**: `{characterName, characterLine, options[], correctOptionIndex, explanation?}`
**free_text**: `{situation, prompt, evaluationCriteria}`

`answer` shape by type:

**multiple_choice / fill_blank**: `{selectedOptionIndex: number}`
**free_text**: `{text: string}`

---

## Reference

| Method | Path | Response |
|---|---|---|
| GET | /skills/:slug/reference | `ReferenceMaterialDto[]` |

`ReferenceMaterialDto`: `{materialId, title, markdownContent, sortOrder}`

---

## Profile

| Method | Path | Response |
|---|---|---|
| GET | /profile | `UserProfileStatsDto` |

`UserProfileStatsDto`: `{displayName, email, currentStreakDayCount, longestStreakDayCount, totalXpAmount, completedSkillCount, totalSkillCount, averageExerciseScore}`

---

## League

| Method | Path | Response |
|---|---|---|
| GET | /league | `CurrentLeagueResponseDto` |

`CurrentLeagueResponseDto`: `{leagueId, tier, weekStartDate, weekEndDate, participantsByRank[], currentUserRank}`
`LeagueParticipantDto`: `{userId, displayName, weeklyXpAmount, rank, isCurrentUser}`
