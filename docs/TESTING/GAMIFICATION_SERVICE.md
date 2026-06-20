# TESTING — Gamification Service

How to build, test, and manually verify `src/backend/gamification-service`.

## Automated tests

```bash
dotnet test src/backend/gamification-service/Gamification.Tests/Sellevate.Gamification.Tests.csproj
```

Unit suite (NUnit, EF Core InMemory + NSubstitute — runs offline):

| Test fixture | Covers |
|---|---|
| `GamificationEventHandlerTests` | XP grant from each event type: `exercise.completed` (correct grants base/configured XP, incorrect grants none, always registers streak), `dialog.evaluated` (grants `xpEarned`), `lesson.completed` (increments count + unlocks first-lesson + emits `achievement.unlocked`), `skill.completed` (marks skill + unlocks skill achievement). |
| `StreakServiceTests` | New streak starts at 1; same-day twice does not increment; gap resets to 1 (longest preserved); consecutive day increments and a reached milestone grants bonus XP + emits `streak.milestone`; no milestone → no emission. |
| `StreakResetJobTests` | Stale streaks (no activity since before yesterday) reset to 0; today's and yesterday's streaks untouched; longest preserved. |
| `AchievementServiceTests` | XP-threshold unlock + `achievement.unlocked` emission; below-threshold no unlock; idempotent re-evaluation does not unlock twice; `GetAchievementsForUserAsync` unlocked flag per achievement. |
| `LeagueServiceTests` | Settings initialize the period when missing; weekly rollover promotes the top zone, relegates the bottom zone, keeps the middle, and advances the period schedule. |
| `OutgoingEventContractTests` | Serialized payload shapes for `gamification.dialog-weights.updated` (matches what ai-service consumes), `xp.granted` (analytics), `achievement.unlocked` / `streak.milestone` (notification-service); and that `dialog.evaluated` from AI deserializes correctly. |

Gateway route-flip is verified in the gateway test project:

```bash
dotnet test src/backend/gateway/Gateway.Tests/Sellevate.Gateway.Tests.csproj
```

`GamificationRouteFlipTests` proves `/gamification/*`, `/league`, `/profile/achievements`,
`/admin/gamification/*`, `/admin/leagues/*` resolve to the `gamification` cluster (not the
monolith) and that the cluster has a destination.

## Build

```bash
dotnet build src/backend/gamification-service/Gamification/Sellevate.Gamification.csproj
```

## Manual checklist (requires infra)

1. `scripts/dev-infra.sh` then `scripts/dev-gamification.sh` (or `docker compose up --build -d gamification gateway`).
2. `GET http://localhost:5007/healthz` → `{ "status": "ok", "service": "gamification" }`.
3. Through the gateway (`http://localhost:5000`, with a valid JWT):
   - `GET /gamification/progress` → XP totals + daily/weekly amounts and goals + streak.
   - `GET /profile/achievements` → the seeded achievements with `isUnlocked` flags.
   - `GET /league` → current league with participants ranked by weekly XP.
   - `GET /admin/gamification/settings`, `PUT /admin/gamification/settings` (admin JWT).
   - `GET /admin/leagues`, `POST /admin/leagues/close-current`, tier/membership admin.
4. Publish an `exercise.completed` / `dialog.evaluated` / `lesson.completed` / `skill.completed`
   event (Kafka UI on `:8085`) and confirm XP/streak/achievement state changes and that
   `xp.granted` / `achievement.unlocked` / `streak.milestone` are produced.
5. `PUT /admin/gamification/settings` → confirm a `gamification.dialog-weights.updated`
   event is produced (the ai-service caches it).
