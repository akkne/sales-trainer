# ARCHITECTURE.md

## Stack

```
Next.js 15 (TypeScript, App Router)
  → HTTP REST (JSON) + JWT Bearer
    → ASP.NET Core 9 Web API (C#)
        ├── PostgreSQL 17   (main relational data)
        ├── MongoDB 8       (chat messages, future transcripts)
        ├── Redis 7         (cache, sessions, leaderboards)
        └── OpenAI API      (free-text exercise evaluation)
```

## Frontend: `src/frontend`

**Libraries:** Next.js 15, TypeScript, Tailwind CSS, Zustand, TanStack Query, Framer Motion

**Route layout (App Router):**
```
app/
  (auth)/
    login/
    register/
    onboarding/
  (main)/
    tree/              ← skill tree, main screen
    skill/[id]/        ← lesson list inside a skill
    exercise/[id]/     ← exercise screen
    reference/[id]/    ← reference material
    league/            ← weekly leaderboard
    profile/           ← profile & stats
lib/
  api/apiClient.ts     ← single fetch wrapper (auto JWT + 401 refresh)
  store/authStore.ts   ← Zustand auth state
  hooks/               ← one hook per feature, no logic in components
components/
  ui/                  ← shared primitives
  exercise/            ← exercise type renderers
  layout/              ← shell, nav
```

## Backend: `src/backend`

**Vertical slice structure — one folder per feature:**
```
Features/
  Auth/
  Onboarding/
  SkillTree/
  Lessons/
  Exercises/
  Reference/
  Gamification/
  League/
  Profile/
Infrastructure/
  Data/
    AppDbContext.cs
    Migrations/
    *EntityConfiguration.cs   ← jsonb and array column configs
  Mongo/
  Redis/
```

**Rules from RAW.md enforced here:**
- No repository wrappers — services use `AppDbContext` directly
- DTO ≠ Entity — controllers never return EF entities
- All OpenAI calls go through `AiEvaluationService` (not yet implemented)
- Async/await everywhere, nullable reference types enabled

## Docker

`docker-compose.yml` at root starts all 5 services:
- `frontend` on :3000
- `backend` on :5000 (internal :8080)
- `postgres` on :5432 (healthcheck before backend starts)
- `mongo` on :27017
- `redis` on :6379

Backend auto-runs `db.Database.Migrate()` on startup.

## EF Column Types

| Property | Column type |
|---|---|
| `Skill.ApplicableSalesTypes` | `text[]` |
| `Exercise.SerializedContent` | `jsonb` |
| `UserExerciseAttempt.SerializedAnswer` | `jsonb` |
| `UserExerciseAttempt.SerializedAiFeedback` | `jsonb` |
