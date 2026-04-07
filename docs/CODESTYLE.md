# CODESTYLE.md

## This is law. No exceptions.

---

## Naming

**No abbreviations. Ever.** Names explain purpose without context.

- Variables and fields — long, descriptive: `currentAuthenticatedUser`, `totalCompletedExerciseCount`
- Booleans — always prefixed with `is`, `has`, `can`, `should`, `was`
- Collections — plural with content indicator: `unlockedSkillNodes`, `weeklyLeagueScoresByUserId`
- Methods — verb + full noun: `ProcessExerciseSubmission`, `FindUserByIdentifier`
- Classes — full role name: `ExerciseEvaluationService`, not `ExSvc`
- Interfaces — `I` prefix + full name: `IExerciseRepository`

---

## File structure

**One class — one file. Always.**

File name matches the class inside exactly. No two classes, record+interface, or dto+validator in one file.

Organized by feature, not by type:
```
Features/Exercises/ExerciseController.cs
Features/Exercises/ExerciseEvaluationService.cs
Features/Exercises/ExerciseRepository.cs
Features/Exercises/ExerciseSubmissionDto.cs
```

---

## Required patterns

**Service Layer** — all business logic lives in services. Controller only receives request, calls service, returns result.

**Strategy** — different exercise types (multiple choice, free text, voice) are implemented as separate strategies with a common interface.

**Factory** — creation of complex objects (skill tree, personalized onboarding) via factories.

**Observer** — events after user actions (completed exercise → update streak, update league, award XP) via observers.

**Custom Hook** (frontend) — all logic is extracted from components into hooks. Component only renders.

---

## Comments

**Forbidden entirely.** If code requires a comment — rename the variable or decompose the method.

---
