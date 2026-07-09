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
  api/apiClient.ts           ← single fetch wrapper (auto JWT + 401 refresh)
  store/authStore.ts         ← Zustand auth state
  store/selectedSkillStore.ts ← persisted selected skill for /tree home view
  hooks/                     ← one hook per feature, no logic in components
components/
  ui/                  ← shared primitives
  exercise/            ← exercise type renderers
  layout/              ← shell, nav
```

## Backend: `src/api/backend`

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

## Microservices migration — platform foundations (Phase 0)

The monolith above has been carved into independently deployable services per
[MICROSERVICES.md](MICROSERVICES.md) (target) and
[MICROSERVICES_ROADMAP.md](MICROSERVICES_ROADMAP.md) (route). **The migration is
complete (Phase 9): the monolith is retired** — every route is owned by a service and
the gateway no longer has a catch-all to the monolith. `src/backend/api` is kept in the
repo and the solution as a reference only (not built or run as a container):

```
src/backend/
  api/                         ← RETIRED monolith (reference only; not deployed)
  tests/                       ← monolith tests (reference only)
  {identity,learning,gamification,ai,social,analytics,notification,company}-service/
                               ← the extracted services, each with its own DB + tests
  building-blocks/BuildingBlocks/   ← shared lib (event envelope, Kafka publisher +
                                       idempotent-consumer base, Redis idempotency
                                       store, UserReplica, identity-header helpers)
  building-blocks/BuildingBlocks.Tests/
  gateway/Gateway/             ← YARP API gateway (per-service routing, no catch-all:
                                  unknown routes 404; central JWT validation,
                                  X-User-* header injection)
  gateway/Gateway.Tests/
  Sellevate.sln                ← backend-wide solution (all of the above)
```

- **Event bus:** Apache Kafka (single-broker KRaft) for backend↔backend events.
  Topic names + the `{ eventId, occurredAt, type, version, data }` envelope live in
  `BuildingBlocks` (`Topics`, `EventEnvelope`). Consumers are idempotent (dedupe on
  `eventId` via a Redis-backed `IIdempotencyStore`). Local broker: `localhost:9092`,
  Kafka UI on `:8085`.
- **Poison-message handling (Phase 10.2):** the shared idempotent consumer base
  (`KafkaConsumerBackgroundService` → `EventMessageProcessor`) retries a failing handler
  a bounded number of times in-process, then — if dead-lettering is enabled — forwards the
  original message to `<topic>.dlt` (e.g. `exercise.completed.dlt`) and commits the offset,
  so a single poison message can never block its partition. The policy is opt-in via the
  strongly-typed `ConsumerResilienceSettings` (config section `Kafka:ConsumerResilience`)
  with safe defaults: 3 retries, 500 ms linear back-off, dead-lettering on. Set
  `DeadLetterEnabled=false` to fall back to the previous redeliver-forever behaviour. The
  dead-letter topic suffix (`.dlt`) is the `Topics.DeadLetterSuffix` constant. DLT messages
  carry `x-dead-letter-reason` / `x-dead-letter-at` headers for diagnostics; replay is a
  manual operator action (re-produce the value onto the source topic).
- **API Gateway (YARP):** single entry point; validates the JWT once and forwards
  `X-User-Id` / `X-User-Role` headers downstream (client-supplied copies are stripped).
  **Authorization source of truth is the JWT itself:** every service independently
  re-validates the bearer token (shared `Jwt:Key`/`Issuer`/`Audience`) and authorizes
  off its claims via `[Authorize]` policies — defense-in-depth, so a service is never
  open even if reached directly. The forwarded headers are a convenience/diagnostic
  signal, **not** a trust boundary; services must not authorize off them. The
  strangler-fig migration is finished: it routes every prefix to its owning service and
  has **no catch-all** (unknown routes return 404).
- **Transactional outbox (Phase 10.3):** to make a state change and its event publish
  atomic, a producer can write an `OutboxMessage` row in the *same* EF transaction as its
  business change (`IOutboxWriter.Enqueue` stages the row; the caller's single
  `SaveChangesAsync` commits both). A per-service `OutboxRelayBackgroundService` then polls
  pending rows (`IOutboxStore`), forwards each stored envelope to Kafka verbatim
  (`IOutboxEventForwarder`), and marks it dispatched — at-least-once with no lost events on
  a crash between DB commit and Kafka produce. Shared building blocks live in
  `BuildingBlocks/Outbox`; **gamification, identity and learning are all fully wired** — each
  has its own `OutboxMessages` table + `AddOutboxMessages` migration, per-service store/writer,
  and relay hosted service, with every outgoing event routed through the outbox. (Gamification
  was the original reference; identity's `user.*` and learning's
  `exercise/lesson/skill.completed` producers were converted in the same way — the enqueue is
  staged before the business `SaveChangesAsync` so state + event commit atomically.) These three
  were the named scope of roadmap 10.3 (the producers whose events drive cross-service state).
  Other producers (social, ai) still publish directly and can adopt the same shared building
  blocks if/when their events need the same guarantee.
- **Data ownership:** the original single `AppDbContext` (42 entities) is split into a
  database per service per [DATA_OWNERSHIP.md](DATA_OWNERSHIP.md); each service owns its
  own schema + EF migrations.

## EF Column Types

| Property | Column type |
|---|---|
| `Skill.ApplicableSalesTypes` | `text[]` |
| `Exercise.SerializedContent` | `jsonb` |
| `UserExerciseAttempt.SerializedAnswer` | `jsonb` |
| `UserExerciseAttempt.SerializedAiFeedback` | `jsonb` |
