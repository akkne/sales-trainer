# LEARNING_SERVICE.md — Learning Service extraction

> Phase 8 of the [microservices migration](MICROSERVICES_ROADMAP.md). Extracts the
> content tree and the learner's progress through it out of the monolith
> (`src/backend/api`) into an independently deployable `learning-service`. This is the
> last and largest service; after its routes are flipped the monolith serves only the
> `/admin/users/*` admin user-management routes (never extracted; Phase 9 moves them).
> Its code stays in the repo as reference, retired in Phase 9.

## Bounded context

Content + the learner's progress through it:

- **SkillTree** — skills, stages, topics; the `/skill-tree` aggregate view.
- **Lessons / Exercises** — the exercise tree, submission grading, attempts, progress.
- **Reference** — per-skill reference materials.
- **Techniques** — the technique library (cards, detail, coach, user progress).
- **DailyQuotes** — the daily motivational quote.
- **Admin** — content CRUD for everything above, plus the JSON content seeder.

## Layout

```
src/backend/learning-service/
  Learning/
    Program.cs                         service host wiring (extensions only)
    Sellevate.Learning.csproj
    Dockerfile                         build context = src/backend (for building-blocks)
    Common/Constants/                  ExerciseTypes, LessonProgressStatuses, LessonKinds, policies
    DependencyInjection/               AddLearningServices(IServiceCollection, IConfiguration)
    Eventing/                          exercise/lesson/skill/technique producers + user.* replica consumer
    Identity/                          local UserReplica
    Features/
      SkillTree/                       /skills, /skill-tree, /skills/{id}/topics
      Lessons/                         Lesson/Exercise/progress/attempt models
      Exercises/                       /lessons, /exercises/*, deterministic + AI grading, chat/voice
      Reference/                       /reference
      Techniques/                      /techniques
      DailyQuotes/                     /daily-quote
      Admin/                           /admin/* content CRUD + /admin/seeder/*
    Infrastructure/
      Ai/                              AI evaluation client (calls ai-service /ai/evaluate) + ported chat/TTS
      Configuration/                   AiService + OpenAI/TTS options
      Data/                            LearningDbContext (Postgres) + EF migrations + bootstrapper
  Learning.Tests/                      NUnit unit tests
```

## Data ownership

Owns Postgres database **`learning`** (created on startup by `DatabaseBootstrapper`,
then EF migration `InitialLearningSchema` runs):

`Skills`, `SkillStages`, `Topics`, `UserSkillProgressRecords`, `Lessons`, `Exercises`,
`UserLessonProgressRecords`, `UserExerciseAttempts`, `ExerciseTypePrompts`,
`ReferenceMaterials`, `DailyQuotes`, `Techniques`, `TechniqueSkills`,
`TechniqueCoaches`, `UserTechniqueProgressRecords`, plus a local `UserReplicas`
read-model (`UserId`, `Email`, `DisplayName`, `AvatarKey`) fed by `user.*` events.

Reuses the shared Redis (Kafka idempotency store) and Kafka broker.

## Coupling broken during extraction

| Monolith coupling | Resolution in learning-service |
|---|---|
| `ExerciseService` writing `UserXp` / `UserStreak` rows + calling `IGamificationService` for base XP | Removed. On a correct submission Learning emits `exercise.completed`; Gamification grants XP / updates streaks. `XpEarned` in the submission DTO is `0` (shape unchanged). |
| `ExerciseService` calling `IAchievementService.EvaluateAchievementsAfterSubmitAsync` | Removed. Achievements are unlocked by Gamification reacting to the events. `NewlyUnlockedAchievementKeys` is empty (shape unchanged). |
| `ExerciseService` writing `StreakMilestone` notifications | Removed. Gamification produces `streak.milestone`; Notifications writes the inbox entry. |
| AI grading strategies calling OpenAI directly + reading `ExerciseTypePrompt` | The deterministic strategies stay local. The 5 AI types call ai-service `POST /ai/evaluate`, passing the global `ExerciseTypePrompt` text (Learning still owns the prompts) plus the raw exercise content + user answer; ai-service runs the LLM grading and returns the verdict. |
| `SkillTreeService` reading XP/streak/goals for the `/skill-tree` aggregate | Those fields are owned by Gamification (Phase 7). Learning serves the skill-progress fields truthfully (computed from its own lesson progress) and returns `currentStreakDayCount`/`totalXp`/`weeklyXp`/`dailyXp`/goals as `0` — DTO shape unchanged, composed for real once the frontend reads gamification aggregates. |

## Events produced

| Topic | When | Payload (camelCase on the wire) |
|---|---|---|
| `exercise.completed` | every exercise submission | `{ userId, exerciseType, score, isCorrect }` |
| `lesson.completed` | a lesson transitions to completed | `{ userId, lessonId, bestScore }` |
| `skill.completed` | the last lesson of a skill completes | `{ userId, skillId }` |
| `technique.mastery.changed` | a user's technique mastery/level changes | `{ userId, techniqueId, level, masteryPercent }` |

The first three match the gamification-service consumer contract verbatim
(`ExerciseCompletedEvent`, `LessonCompletedEvent`, `SkillCompletedEvent`). Consumed by
Gamification (XP/streaks/achievements/league) and Analytics (`exercise.completed`).

## Events consumed

`user.registered` / `user.updated` / `user.avatar.changed` / `user.deleted` →
keep the local `UserReplica` in sync (idempotent, dedupe on `eventId`).

## Synchronous dependency

`Learning → AI`: `POST /ai/evaluate` for the 5 AI-graded exercise types
(`spot_mistake`, `rewrite`, `ai_dialogue`, `evaluate_call`, `free_text`). The learner
is waiting for the grade in real time, so this is REST, not an event. Configured via
the `AiService:BaseUrl` option (`http://ai:8080` in compose).

## Routes flipped at the gateway

`/skills/*`, `/skills`, `/skill-tree`, `/lessons/*`, `/lessons`, `/topics/*`,
`/exercises/*`, `/reference/*`, `/reference`, `/techniques/*`, `/techniques`,
`/daily-quote`, and the learning `/admin/*` content routes (`/admin/skills`,
`/admin/skill-stages`, `/admin/topics`, `/admin/lessons`, `/admin/exercises`,
`/admin/exercise-type-prompts`, `/admin/reference`, `/admin/techniques`,
`/admin/daily-quotes`, `/admin/seeder`). `/profile/*` is intentionally NOT captured
(owned by identity/gamification).

After this flip the only public route still served by the monolith catch-all is
`/admin/users/*` (admin user management: list/detail, moderation rename, avatar
reset, role change). It was never part of any service's scope — Identity owns the
user aggregate but never took these admin routes. Phase 9 must move `/admin/users/*`
(naturally to identity-service) before the monolith can be retired; until then the
`monolith` cluster and its catch-all must stay in the gateway.

## Known limitations

- `/exercises/{id}/chat` and `/exercises/{id}/voice/stream` (interactive `ai_dialogue`)
  are served by Learning with the OpenAI chat + TTS pipeline ported from the monolith
  so the frontend contract is preserved. Long term this LLM/TTS compute belongs in
  ai-service behind a generic chat endpoint; that refactor is out of Phase 8 scope.
- `technique.mastery.changed` has a publisher and contract but no current trigger:
  the monolith's `MarkTechniqueSeen` only records first-seen and never changes mastery
  (matching prior behaviour). The producer is wired for when a mastery-progression
  flow lands.

## Local dev

`scripts/dev-learning.sh` (host port **5008**, db `learning` on the shared Postgres).
Run alongside `scripts/dev-ai.sh` (AI grading) and `scripts/dev-gateway.sh`.
See [docs/TESTING/LEARNING_SERVICE.md](TESTING/LEARNING_SERVICE.md).
