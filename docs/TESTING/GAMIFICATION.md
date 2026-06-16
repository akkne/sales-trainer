# Testing — DB-driven XP / Gamification

Covers the admin-editable XP economy: per-exercise-type base XP, dialog scoring
(multiplier + criterion weights), daily/weekly goals, and streak milestones.
See [DB_SCHEMA](../DB_SCHEMA.md) (`GamificationSettings`, `ExerciseTypeRewards`,
`StreakMilestones`) and [API_CONTRACTS](../API_CONTRACTS.md#gamification-xp).

## Automated tests

Run from `src/backend`:

```bash
dotnet test SalesTrainer.Tests.csproj --filter "FullyQualifiedName~GamificationServiceTests"
dotnet test SalesTrainer.Tests.csproj --filter "FullyQualifiedName~OpenAiChatServiceTests"
dotnet test SalesTrainer.Tests.csproj --filter "FullyQualifiedName~ExerciseServiceTests"
dotnet test SalesTrainer.Tests.csproj --filter "FullyQualifiedName~AdminGamificationTests"   # needs Docker (Testcontainers)
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

1. Admin → `/admin/gamification`: change the daily goal; the skill-tree progress ring uses it.
2. Admin → `/admin/prompts`: raise a type's base XP; submit that exercise type → the awarded
   XP matches.
3. Admin → `/admin/dialog`: set the multiplier to 2.0; complete a dialog → earned XP is the
   AI score doubled. Adjust a criterion weight → the feedback prompt's per-criterion caps change.
4. Add a streak milestone (e.g. 3 → 30); reach a 3-day streak → bonus is awarded once.
