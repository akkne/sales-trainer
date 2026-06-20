# GAMIFICATION_SERVICE.md — Gamification Service extraction

> Phase 7 of the [microservices migration](MICROSERVICES_ROADMAP.md). Extracts the
> rewards-and-competition core (XP, streaks, achievements, league) out of the monolith
> (`src/backend/api`) into an independently deployable, **event-driven** `gamification-service`.
> The monolith slices are left in place as reference; the gateway flips the relevant
> routes to the new service (strangler fig).

## Bounded context

Rewards & competition — the flagship event-driven service:

- **XP** — `UserXpRecords`, admin-tunable `GamificationSettings` and `ExerciseTypeRewards`.
- **Streaks** — `UserStreaks`, admin-tunable `StreakMilestones`, daily reset job.
- **Achievements** — `Achievements` + `UserAchievements`, unlocked from event-driven progress.
- **League** — `Leagues`, `LeagueTiers`, `LeagueMemberships`, `LeagueSettings`, weekly rollover.

Gamification owns **no** write to its inputs — it reacts to Kafka events and is the
sole writer of XP/streak/achievement/league state. This is pure eventual consistency:
if an event is late, the XP simply lands a moment later. There is **no** cross-service
transaction with Learning or AI.

## Layout

```
src/backend/gamification-service/
  Gamification/
    Program.cs                         service host wiring (extensions only)
    Sellevate.Gamification.csproj
    Dockerfile                         build context = src/backend (for building-blocks)
    Common/Constants/                  XP sources, achievement condition types, routes, job ids
    Common/Extensions/                 ClaimsPrincipal user-id resolver
    DependencyInjection/               AddGamificationServices()
    Eventing/                          consumers (user.*, learning, dialog) + Kafka publisher
    Features/
      Gamification/                    XP grant, streak, settings, progress, event handler, StreakResetJob
      Achievements/                    achievement evaluation, learning-progress projection, seeder
      League/                          league service + WeeklyLeagueClosureJob
      Admin/                           /admin/gamification + /admin/leagues controllers + DTOs
    Identity/                          local UserReplica
    Infrastructure/
      Data/                            GamificationDbContext (Postgres) + EF migration + bootstrapper
  Gamification.Tests/                  NUnit unit tests
```

## Data ownership

| Store | Owns | Notes |
|---|---|---|
| Postgres `gamification` | `UserXpRecords`, `UserStreaks`, `GamificationSettings`, `ExerciseTypeRewards`, `StreakMilestones` | XP economy + streak config. |
| Postgres `gamification` | `Achievements`, `UserAchievements` | Seeded with the 10 default achievements on startup. |
| Postgres `gamification` | `Leagues`, `LeagueTiers`, `LeagueMemberships`, `LeagueSettings` | Weekly competition; DB-backed (Phase 26 Redis-leaderboard is **SKIP**). |
| Postgres `gamification` | `UserReplicas` | Local read-model (`UserId`, `Email`, `DisplayName`, `AvatarKey`) fed by `user.*` events; used by league participant lists + admin instead of joining Identity. |
| Postgres `gamification` | `UserLearningProgress` | Local projection of completed-lesson count + has-completed-any-skill, fed by `lesson.completed` / `skill.completed`, so achievement evaluation needs no cross-read into Learning. |
| Postgres `gamification` | Hangfire schema | `StreakResetJob` + `WeeklyLeagueClosureJob` run on this DB. |
| Redis (shared) | Kafka idempotency store | Dedupe on `eventId`. |

`DatabaseBootstrapper` creates the `gamification` database on startup, then EF migrations
run (`InitialGamificationSchema`), then the achievement seeder runs.

## Coupling broken during extraction

| Monolith coupling | Resolution in gamification-service |
|---|---|
| `ExerciseService` writing `UserXp` + updating `UserStreak` inline on submit | Gamification consumes `exercise.completed` and grants XP / updates streak itself. |
| `DialogController` writing `UserXp` for a finished roleplay | Gamification consumes `dialog.evaluated` (XP already computed by AI as `xpEarned`) and grants it. |
| `AchievementService` reading `UserLessonProgressRecords` / `UserSkillProgressRecords` (owned by Learning) | A local `UserLearningProgress` projection is maintained from `lesson.completed` / `skill.completed`; XP total + streak are read from this service's own tables. |
| `AchievementService` calling `INotificationService` directly | Gamification emits `achievement.unlocked`; the notification-service consumes it. |
| League / admin joining the monolith `Users` table | Replaced with joins onto the local `UserReplicas`. |
| Admin dialog-weights stored in the monolith and read by `DialogService` | On `PUT /admin/gamification/settings` the service emits `gamification.dialog-weights.updated`; AI caches it. |

## Kafka

- **Consumes** (all idempotent, dedupe on `eventId`):
  - `user.registered` / `user.updated` / `user.deleted` / `user.avatar.changed` → maintain `UserReplica`.
  - `exercise.completed` (`userId`, `exerciseType`, `score`, `isCorrect`) → grant base XP (if correct), register streak activity, evaluate achievements.
  - `dialog.evaluated` (`userId`, `sessionId`, `bundleId`, `modeId`, `rawScore`, `xpEarned`) → grant `xpEarned` as dialog XP, register streak, evaluate achievements.
  - `lesson.completed` (`userId`, `lessonId`, `bestScore`) → increment lesson count, register streak, evaluate achievements.
  - `skill.completed` (`userId`, `skillId`) → mark skill completed, evaluate achievements.
- **Produces** (partition key = `userId`, except dialog-weights which is a singleton config snapshot):
  - `xp.granted` — `{ userId, amount, source }` (analytics reads `userId`/`amount`).
  - `achievement.unlocked` — `{ userId, achievementKey, title }` (notification-service contract).
  - `streak.milestone` — `{ userId, dayCount, bonusXp }` (notification-service contract).
  - `gamification.dialog-weights.updated` — `{ confidence, structure, objection, goal, multiplier }` (ai-service contract; `int/int/int/int/double`).

> Producers for `exercise.completed` / `lesson.completed` / `skill.completed` ship with
> the Learning service (Phase 8); the consumers are wired now per the event catalogue and
> idle until then. `dialog.evaluated` is already produced by the AI service (Phase 6).

## Routes (through the gateway, paths preserved)

Flipped to the `gamification` cluster:

- `/gamification/*` — `GET /gamification/progress` (XP totals + daily/weekly amounts and
  goals + streak; Identity's `/profile` composes this once it consumes the data).
- `/league` (+ `/league/*`).
- `/profile/achievements` — more specific than Identity's `/profile/*`, so it wins.
- `/admin/gamification/*`, `/admin/leagues` (+ `/admin/leagues/*`).

All frontend DTO shapes are preserved from the monolith.

## Background jobs (Hangfire on the gamification DB)

- `WeeklyLeagueClosureJob` — cron `*/15 * * * *`; rolls the league over once the
  admin-configured period end has passed.
- `StreakResetJob` — cron `5 0 * * *`; zeroes streaks with no activity since before yesterday.

## Running locally

Infra (`scripts/dev-infra.sh`) then `scripts/dev-gamification.sh` (host, port 5007), or the
full Docker stack `docker compose up --build -d gamification gateway`. Health: `GET /healthz`.

See [docs/TESTING/GAMIFICATION_SERVICE.md](TESTING/GAMIFICATION_SERVICE.md) for the test
layout and the manual checklist.
