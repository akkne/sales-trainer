# GAMIFICATION_SERVICE.md — Gamification Service extraction

> Phase 7 of the [microservices migration](MICROSERVICES_ROADMAP.md). Extracts the
> progress & recognition core (progress points, activity consistency, milestones, team
> progress) out of the monolith (`src/backend/api`) into an independently deployable,
> **event-driven** `gamification-service`. The monolith slices are left in place as
> reference; the gateway flips the relevant routes to the new service (strangler fig).

## Bounded context

Progress & recognition — the event-driven service:

- **Progress points** — `UserXpRecords`, admin-tunable `GamificationSettings` and `ExerciseTypeRewards`.
- **Activity consistency** — `UserStreaks`, admin-tunable `StreakMilestones`, daily reset job.
- **Milestones** — `Achievements` + `UserAchievements`, unlocked from event-driven progress.
- **Team progress** — `Leagues`, `LeagueTiers`, `LeagueMemberships`, `LeagueSettings`, weekly rollover.

Gamification owns **no** write to its inputs — it reacts to Kafka events and is the
sole writer of progress-points/activity/milestone/team-progress state. This is pure
eventual consistency: if an event is late, the progress points simply land a moment
later. There is **no** cross-service transaction with Learning or AI.

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
| Postgres `gamification` | `UserXpRecords`, `UserStreaks` | Progress-points ledger + activity-consistency state. Tenancy: `OrganizationId` `NOT NULL`, `ITenantScoped`, strict RLS (40.13). |
| Postgres `gamification` | `GamificationSettings`, `ExerciseTypeRewards`, `StreakMilestones` | Installation-wide configuration. **No `OrganizationId`** — deliberately platform-global (40.13). |
| Postgres `gamification` | `Achievements` | Catalogue, seeded with the 10 default milestones on startup. **No `OrganizationId`.** |
| Postgres `gamification` | `UserAchievements` | Unlocked milestones. Tenancy: `OrganizationId` `NOT NULL`, `ITenantScoped`, strict RLS (40.13). |
| Postgres `gamification` | `Leagues`, `LeagueMemberships`, `LeagueSettings` | Weekly team/cohort progress; DB-backed (Phase 26 Redis-based ranking is **SKIP**). Tenancy: `OrganizationId` `NOT NULL`, `ITenantScoped`, strict RLS (40.13) — `LeagueSettings` became per-organization state, not shared configuration (see below). |
| Postgres `gamification` | `LeagueTiers` | The tier ladder (`bronze`/`silver`/…). Catalogue. **No `OrganizationId`.** |
| Postgres `gamification` | `UserReplicas` | Local read-model (`UserId`, `Email`, `DisplayName`, `AvatarKey`) fed by `user.*` events; used by team-progress participant lists + admin instead of joining Identity. **No `OrganizationId`**: cross-organization identity projection, same call learning (40.10)/ai (40.11)/social (40.13) made. |
| Postgres `gamification` | `UserLearningProgress` | Local projection of completed-lesson count + has-completed-any-skill, fed by `lesson.completed` / `skill.completed`, so achievement evaluation needs no cross-read into Learning. Tenancy: `OrganizationId` `NOT NULL`, `ITenantScoped`, strict RLS (40.13); primary key is `(OrganizationId, UserId)`. |
| Postgres `gamification` | Hangfire schema + `OutboxMessages` | `StreakResetJob` + `WeeklyLeagueClosureJob` run on this DB. `OutboxMessages` has no `OrganizationId`/RLS — read only by the system-mode relay. |
| Redis (shared) | Kafka idempotency store | Dedupe on `eventId`. |

`DatabaseBootstrapper` creates the `gamification` database on startup, then EF migrations
run (`InitialGamificationSchema` … `AddOrganizationId`), then the settings seeder runs. Index
rebuilds and the backfill are **not** part of startup — they are operational steps, driven by
`scripts/tenancy-gamification-organization-rollout.sh`.

## Multi-tenancy (Phase 40.13)

Seven tables hold one customer's data — `UserXpRecords`, `UserStreaks`, `UserAchievements`,
`UserLearningProgress`, `Leagues`, `LeagueMemberships`, `LeagueSettings` — all `OrganizationId NOT
NULL`, `ITenantScoped`, the **strict** RLS flavour (`EnableTenantRls`, plain equality): there is no
global content library in this database, so a row with no organization is invisible, not shared.
Seven others deliberately get nothing: `Achievements` and `LeagueTiers` are catalogues,
`GamificationSettings`, `StreakMilestones` and `ExerciseTypeRewards` are installation-wide
configuration, `UserReplicas` projects Identity's cross-organization directory (TENANCY.md §4.2,
same call as 40.10/40.11), and `OutboxMessages` is read only by the system-mode relay.

**`LeagueSettings` is the one judgement call worth knowing about.** It looks like configuration and
is not: `CurrentPeriodStartDate` / `CurrentPeriodEndsAt` are the state of a running competition —
which week is currently open. Shared, the first organization to roll over advanced the period for
everybody, and every other organization's rollover then found the period already advanced and
bailed out, leaving its leagues open forever. It is now tenant data with `UNIQUE(OrganizationId)`,
created lazily per organization: the startup seeder no longer creates it (startup has no tenant,
and a row seeded with no organization would be hidden from everybody by its own policy), and
`LeagueService.GetSettingsAsync` returns a correct unsaved default until an organization's admin
first saves league settings.

Four unique constraints move into the `AddOrganizationId` migration itself rather than the
concurrent-rebuild script, because each was load-bearing for correctness in the window between
deploy and script — and all four tables hold at most a row per user or a handful of rows per week,
so the swap is a short lock:

- `UNIQUE(WeekStartDate, Tier)` on `Leagues` → `UNIQUE(OrganizationId, WeekStartDate, Tier)`: the
  old constraint meant "one bronze league per week for the whole platform" — the second
  organization to roll over would get a unique violation and no league at all.
- `UNIQUE(UserId)` on `UserStreaks` → `UNIQUE(OrganizationId, UserId)`, and
  `UNIQUE(UserId, AchievementId)` on `UserAchievements` → `UNIQUE(OrganizationId, UserId,
  AchievementId)`: memberships (40.6) let one person belong to two customers, and the old
  constraints would have refused that person a second row.
- `UserLearningProgress`'s primary key moves from `UserId` alone to `(OrganizationId, UserId)`: the
  same person in two organizations would otherwise have one organization silently overwrite the
  other's counters.

The read indexes on the two tables that actually grow without bound — `UserXpRecords` and
`LeagueMemberships` — stay out of the migration and are rebuilt concurrently by
`scripts/tenancy-gamification-organization-rollout.sh --indexes`. `UNIQUE(SourceEventId)` on
`UserXpRecords` stays global on purpose: it is a statement about the Kafka event stream, and adding
the organization would let one event grant XP once per tenant.

### Background jobs, reclassified

Both Hangfire jobs became **iterate-organizations** jobs (TENANCY.md §1.6, full detail in
[Background jobs](#background-jobs-hangfire-on-the-gamification-db) below), which is not tidiness
but necessity: a single pass with no tenant compares `OrganizationId` against `null`, matches
nothing under RLS, and logs success having done nothing. Both enumerate organizations from
gamification-db's own tables (`SELECT DISTINCT "OrganizationId"` in system mode via
`TenantJobScope`) rather than a replicated tenant registry, matching company-service's 40.12
decision — a registry-driven loop could silently skip an organization whose registry row had not
replicated yet. That enumeration query only returns rows for a role that bypasses RLS (today the
service connects as the owning superuser); see `docs/DONT_FORGET.md` for what changes the day the
service moves to a `NOBYPASSRLS` role.

`LeagueSettingsSeeder` no longer seeds `LeagueSettings` at startup — startup has no tenant, and the
row is created lazily per organization instead (see above). It still seeds the singleton,
platform-global `GamificationSettings`. `OutboxRelayBackgroundService` stays system-mode (reads only
`OutboxMessages`, which has no `OrganizationId`/RLS) and `UserReplicaConsumer` stays
platform-global (`RequiresOrganization => false`, `UserReplicas` has no organization column).

**Not run against any database:** migration `20260815213223_AddOrganizationId` adds the columns,
the four load-bearing unique-index swaps, and RLS policies.
`docs/TENANCY/sql/40.13_gamification_organization_backfill.sql` and
`docs/TENANCY/sql/40.13_gamification_organization_indexes_concurrently.sql`, driven by
`scripts/tenancy-gamification-organization-rollout.sh`, have not been run against any database —
see `docs/DONT_FORGET.md` for the rollout order.

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

- `/gamification/*` — `GET /gamification/progress` (progress-point totals + daily/weekly
  amounts and goals + activity consistency; Identity's `/profile` composes this once it
  consumes the data).
- `/league` (+ `/league/*`).
- `/profile/achievements` — more specific than Identity's `/profile/*`, so it wins.
- `/admin/gamification/*`, `/admin/leagues` (+ `/admin/leagues/*`).

All frontend DTO shapes are preserved from the monolith.

## Background jobs (Hangfire on the gamification DB)

- `WeeklyLeagueClosureJob` — cron `*/15 * * * *`; iterates every organization that has a
  `LeagueSettings` row (40.13) and rolls that organization's league over once its own
  admin-configured period end has passed.
- `StreakResetJob` — cron `5 0 * * *`; iterates every organization that has a live streak (40.13)
  and zeroes that organization's streaks with no activity since before yesterday.

Both jobs used to run once, cross-tenant. See [Multi-tenancy](#multi-tenancy-phase-4013) above for
why that stopped being safe once `UserStreaks`/`LeagueSettings` gained RLS, and for the operational
dependency on the service's role having `BYPASSRLS`.

## Running locally

Infra (`scripts/dev-infra.sh`) then `scripts/dev-gamification.sh` (host, port 5007), or the
full Docker stack `docker compose up --build -d gamification gateway`. Health: `GET /healthz`.

See [docs/TESTING/GAMIFICATION_SERVICE.md](TESTING/GAMIFICATION_SERVICE.md) for the test
layout and the manual checklist.
