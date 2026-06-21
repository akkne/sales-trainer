# MICROSERVICES.md — Target Architecture

> Target-state design for breaking the SalesTrainer/Sellevate .NET monolith
> (`src/backend/api`) into clean, independently deployable microservices.
> The migration plan and atomic tasks live in **[MICROSERVICES_ROADMAP.md](MICROSERVICES_ROADMAP.md)**.
> This file is the **destination**; the roadmap is the **route**.

---

## 1. Goals & principles

1. **Clean service boundaries** — each service owns one bounded context, its own
   data store, and its own deploy lifecycle. No shared database, no cross-service
   foreign keys.
2. **Database-per-service** — every service has its own Postgres DB / Mongo DB /
   Redis instance. The single monolithic `AppDbContext` (~40 entities) is split
   along ownership lines.
3. **Async-first between backends** — backend-to-backend communication goes over
   **Apache Kafka** (events, eventual consistency). Synchronous service-to-service
   REST is the exception, used only for read queries that cannot be event-replicated.
4. **REST for the frontend** — anything the Next.js frontend reads/writes is a
   synchronous REST endpoint, exposed through a single **API Gateway (YARP)**.
5. **Redis-only where consistency does not matter** — transient, high-write,
   loss-tolerant data (notifications inbox, presence/analytics counters) lives in
   Redis with no relational guarantees.
6. **Single source of identity** — `User` is owned by **Identity**. Every other
   service keeps a small local **read-model replica** (`UserId`, `DisplayName`,
   `AvatarKey`) kept in sync via `user.*` Kafka events. Services never reach into
   Identity's DB.

### Tech decisions (locked)

| Decision | Choice | Rationale |
|---|---|---|
| Data isolation | **Database-per-service** | True microservice isolation; strongest resume signal |
| Service stack | **All ASP.NET Core 9 (C#)** | Reuse existing vertical slices; consistent ops |
| Backend↔backend | **Apache Kafka** | Eventual consistency, decoupling, event-driven resume narrative |
| Frontend↔backend | **REST/JSON over API Gateway** | Browser-friendly, streaming-capable |
| API Gateway | **YARP** (Microsoft reverse proxy, standalone service) | Code-visible routing + central JWT validation |
| Simple stores | **Redis** for Notifications & Analytics | No consistency/atomicity needed there |
| AuthN | **JWT** issued by Identity, validated at the Gateway | Single issuer, stateless verification |

---

## 2. Service catalogue (7 services + Gateway)

```
                         ┌──────────────────────────┐
   Next.js frontend ───► │   API Gateway (YARP)     │  JWT validation, routing
                         └──────────┬───────────────┘
            ┌──────────────┬────────┼────────┬──────────────┬───────────────┐
            ▼              ▼        ▼         ▼              ▼               ▼
       ┌─────────┐  ┌───────────┐ ┌──────┐ ┌────────────┐ ┌────────┐ ┌──────────────┐
       │Identity │  │ Learning  │ │ AI   │ │Gamification│ │ Social │ │Notifications │
       │(Postgres)│ │(Postgres) │ │(Mongo│ │ (Postgres) │ │(PG+Mongo)│ │  (Redis)    │
       └─────────┘  └───────────┘ │+APIs)│ └────────────┘ └────────┘ └──────────────┘
                                   └──────┘            ┌──────────────┐
                                                       │  Analytics   │
                                                       │   (Redis)    │
                                                       └──────────────┘
                            ▲                ▲
                            └──── Apache Kafka (event bus) ────┘
```

### 2.0 Repository layout

Every service lives in **its own folder under `src/backend/`**, and **each service
carries its own tests folder next to its code** — there is no shared top-level test
project. The legacy monolith (`src/backend/api`) and its shared `src/backend/tests`
**stay in the repo throughout the migration and are kept as a reference even after
retirement** — services are carved out beside it and routes are flipped at the gateway,
never by deleting monolith code. In Phase 9 the monolith is dropped from deploy/compose
but its source is **not** deleted (kept for behaviour-parity reference).

```
src/backend/
  api/                         ← legacy monolith (kept as reference, not deleted)
  tests/                       ← legacy monolith tests (kept as reference)
  building-blocks/             ← shared lib (event envelope, Kafka base, idempotency,
                                  UserReplica, JWT helpers)
  gateway/
    Gateway/                   ← YARP gateway code
    Gateway.Tests/             ← tests, next to the code
  identity-service/
    Identity/
    Identity.Tests/
  learning-service/
    Learning/
    Learning.Tests/
  ai-service/
    Ai/
    Ai.Tests/
  gamification-service/
    Gamification/
    Gamification.Tests/
  social-service/
    Social/
    Social.Tests/
  notification-service/
    Notification/
    Notification.Tests/
  analytics-service/
    Analytics/
    Analytics.Tests/
```

Rule: a service is self-contained — `<service>/<Name>` (code) + `<service>/<Name>.Tests`
(unit + integration tests for that service only). Cross-service contract tests, if any,
live in `building-blocks`.

### 2.1 Identity Service — `identity-service`
**Bounded context:** who the user is.
- **Absorbs:** `Auth`, `Profile`, `Onboarding`, `Avatars`.
- **Owns (Postgres `identity-db`):** `Users`, `RefreshTokens`, `EmailVerificationCodes`,
  `UserProfiles`, `DefaultAvatars`.
- **External:** Google OAuth, Email (MailerSend), S3/MinIO (avatar blobs).
- **Frontend REST:** `/auth/*`, `/profile/*`, `/onboarding/*`, `/avatars/*`.
- **Kafka (produces):** `user.registered`, `user.updated`, `user.deleted`,
  `user.avatar.changed`.
- **Role:** the only JWT **issuer**. Holds the signing key. Every other service
  trusts JWTs validated at the gateway and keeps a local user replica.

### 2.2 Learning Service — `learning-service`
**Bounded context:** content + the learner's progress through it.
- **Absorbs:** `SkillTree`, `Lessons`, `Exercises` (non-AI grading + orchestration),
  `Reference`, `Techniques`, `DailyQuotes`.
- **Owns (Postgres `learning-db`):** `Skills`, `SkillStages`, `Topics`, `Lessons`,
  `Exercises`, `ExerciseTypePrompts`, `UserSkillProgressRecords`,
  `UserLessonProgressRecords`, `UserExerciseAttempts`, `ReferenceMaterials`,
  `Techniques`, `TechniqueSkills`, `TechniqueCoaches`, `UserTechniqueProgressRecords`,
  `DailyQuotes`.
- **Frontend REST:** `/skills/*`, `/lessons/*`, `/exercises/*`, `/reference/*`,
  `/techniques/*`, `/daily-quote`.
- **Kafka (produces):** `exercise.completed`, `lesson.completed`, `skill.completed`,
  `technique.mastery.changed`.
- **Sync dependency:** for AI-graded exercise types, calls **AI Service** REST and
  awaits the verdict, then persists the attempt and emits `exercise.completed`.

### 2.3 AI Engine Service — `ai-service`
**Bounded context:** everything that talks to an LLM / speech API.
- **Absorbs:** `Dialog` (GPT roleplay), `Voice` (TTS), `Transcription` (Whisper/STT),
  and the **AI evaluation strategies** currently inside `Exercises`.
- **Owns (Mongo `ai-db`):** `dialog_sessions` / `chat_messages` (roleplay transcripts).
  Largely **stateless compute** otherwise; Redis only as a TTS cache.
- **External:** OpenAI GPT, OpenAI Whisper, Google/Yandex/ElevenLabs TTS, Deepgram STT.
- **Frontend REST (streaming):** `/dialog/*`, `/voice/*`, `/transcription/*` —
  `IAsyncEnumerable` streaming preserved through the gateway.
- **Sync REST (for other services):** `POST /ai/evaluate` (called by Learning to
  grade AI exercise types).
- **Kafka (produces):** `dialog.evaluated` (XP-bearing score for a finished roleplay).
- **Config replication:** consumes `gamification.dialog-weights.updated` to cache the
  admin-tuned dialog scoring weights locally (no synchronous read of Gamification).

### 2.4 Gamification Service — `gamification-service`
**Bounded context:** rewards & competition. **The flagship event-driven service.**
- **Absorbs:** `Gamification` (XP, streaks, milestones), `Achievements`, `League`.
- **Owns (Postgres `gamification-db`):** `UserXpRecords`, `UserStreaks`,
  `GamificationSettings`, `ExerciseTypeRewards`, `StreakMilestones`, `Achievements`,
  `UserAchievements`, `Leagues`, `LeagueTiers`, `LeagueMemberships`, `LeagueSettings`.
- **Frontend REST:** `/gamification/*`, `/achievements/*`, `/league/*`.
- **Kafka (consumes):** `exercise.completed`, `dialog.evaluated`, `lesson.completed`,
  `skill.completed` → grants XP, updates streaks, unlocks achievements, updates the
  weekly league. **This is pure eventual consistency** — no atomic cross-service
  transaction with Learning/AI; if an event is late, XP simply lands a moment later.
- **Kafka (produces):** `xp.granted`, `achievement.unlocked`, `streak.milestone`,
  `gamification.dialog-weights.updated`.
- **Background jobs:** `StreakResetJob`, `WeeklyLeagueClosureJob` (Hangfire on its own DB).

### 2.5 Social Service — `social-service`
**Bounded context:** user-to-user interaction.
- **Absorbs:** `Friends`, `Discuss` (forum), `Chat`.
- **Owns (Postgres `social-db`):** `Friendships`, Discuss tables (`DiscussThreads`,
  `DiscussReplies`, `DiscussVotes`, `DiscussTags`, `DiscussPhotos`, …).
  **Owns (Mongo `social-mongo`):** `chat_conversations`.
- **External:** S3/MinIO (Discuss photos).
- **Frontend REST:** `/friends/*`, `/discuss/*`, `/chat/*`.
- **Kafka (produces):** `friend.request.received`, `friend.request.accepted`,
  `chat.message.sent`.
- **Local replica:** keeps `UserId`/`DisplayName`/`AvatarKey` via `user.*` events.

### 2.6 Notifications Service — `notification-service` (Redis-only)
**Bounded context:** the in-app inbox. **Simplest service — no relational store.**
- **Absorbs:** `Notifications`.
- **Owns (Redis):** per-user notification list (`notif:{userId}` → capped list) +
  unread counter (`notif:unread:{userId}`). TTL-based 30-day expiry replaces the
  Hangfire cleanup job. **No consistency or atomicity requirements** — a lost
  notification is acceptable, which is exactly why Redis fits.
- **Frontend REST:** `/notifications/*` (list, unread count, mark-read).
- **Kafka (consumes):** `achievement.unlocked`, `streak.milestone`,
  `friend.request.received`, `friend.request.accepted`, `chat.message.sent`
  → writes an inbox entry. Pure consumer + thin read API.

### 2.7 Analytics Service — `analytics-service` (Redis + Prometheus)
**Bounded context:** product metrics & presence. **Also Redis-only, loss-tolerant.**
- **Absorbs:** `Metrics`, activity tracking, presence.
- **Owns (Redis):** `presence:online` sorted set, visit/event counters; exports to
  **Prometheus** (existing Grafana stack unchanged).
- **Frontend REST:** `POST /tracking/events`, presence ping.
- **Kafka (consumes):** optionally mirrors `user.registered`, `exercise.completed`,
  `xp.granted` for product funnels. No durability guarantees needed.

### 2.8 API Gateway — `gateway` (YARP)
- Single public entry point. Terminates TLS, validates the JWT once, forwards
  `X-User-Id`/`X-User-Role` headers downstream (stripping client copies), and routes by
  path prefix to the services above. **Each service still re-validates the JWT and
  authorizes off its claims** (shared signing key) — the gateway is not the sole
  authorization authority, and the forwarded headers are defense-in-depth, not a trust
  boundary the services rely on. Admin routes (`/admin/*`) fan out to each owning
  service's admin endpoints, which enforce their own `[Authorize]` admin policy.

---

## 3. Admin panel under microservices

The monolith's `Admin` feature was an orchestrator over every other slice. In the
target state there is **no central Admin service**: each service exposes its own
`/admin/*` endpoints for the data it owns (Learning admin for skills/exercises,
Gamification admin for XP economy, etc.). The gateway enforces the `Admin`/`SuperAdmin`
role and routes admin calls to the owning service. The frontend admin panel becomes
a composition of those per-service admin APIs.

---

## 4. Communication contracts

### 4.1 Kafka topics (backend ↔ backend, async)

| Topic | Producer | Consumers | Payload (key fields) |
|---|---|---|---|
| `user.registered` | Identity | all (replica seed) | userId, email, displayName, avatarKey |
| `user.updated` | Identity | all replicas | userId, displayName, avatarKey |
| `user.deleted` | Identity | all (cascade cleanup) | userId |
| `user.avatar.changed` | Identity | Social, Gamification | userId, avatarKey |
| `exercise.completed` | Learning | Gamification, Analytics | userId, exerciseType, score, isCorrect |
| `lesson.completed` | Learning | Gamification | userId, lessonId, bestScore |
| `skill.completed` | Learning | Gamification | userId, skillId |
| `dialog.evaluated` | AI | Gamification | userId, rawScore, criteria |
| `xp.granted` | Gamification | Analytics, League(internal) | userId, amount, source |
| `achievement.unlocked` | Gamification | Notifications | userId, achievementKey, title |
| `streak.milestone` | Gamification | Notifications | userId, dayCount, bonusXp |
| `gamification.dialog-weights.updated` | Gamification | AI | weights snapshot |
| `friend.request.received` | Social | Notifications | recipientId, requesterName |
| `friend.request.accepted` | Social | Notifications | recipientId, accepterName |
| `chat.message.sent` | Social | Notifications | recipientId, senderName, preview |
| `chat.message.read` | Social | Notifications | readerUserId, conversationId, readAt |
| `discuss.reply.created` | Social | Notifications | recipientId, replyAuthorName, threadId, threadTitle, replyId, preview |
| `league.updated` | Gamification | Notifications | userId, leagueId, previousTier, newTier, outcome, rank |

**Conventions:** topic = `<aggregate>.<event>`, partition key = `userId` (ordering
per user), envelope = `{ eventId, occurredAt, type, version, data }`, at-least-once
delivery → **consumers are idempotent** (dedupe on `eventId`).

### 4.2 Synchronous service-to-service REST (kept minimal)

| Caller → Callee | Endpoint | Why sync (not event) |
|---|---|---|
| Learning → AI | `POST /ai/evaluate` | The learner is waiting for the grade in real time |

Everything else that *could* be a synchronous read is instead solved by **local
read-model replicas fed by Kafka**, to keep services independently deployable.

### 4.3 Frontend REST (through the gateway)

The existing public contract in **[API_CONTRACTS.md](API_CONTRACTS.md)** is
preserved verbatim — the gateway maps the same paths to the new services, so the
frontend does not change during the migration.

---

## 5. Data ownership & the "shared User" problem

| Concern | Resolution |
|---|---|
| `User` referenced everywhere | Owned by Identity. Others store a `UserReplica { userId, displayName, avatarKey }` updated via `user.*` events. |
| `UserXp` written by Exercises & Dialog | Moves entirely into **Gamification**, written only by Gamification in reaction to `exercise.completed` / `dialog.evaluated`. |
| `UserStreak` multi-writer | Moves into **Gamification**, single-writer. |
| `UserProfile` written by Onboarding & Profile | Both live in **Identity**; Onboarding becomes an internal flow, not a cross-service writer. |
| `UserLessonProgress` written by Lessons & Exercises | Both live in **Learning**; single owner. |
| Dialog reads Gamification XP weights | AI caches weights from `gamification.dialog-weights.updated`; no live cross-call. |
| League reads UserXp | League is **inside** Gamification — same DB, no cross-service read. |

---

## 6. Cross-cutting concerns

- **Auth:** JWT issued by Identity (15-min access, 30-day refresh cookie). Validated
  centrally at the YARP gateway; downstream services trust forwarded identity headers.
- **Config/secrets:** per-service `.env` + appsettings, same pattern as today
  (see [CONFIGURATION.md](CONFIGURATION.md)).
- **Observability:** each service ships logs to Loki and metrics to Prometheus; the
  existing Grafana stack is reused. Add per-service health endpoints + Kafka
  consumer-lag dashboards.
- **Migrations:** each service runs its own EF Core migrations against its own DB on
  startup (same `db.Database.Migrate()` pattern, scoped per service).
- **Local dev:** infra (Postgres×N or schemas, Mongo, Redis, **Kafka+Zookeeper/KRaft**,
  Loki, Prometheus, Grafana) in Docker; services runnable individually with hot reload
  (extends the existing `scripts/dev-*.sh` model — see [LOCAL_DEV.md](LOCAL_DEV.md)).

---

## 7. Why this split (resume narrative)

- **7 services + gateway** — substantial, mid-level-credible distributed system.
- **Event-driven core** (Kafka) with **idempotent consumers** and **eventual
  consistency** in Gamification — the headline talking point.
- **Database-per-service** with a **replicated User read-model** — demonstrates the
  classic "shared data in microservices" problem solved correctly (no shared DB).
- **Polyglot persistence** — Postgres, Mongo, **Redis-as-primary-store** (Notifications,
  Analytics) deliberately chosen where consistency is not required.
- **API Gateway + central auth** — YARP routing, JWT validation, header propagation.
- **Streaming preserved end-to-end** — AI dialog/voice over `IAsyncEnumerable`.
- **Strangler-fig migration** — incrementally carved from a real monolith, not a
  greenfield toy.
