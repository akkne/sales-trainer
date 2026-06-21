# MICROSERVICES_ROADMAP.md — Monolith → Microservices Migration

> Phased, strangler-fig migration of `src/backend/api` into the 7-service
> architecture defined in **[MICROSERVICES.md](MICROSERVICES.md)**.
> Status legend (same as the project ROADMAP): `[ ]` todo · `[>]` in progress ·
> `[x]` done · `[~]` blocked (one-line reason) · `[SKIP]` intentionally skipped.
>
> **Decisions locked:** database-per-service · all ASP.NET Core 9 · Apache Kafka
> for backend↔backend · REST for frontend · YARP API Gateway · Redis-only for
> Notifications & Analytics.
>
> ## Branching strategy

Each service is built on **its own development branch**, branched from `main`, and
merged back (PR → squash/merge) only when its phase is complete, green, and documented.
Never commit a service's work directly to `main`.

| Phase | Branch | Merges to |
|---|---|---|
| 0 — Platform | `platform/foundations` | `main` |
| 1 — Analytics | `service/analytics` | `main` |
| 2 — Identity | `service/identity` | `main` |
| 3 — User replica | `platform/user-replica` | `main` |
| 4 — Notifications | `service/notifications` | `main` |
| 5 — Social | `service/social` | `main` |
| 6 — AI Engine | `service/ai` | `main` |
| 7 — Gamification | `service/gamification` | `main` |
| 8 — Learning | `service/learning` | `main` |
| 9 — Admin/retire | `chore/retire-monolith` | `main` |
| 10 — Hardening | `chore/hardening` | `main` |

Rules:
- Branch off the latest `main` at the start of each phase: `git switch -c <branch> main`.
- All of that phase's atomic-task commits land on its branch (Rule #4 still applies —
  one working, tested commit per unit).
- Open a PR when the phase's checklist is fully `[x]` and tests pass; merge to `main`,
  then delete the branch.
- Later phases branch off `main` *after* the previous phase has merged, so each service
  branch starts from the newest shared state.

---

**Order principle:** scaffold the platform first, then extract isolated services
> (low coupling) before the tangled core (Gamification ⇄ Learning ⇄ AI). Each phase
> ends in a **working, committed, tested** state. The monolith keeps serving traffic
> until each service's routes are flipped at the gateway (strangler fig).

---

## Phase 0 — Platform foundations `[x]`
Goal: the scaffolding every service needs, with the monolith still untouched.

- [x] **0.1** Solution layout established: shared `src/backend/building-blocks/BuildingBlocks`
      class library (event envelope, topic constants, Kafka publisher + idempotent-consumer
      base, Redis idempotency store, `UserReplica`, identity-header helpers) with co-located
      `BuildingBlocks.Tests`, plus a backend-wide `src/backend/Sellevate.sln` tying the
      monolith, building-blocks and gateway together. Per-service folders
      (`src/backend/<service>/<Name>` + `.Tests`) are created on each service's own branch
      in its extraction phase (matches the branching strategy) — not pre-created here, so
      the repo stays free of empty shells.
- [x] **0.2** **Kafka** (KRaft, single broker, dual listeners for host + in-network clients)
      + **Kafka UI** added to `docker-compose.infra.yml` and `docker-compose.yml`;
      `scripts/dev-infra.sh` + `lib-local-env.sh` updated (`localhost:9092`, UI on `:8085`).
- [x] **0.3** **Event envelope** (`{ eventId, occurredAt, type, version, data }`, payload as
      opaque `JsonElement`) + topic-name constants (`Topics`) in `BuildingBlocks`.
      Serialization: System.Text.Json (camelCase) now; Avro/Schema-Registry noted as future.
- [x] **0.4** Reusable **idempotent consumer** (`KafkaConsumerBackgroundService`): manual
      commit, per-message scope, check→handle→mark dedupe via `IIdempotencyStore`
      (Redis-backed, TTL'd) — at-least-once with safe retry on handler failure.
- [x] **0.5** **YARP gateway** (`src/backend/gateway/Gateway`) scaffolded: single catch-all
      passthrough route to the monolith (no behaviour change), central JWT validation
      (optional, so public routes pass through), and trusted `X-User-Id`/`X-User-Role`
      injection (client-supplied copies stripped first). Dockerfile + `scripts/dev-gateway.sh`.
- [x] **0.6** Event catalogue already in MICROSERVICES.md §4; added a Kafka + gateway section
      to [LOCAL_DEV.md](LOCAL_DEV.md) and [ARCHITECTURE.md](ARCHITECTURE.md).
- [x] **0.7** `AppDbContext` ownership matrix written: [DATA_OWNERSHIP.md](DATA_OWNERSHIP.md)
      maps all 42 entities → owning service and lists every cross-feature reference to break.
      No code moved.

**Commit checkpoints:** `feat: kafka + building blocks`, `feat: yarp gateway passthrough`.

> **Branch note:** per the user's instruction this foundational work landed directly
> (no `platform/foundations` branch was cut). Subsequent service-extraction phases
> should still follow the per-service branching strategy above.

---

## Phase 1 — Analytics Service (Redis-only) `[x]`
Goal: first real extraction. Most isolated, loss-tolerant, no relational data —
proves the gateway + event pipeline end to end.
See [ANALYTICS_SERVICE.md](ANALYTICS_SERVICE.md) for the implementation writeup.

- [x] **1.1** Scaffolded `src/backend/analytics-service/Analytics` (+ `Analytics.Tests`,
      ASP.NET Core 9) with its own Redis (`analytics-redis`, host port 6380), `/healthz`,
      Dockerfile, `scripts/dev-analytics.sh`, and wiring into `docker-compose.yml` /
      `docker-compose.infra.yml`. No relational/Mongo store.
- [x] **1.2** Moved the `Metrics`/tracking/presence logic; exposes `POST /tracking/events`
      and `POST /tracking/presence/ping`. Prometheus exporter (`/metrics`) + the
      product-metrics Grafana dashboard kept working (dashboard PromQL now matches the
      `sellevate-analytics` job too). Monolith slices left in place as reference.
- [x] **1.3** Consumes `user.registered` / `exercise.completed` / `xp.granted` for funnels
      via the shared idempotent consumer base (dedupe on `eventId`); loss-tolerant.
- [x] **1.4** Flipped `/tracking/*` (events + presence ping) at the gateway to the
      `analytics` cluster; stopped routing that slice to the monolith (its code remains as
      reference).
- [x] **1.5** Tests (NUnit, offline/mocked): presence window math, usage-counter
      increments, funnel event consumption, gateway route-flip config. Updated
      [MONITORING.md](MONITORING.md) + [API_CONTRACTS.md](API_CONTRACTS.md); added
      [ANALYTICS_SERVICE.md](ANALYTICS_SERVICE.md) +
      [docs/TESTING/ANALYTICS_SERVICE.md](TESTING/ANALYTICS_SERVICE.md).

**Commit:** `feat: extract analytics-service (redis)`.

---

## Phase 2 — Identity Service `[x]`
Goal: the identity root — must exist before others can own a User replica.
See [IDENTITY_SERVICE.md](IDENTITY_SERVICE.md) for the implementation writeup.

- [x] **2.1** Scaffolded `src/backend/identity-service/Identity` (+ `Identity.Tests`) with its
      own `IdentityDbContext` and EF migration `InitialIdentitySchema` for `Users`,
      `RefreshTokens`, `EmailVerificationCodes`, `UserProfiles`, `DefaultAvatars`. Owns a
      separate Postgres database `identity` (`DatabaseBootstrapper` creates it on startup).
- [x] **2.2** Moved `Auth`, `Profile`, `Onboarding`, `Avatars` slices; JWT issuance (sole
      issuer, same key/issuer/audience), Google OAuth, MailerSend and S3/MinIO avatar
      storage preserved. The Hangfire cleanup cron became a `BackgroundService`. Monolith
      slices left in place as reference.
- [x] **2.3** Produces `user.registered` (email/Google/super-admin) and `user.avatar.changed`
      (upload/reset) via `IUserEventPublisher` → shared Kafka publisher. `user.updated` /
      `user.deleted` contracts + publisher methods exist but have no trigger yet (no
      rename/delete-account endpoints) — wired for when those land.
- [x] **2.4** Gateway flips `/auth/*`, `/demo/*`, `/profile/*`, `/onboarding/*`, `/avatars/*`
      to the Identity cluster (`http://identity:8080`); the monolith catch-all keeps the
      rest. Gateway already validates the shared JWT key, so tokens stay cross-valid.
- [x] **2.5** Tests: register/login/refresh, email verification, persona/onboarding, avatar
      upload + `user.*` event emission (unit InMemory + integration Testcontainers). Updated
      [EMAIL_VERIFICATION.md](EMAIL_VERIFICATION.md), [API_CONTRACTS.md](API_CONTRACTS.md),
      [docs/TESTING/IDENTITY_SERVICE.md](TESTING/IDENTITY_SERVICE.md).
- [~] **Caveat (2.2/2.4):** `GET /profile` aggregates (streak/XP/skills/score) are owned by
      Gamification/Learning (phases 7 & 8, not extracted yet), so Identity returns them as
      `0` while serving identity fields truthfully — DTO shape unchanged. Composed for real
      once those services exist.

**Commit:** `feat: extract identity-service`.

---

## Phase 3 — Shared User read-model replica `[x]`
Goal: the pattern that makes database-per-service possible.
Resolved as satisfied/superseded — see [DECISIONS.md](DECISIONS.md) (2026-06-21).

- [x] **3.1** Add a `UserReplica` table + `user.*` consumer to `BuildingBlocks`,
      reusable by every downstream service. Done: shared `UserReplica` + `user.*` topics +
      idempotent consumer base live in `BuildingBlocks` (Phase 0.1/0.4); each domain service
      (gamification, ai, social, learning) keeps its own replica table fed by its own
      `UserReplicaConsumer` + EF config (Redis-only services consume directly, no table).
- [~] **3.2** Wire the replica into the (still-monolithic) remaining features so they
      stop joining Identity tables directly and read the replica instead — proves the
      pattern before further splits. **Superseded by Phases 5–8/9:** all domain features were
      extracted into services (each owning a local replica) and the monolith is being retired
      (Phase 9), so no remaining monolithic features exist to wire onto the replica.
- [x] **3.3** Tests: replica seed on `user.registered`, update, delete cascade. Covered
      per-service (canonical: `social-service/Social.Tests/Unit/UserReplicaConsumerTests.cs`).

**Commit:** `docs: resolve Phase 3 user read-model replica (satisfied/superseded)`.

---

## Phase 4 — Notifications Service (Redis-only) `[x]`
Goal: pure Kafka consumer + thin REST; second Redis-as-primary store.
See [NOTIFICATION_SERVICE.md](NOTIFICATION_SERVICE.md) for the implementation writeup.

- [x] **4.1** Scaffolded `src/backend/notification-service/Notification` (+ `Notification.Tests`)
      with Redis as the primary store: per-user capped list `notifications:inbox:{userId}`
      (`LPUSH`/`LTRIM`, default cap 100) + unread counter `notifications:unread:{userId}`,
      both with a 30-day TTL that replaces the monolith's Hangfire `NotificationCleanupJob`
      (no relational DB). Health endpoint, Dockerfile, `scripts/dev-notifications.sh`,
      docker-compose wiring, added to `Sellevate.sln`.
- [x] **4.2** Idempotent consumer (`NotificationEventConsumer`, dedupe on `eventId`) for
      `achievement.unlocked`, `streak.milestone`, `friend.request.received`,
      `friend.request.accepted`, `chat.message.sent`; each maps to a notification written
      to the recipient's Redis inbox via `INotificationEventMapper`. (Producers ship in
      Phases 5/7; consumers idle until then.)
- [x] **4.3** Exposes `/notifications` (list, `?limit=&includeRead=`),
      `/notifications/unread-count`, `/notifications/{id}/read`, `/notifications/read-all`
      — contracts preserved from [API_CONTRACTS.md](API_CONTRACTS.md).
- [x] **4.4** Gateway flips `/notifications/*` to the `notification` cluster; the
      monolith slice stays in `src/backend/api` as reference.
- [x] **4.5** Tests (NUnit, offline): event→inbox write, unread count, mark-read,
      capping, retention/TTL, mapper (incl. unknown/blank → skip), route-flip config.
      Added [NOTIFICATION_SERVICE.md](NOTIFICATION_SERVICE.md) +
      [docs/TESTING/NOTIFICATION_SERVICE.md](TESTING/NOTIFICATION_SERVICE.md); updated
      [NOTIFICATIONS.md](NOTIFICATIONS.md) + [API_CONTRACTS.md](API_CONTRACTS.md).

**Commit:** `feat: extract notification-service (redis)`.

---

## Phase 5 — Social Service `[>]`
Goal: user-to-user features; becomes a notification event producer.
See [SOCIAL_SERVICE.md](SOCIAL_SERVICE.md) for the implementation writeup.

- [x] **5.1** Scaffolded `src/backend/social-service/Social` (+ `Social.Tests`) with its own
      Postgres `social` database (`Friendships`, all Discuss tables; `DatabaseBootstrapper`
      + EF migration `InitialSocialSchema`) and shared Mongo `chat_conversations`. Health
      endpoint, Dockerfile, `scripts/dev-social.sh`, docker-compose wiring, Sellevate.sln
      entries.
- [x] **5.2** Moved `Friends`, `Discuss`, `Chat` slices; uses the local `UserReplica`
      (seeded from `user.*` events) for names; keeps S3/MinIO for Discuss photos. Monolith
      slices left in place as reference.
- [x] **5.3** Produces `friend.request.received`, `friend.request.accepted`,
      `chat.message.sent` (payload shapes match the notification-service consumer contract).
- [x] **5.4** Gateway flips `/friends/*`, `/discuss/*`, `/chat/*`, `/admin/discuss/*` to the
      `social` cluster; the monolith catch-all keeps the rest (its slices remain as reference).
- [x] **5.5** Tests (NUnit, offline/mocked): friend lifecycle, forum CRUD/voting/photos,
      chat, event emission, idempotency, route-flip config. Added
      [SOCIAL_SERVICE.md](SOCIAL_SERVICE.md) + [docs/TESTING/SOCIAL_SERVICE.md](TESTING/SOCIAL_SERVICE.md);
      updated [FRIENDS.md](FRIENDS.md), [DISCUSS.md](DISCUSS.md), [API_CONTRACTS.md](API_CONTRACTS.md),
      [DB_SCHEMA.md](DB_SCHEMA.md).
- [~] **Caveat (5.2):** the friends leaderboard / public-profile / activity-feed aggregates
      (XP, streak, achievement count, average exercise score, recent XP/achievement activity)
      are owned by Gamification/Learning (phases 7 & 8, not extracted yet), so Social returns
      them as `0`/empty while serving identity fields (display name, avatar) truthfully via the
      `UserReplica` — DTO shapes unchanged. Composed for real once those services exist.

**Commit:** `feat: extract social-service`.

---

## Phase 6 — AI Engine Service `[x]`
Goal: isolate all LLM/speech compute; expose the sync grading endpoint Learning needs.
See [AI_SERVICE.md](AI_SERVICE.md) for the implementation writeup.

- [x] **6.1** Scaffolded `src/backend/ai-service/Ai` (+ `Ai.Tests`) with its own Postgres
      `ai` database (`DialogBundles`, `DialogModes`, `UserReplicas`; `DatabaseBootstrapper`
      + EF migration `InitialAiSchema`) and Mongo `dialog_sessions`.
- [x] **6.2** Moved `Dialog`, `Voice`, `Transcription` and the 5 AI **evaluation
      strategies** out of `Exercises`; `IAsyncEnumerable` voice streaming preserved.
      Monolith slices left in place as reference.
- [x] **6.3** Exposes `POST /ai/evaluate` (internal sync endpoint for Learning — the
      system-prompt text is passed in, so `ExerciseTypePrompt` stays owned by Learning).
      Gateway flips `/dialog/*`, `/transcription/*`, `/admin/dialog/*`, `/admin/voice/*`
      to the `ai` cluster (voice routes live under `/dialog/*`).
- [x] **6.4** Produces `dialog.evaluated` on session completion (replacing the direct
      `UserXp` write); consumes `gamification.dialog-weights.updated` to cache scoring
      weights locally (default 25/25/25/25 ×1.0, no live cross-call) and `user.*` to keep
      a local `UserReplica`.
- [x] **6.5** Tests: scoring-weights cache, evaluation factory + spot-mistake local
      scoring, streaming reply parser, sentence chunker (NUnit, offline). Added
      [AI_SERVICE.md](AI_SERVICE.md) + [docs/TESTING/AI_SERVICE.md](TESTING/AI_SERVICE.md).
- [~] **Caveat (6.3):** the dialog admin CRUD/import now takes `skillId` (a `Guid`)
      directly instead of resolving a `skillIconicName` against the `Skills` table
      (owned by Learning) — see [API_CONTRACTS.md](API_CONTRACTS.md).

**Commit:** `feat: extract ai-service`.

---

## Phase 7 — Gamification Service (event-driven core) `[x]`
Goal: the flagship — XP/streaks/achievements/league, fed entirely by Kafka events.
See [GAMIFICATION_SERVICE.md](GAMIFICATION_SERVICE.md) for the implementation writeup.

- [x] **7.1** Scaffolded `src/backend/gamification-service/Gamification` (+ `Gamification.Tests`)
      with its own Postgres `gamification` database (`DatabaseBootstrapper` + EF migration
      `InitialGamificationSchema`) covering `UserXpRecords`, `UserStreaks`,
      `GamificationSettings`, `ExerciseTypeRewards`, `StreakMilestones`, `Achievements`,
      `UserAchievements`, `Leagues`, `LeagueTiers`, `LeagueMemberships`, `LeagueSettings`
      (plus a local `UserReplica` and a `UserLearningProgress` projection). Health endpoint,
      Dockerfile, `scripts/dev-gamification.sh`, docker-compose wiring, `Sellevate.sln` entries.
- [x] **7.2** Consumes `exercise.completed`, `dialog.evaluated`, `lesson.completed`,
      `skill.completed` → grants XP / updates streaks / unlocks achievements / feeds the
      league XP sync. Idempotent (dedupe on `eventId`), eventual consistency, **no
      cross-service transaction**. (Learning producers arrive in Phase 8; consumers wired now.)
- [x] **7.3** Produces `xp.granted` (`userId`/`amount`/`source`), `achievement.unlocked`
      (`userId`/`achievementKey`/`title`), `streak.milestone` (`userId`/`dayCount`/`bonusXp`)
      and `gamification.dialog-weights.updated` (`confidence`/`structure`/`objection`/`goal`/
      `multiplier`, matching the ai-service §6.4 contract). The first three match the
      already-built notification-service contracts verbatim.
- [x] **7.4** Moved `StreakResetJob` + `WeeklyLeagueClosureJob` (Hangfire on the gamification
      DB, same crons). Exposes `/gamification/progress`, `/profile/achievements`, `/league`
      and the `/admin/gamification/*` + `/admin/leagues/*` CRUD; the gateway flips those routes
      to the `gamification` cluster and stops routing them to the monolith (its code stays).
- [x] **7.5** Tests (NUnit, offline/mocked): XP grant from each event type, idempotency/dedupe
      (achievement re-eval + dedup contract), streak reset, achievement unlock → event
      emission, league rollover, outgoing event-contract shapes, and gateway route-flip
      config. Updated [DB_SCHEMA.md](DB_SCHEMA.md) + [API_CONTRACTS.md](API_CONTRACTS.md);
      added [GAMIFICATION_SERVICE.md](GAMIFICATION_SERVICE.md) +
      [docs/TESTING/GAMIFICATION_SERVICE.md](TESTING/GAMIFICATION_SERVICE.md).

**Commit:** `feat: extract gamification-service (event-driven)`.

---

## Phase 8 — Learning Service `[x]`
Goal: the last and largest — content + progress; main event producer for Gamification.
See [LEARNING_SERVICE.md](LEARNING_SERVICE.md) for the implementation writeup.

- [x] **8.1** Scaffolded `src/backend/learning-service/Learning` (+ `Learning.Tests`) with its
      own Postgres `learning` database (`DatabaseBootstrapper` + EF migration
      `InitialLearningSchema`) covering `Skills`, `SkillStages`, `Topics`,
      `UserSkillProgressRecords`, `Lessons`, `Exercises`, `UserLessonProgressRecords`,
      `UserExerciseAttempts`, `ExerciseTypePrompts`, `ReferenceMaterials`, `DailyQuotes`,
      `Techniques`, `TechniqueSkills`, `TechniqueCoaches`, `UserTechniqueProgress` (plus a
      local `UserReplica`). Health endpoint, Dockerfile, `scripts/dev-learning.sh`,
      docker-compose wiring, `Sellevate.sln` entries (port 5008).
- [x] **8.2** Moved `SkillTree`, `Lessons`, `Exercises` (deterministic grading +
      orchestration), `Reference`, `Techniques`, `DailyQuotes`. Deterministic strategies run
      locally; the 5 AI types call ai-service `POST /ai/evaluate`, passing the global
      `ExerciseTypePrompt` text (owned by Learning) + raw content/answer. Monolith slices
      left in place as reference.
- [x] **8.3** Produces `exercise.completed` `{userId,exerciseType,score,isCorrect}`,
      `lesson.completed` `{userId,lessonId,bestScore}`, `skill.completed` `{userId,skillId}`
      (shapes verbatim-matching the gamification-service consumer) and
      `technique.mastery.changed` `{userId,techniqueId,level,masteryPercent}`. Emitted on the
      real grading/completion flow, replacing the monolith's direct XP/streak/achievement writes.
- [x] **8.4** Gateway flips `/skills/*`, `/skills`, `/skill-tree`, `/lessons/*`, `/lessons`,
      `/topics/*`, `/exercises/*`, `/reference/*`, `/reference`, `/techniques/*`,
      `/techniques`, `/daily-quote` and the learning `/admin/*` content routes to the
      `learning` cluster. The monolith catch-all now serves only `/admin/users/*` (admin
      user management — never extracted; Phase 9 must move it, naturally to identity-service,
      before the monolith can be retired). Its code stays as reference. `/profile/*` is not
      captured (owned by identity/gamification).
- [x] **8.5** Tests (NUnit, offline/mocked): deterministic grading, AI grading via mocked
      `/ai/evaluate`, submit event emission, skill-tree progress, technique progress, outgoing
      event-contract shapes, gateway route-flip config. Updated [API_CONTRACTS.md](API_CONTRACTS.md),
      [DB_SCHEMA.md](DB_SCHEMA.md), [SKILLS_AND_EXERCISES.md](SKILLS_AND_EXERCISES.md),
      [NEW_EXERCISE_TYPES.md](NEW_EXERCISE_TYPES.md), [SEEDER.md](SEEDER.md); added
      [LEARNING_SERVICE.md](LEARNING_SERVICE.md) + [docs/TESTING/LEARNING_SERVICE.md](TESTING/LEARNING_SERVICE.md).
- [~] **Caveat (8.2):** `/exercises/{id}/chat` and `/exercises/{id}/voice/stream` (interactive
      `ai_dialogue`) are served by Learning with the OpenAI chat + TTS pipeline ported from the
      monolith to preserve the frontend contract; long-term this LLM/TTS compute belongs behind
      a generic ai-service endpoint (out of Phase 8 scope).
- [~] **Caveat (8.3):** `technique.mastery.changed` has a producer + contract but no trigger
      yet — the monolith's `MarkTechniqueSeen` only records first-seen and never changes mastery
      (behaviour preserved); wired for when a mastery-progression flow lands.

**Commit:** `feat: extract learning-service`.

---

## Phase 9 — Admin distribution & monolith retirement `[x]`
- [x] **9.1** Moved the last admin straggler — `/admin/users/*` (list, detail, rename,
      avatar reset, role change [SuperAdmin]) — into **identity-service** (owns
      Users/Roles), preserving paths + contracts; the service enforces
      `RequireAdmin`/`RequireSuperAdmin` and the gateway injects trusted
      `X-User-Id`/`X-User-Role`. All other admin CRUD was already in its owning service
      from Phases 5–8 (learning/gamification/ai/social). `/admin/voice/usage` was already
      owned by ai-service (Phase 6) — the roadmap's "voice-usage" straggler was a mislabel
      of the already-flipped `/admin/voice/*` route. The monolith's admin controllers stay
      as reference.
- [x] **9.2** Frontend needs **no change**: it calls every `/admin/*` path through the
      gateway base URL (`api.${DOMAIN}` / `localhost:5000`), which preserves paths. Verified
      every frontend API path resolves to a service route (see the inventory below).
- [x] **9.3** Retired the monolith: removed the `monolith` cluster + `{**catch-all}` route
      from the gateway (unknown routes now 404), removed the `backend` service from
      `docker-compose.yml`, pointed Traefik `api.${DOMAIN}` and Prometheus at the gateway,
      and removed the monolith from `dev-up.sh`/`lib-local-env.sh` (`dev-backend.sh` is now a
      `--force`-only launcher for the reference monolith). **`src/backend/api` + its tests stay
      in the repo and the solution.** Updated DEPLOYMENT.md, ARCHITECTURE.md, LOCAL_DEV.md.
- [x] **9.4** Tests: identity-service `/admin/users` CRUD + authz integration tests; gateway
      route-flip test for `identity-admin-users`; a full smoke asserting every flipped
      `/admin/*` route maps to its owning cluster; and assertions that no `monolith` cluster
      or `{**catch-all}` route remains and an unknown route 404s. Updated
      [ADMIN_PANEL.md](ADMIN_PANEL.md), [API_CONTRACTS.md](API_CONTRACTS.md),
      [docs/TESTING/IDENTITY_SERVICE.md](TESTING/IDENTITY_SERVICE.md).

### Route inventory at retirement
Every monolith HTTP route, and the service now serving it through the gateway:

| Route prefix | Owning service (gateway cluster) |
|---|---|
| `/auth/*`, `/demo/*`, `/profile`, `/profile/persona`, `/onboarding`, `/avatars/*`, **`/admin/users/*`** | identity |
| `/skills*`, `/skill-tree`, `/lessons*`, `/topics/*`, `/exercises/*`, `/reference*`, `/techniques*`, `/daily-quote`, `/admin/{skills,skill-stages,topics,lessons,exercises,exercise-type-prompts,reference,techniques,daily-quotes,seeder}/*` | learning |
| `/league`, `/profile/achievements`, `/gamification/*`, `/admin/{gamification,leagues}/*` | gamification |
| `/dialog/*`, `/transcription/*`, `/admin/{dialog,voice}/*` | ai |
| `/friends/*`, `/discuss/*`, `/chat/*`, `/admin/discuss/*` | social |
| `/tracking/*` | analytics |
| `/notifications*` | notification |

**Only straggler still on the monolith catch-all before Phase 9:** `/admin/users/*` →
moved to identity. Everything else had already been flipped in Phases 1–8. After removing
the catch-all, no frontend-used path is left unrouted (cross-checked against the frontend
API client). Unknown routes now return 404.

**Commit:** `refactor: retire monolith (kept as reference), finalize microservices`.

---

## Phase 10 — Hardening (optional, resume polish) `[>]`
- [x] **10.1** Per-service health checks + Kafka consumer-lag dashboards in Grafana.
      Shared `BuildingBlocks.HealthChecks` helpers expose `/healthz` (liveness) + `/readyz`
      (readiness: postgres/redis/kafka/mongo probes per service) uniformly across all 7
      services + gateway. Added a `kafka-exporter` to both compose stacks + a Prometheus
      `kafka-exporter` scrape job, and a Grafana dashboard
      `infrastructure/grafana/dashboards/kafka-consumer-lag.json` (consumer lag by
      group/topic + broker/service health). Docs: [MONITORING.md](MONITORING.md).
- [ ] **10.2** Dead-letter topics + retry policy for poison events.
- [ ] **10.3** Outbox pattern in producers (Learning, Identity, Gamification) for
      atomic DB-write + event-publish.
- [ ] **10.4** Contract tests on Kafka schemas (and optional Schema Registry/Avro).
- [ ] **10.5** k8s manifests / Helm chart per service (stretch).

---

## Cross-phase rules
- Each phase/service has its **own branch** (see Branching strategy above); merge to
  `main` only when the phase is complete and green.
- One commit = one working, tested unit (project Rule #4). Never commit failing tests.
- Update the affected docs in the **same** phase that changes behaviour (Rule #1.4).
- Each service: own folder under `src/backend/`, own DB, own EF migrations, own
  `.env`, own health endpoint, and its **own `*.Tests` project beside its code**.
- All Kafka consumers idempotent (dedupe on `eventId`) — at-least-once delivery.
- Keep [API_CONTRACTS.md](API_CONTRACTS.md) frontend paths stable; the gateway
  preserves them so the frontend never breaks mid-migration.
- If a phase is blocked >2 attempts: mark `[~]` + one-line reason, move on (AGENTS.md).
