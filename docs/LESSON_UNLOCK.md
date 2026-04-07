# Sequential Lesson Unlock

## Feature Overview

Users progress through lessons within a skill one at a time:

- **First lesson** is automatically unlocked (`available`) when the user first accesses the skill.
- Remaining lessons start as `locked`.
- Completing a lesson (answering any exercise correctly) **unlocks the next lesson** in the same skill (ordered by `sortOrder`).
- All lessons in a skill can only be accessed after unlocking each one in sequence.

## How It Works

### Lazy Seeding — `EnsureSkillLessonsSeededAsync`

`ExerciseService` checks on every `GET /skills/:slug/lessons` and `GET /lessons` call whether `UserLessonProgress` rows exist for the user+skill combination. If no rows exist and the skill is **not locked**, the service seeds them:

```
Lesson sortOrder=1 → status = "available"
Lesson sortOrder=2 → status = "locked"
Lesson sortOrder=3 → status = "locked"
…
```

Seeding is a no-op if ANY progress record already exists for that user+skill (user has already started).

### Sequential Unlock — `UnlockNextLessonInSkillAsync`

Called from `UpdateLessonProgressAsync` (inside `SubmitExerciseAnswerAsync`) whenever a correct answer is recorded. After marking the current lesson `completed`, it finds the lesson with the next `sortOrder` in the same skill and sets its status to `available`.

- If the next lesson's `UserLessonProgress` row exists with `locked` → update to `available`.
- If the row doesn't exist yet (e.g. skill was never accessed before the submit) → create a new row with `available`.

### Skill-level unlock

Skill unlock (`UnlockNextSkillAsync`) is unchanged — it triggers after all lessons in a skill are completed. The first lesson of the newly-unlocked skill is then seeded as `available` on the next lesson fetch.

## Lesson Statuses

| Status | Meaning |
|---|---|
| `locked` | Not yet available — previous lesson not completed |
| `available` | Ready to start |
| `in_progress` | User has answered at least one exercise |
| `completed` | Lesson fully completed |

## Affected Code

| File | Change |
|---|---|
| `Features/Exercises/ExerciseService.cs` | `EnsureSkillLessonsSeededAsync`, `UnlockNextLessonInSkillAsync`, updated `GetAllLessonsAsync`, `GetLessonsForSkillAsync`, `UpdateLessonProgressAsync` |
| `tests/Unit/ExerciseServiceTests.cs` | 7 new unit tests for seeding and unlock logic |
| `tests/Integration/ExerciseSubmitTests.cs` | 3 new integration tests for full flow |

## Frontend

No frontend changes required — `LessonPath` already renders nodes based on `status` from the API (`locked` / `available` / `completed`).
