# Testing — DB-driven Progress Points / Progress & Recognition

Covers the admin-editable progress-points economy: per-exercise-type base progress
points, dialog scoring (multiplier + criterion weights), daily/weekly goals, and
activity-consistency milestones.
See [DB_SCHEMA](../DB_SCHEMA.md) (`GamificationSettings`, `ExerciseTypeRewards`,
`StreakMilestones`) and [API_CONTRACTS](../API_CONTRACTS.md#gamification-xp).

## Automated tests

Run from `src/backend`:

```bash
dotnet test Sellevate.Tests.csproj --filter "FullyQualifiedName~GamificationServiceTests"
dotnet test Sellevate.Tests.csproj --filter "FullyQualifiedName~OpenAiChatServiceTests"
dotnet test Sellevate.Tests.csproj --filter "FullyQualifiedName~ExerciseServiceTests"
dotnet test Sellevate.Tests.csproj --filter "FullyQualifiedName~AdminGamificationTests"   # needs Docker (Testcontainers)
```

- **`Unit/GamificationServiceTests`** — settings load-or-create + idempotency; exercise
  base-XP lookup with fallback to 10; streak bonus from DB vs. historic fallback ladder
  (and that a non-empty table is authoritative — removed milestones don't resurrect).
- **`Unit/OpenAiChatServiceTests`** — dialog feedback parses `[XP:N]` and clamps to the
  configured weight total (custom weights summing to 60 clamp a raw 95 → 60).
- **`Unit/ExerciseServiceTests`** — a correct answer awards the DB-configured base XP;
  reaching a DB-configured streak day count awards that bonus. With nothing seeded, the
  historic defaults (10 XP; 7→50, 30→200) still apply.
- **`Integration/AdminGamificationTests`** — `/admin/gamification/*` auth (user → 403),
  settings GET/PUT (incl. zero-weight-sum rejection), exercise-reward upsert, and the full
  streak-milestone CRUD cycle (incl. duplicate-day rejection).

## Manual checks

> **The user-facing UI is gone (2026-08-14).** No XP, streak, league or achievement surface exists
> in the app any more — verify these through the admin panel and the API responses, never by
> looking for a number on a user screen. See [DECISIONS.md](../DECISIONS.md).


1. Admin → `/admin/gamification`: change the daily goal; the skill-tree progress ring uses it.
2. Admin → `/admin/prompts`: raise a type's base XP; submit that exercise type → the awarded
   XP matches in the `/exercises/{id}/submit` response (nothing is rendered).
3. Admin → `/admin/dialog`: set the multiplier to 2.0; complete a dialog → `xpEarned` in the
   `/complete` response is the AI score doubled (the modal never shows it). Adjust a criterion weight → the feedback prompt's per-criterion caps change.
4. Add a streak milestone (e.g. 3 → 30); reach a 3-day streak → bonus is awarded once.
