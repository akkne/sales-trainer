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

## Phase 3 — Shared User read-model replica `[ ]`
Goal: the pattern that makes database-per-service possible.

- [ ] **3.1** Add a `UserReplica` table + `user.*` consumer to `BuildingBlocks`,
      reusable by every downstream service.
- [ ] **3.2** Wire the replica into the (still-monolithic) remaining features so they
      stop joining Identity tables directly and read the replica instead — proves the
      pattern before further splits.
- [ ] **3.3** Tests: replica seed on `user.registered`, update, delete cascade.

**Commit:** `feat: user read-model replica via kafka`.

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

## Phase 7 — Gamification Service (event-driven core) `[ ]`
Goal: the flagship — XP/streaks/achievements/league, fed entirely by Kafka events.

- [ ] **7.1** Scaffold `gamification-service` + `gamification-db`; migrate
      `UserXpRecords`, `UserStreaks`, `GamificationSettings`, `ExerciseTypeRewards`,
      `StreakMilestones`, `Achievements`, `UserAchievements`, `Leagues`, `LeagueTiers`,
      `LeagueMemberships`, `LeagueSettings`.
- [ ] **7.2** Consume `exercise.completed`, `dialog.evaluated`, `lesson.completed`,
      `skill.completed` → grant XP / update streaks / unlock achievements / update
      league (idempotent, eventual consistency — **no cross-service transaction**).
- [ ] **7.3** Produce `xp.granted`, `achievement.unlocked`, `streak.milestone`,
      `gamification.dialog-weights.updated`.
- [ ] **7.4** Move `StreakResetJob` + `WeeklyLeagueClosureJob` (Hangfire on its own DB);
      expose `/gamification/*`, `/achievements/*`, `/league/*`; flip routes.
- [ ] **7.5** Tests: XP grant from each event type, idempotency/dedupe, streak reset,
      achievement unlock → notification, league rollover; update [DB_SCHEMA.md] + TESTING.

**Commit:** `feat: extract gamification-service (event-driven)`.

---

## Phase 8 — Learning Service `[ ]`
Goal: the last and largest — content + progress; main event producer for Gamification.

- [ ] **8.1** Scaffold `learning-service` + `learning-db`; migrate the Skills/Topics/
      Lessons/Exercises tree, progress records, attempts, `ExerciseTypePrompts`,
      `ReferenceMaterials`, the Technique cluster, `DailyQuotes`.
- [ ] **8.2** Move `SkillTree`, `Lessons`, `Exercises` (non-AI grading + orchestration),
      `Reference`, `Techniques`, `DailyQuotes`; for AI types call AI's `POST /ai/evaluate`.
- [ ] **8.3** Produce `exercise.completed`, `lesson.completed`, `skill.completed`,
      `technique.mastery.changed` (consumed by Gamification/Analytics).
- [ ] **8.4** Flip `/skills/*`, `/lessons/*`, `/exercises/*`, `/reference/*`,
      `/techniques/*`, `/daily-quote`; **the monolith no longer serves any traffic**
      (its code stays as a reference, not deleted).
- [ ] **8.5** Tests: tree/progress, all exercise types (incl. AI path), technique
      mastery, seeding, event emission; update [SKILLS_AND_EXERCISES.md],
      [NEW_EXERCISE_TYPES.md], [SEEDER.md] + TESTING.

**Commit:** `feat: extract learning-service`.

---

## Phase 9 — Admin distribution & monolith retirement `[ ]`
- [ ] **9.1** Move each `Admin` CRUD endpoint into its owning service's `/admin/*`
      (Learning admin, Gamification admin, Identity admin, …); gateway enforces role.
- [ ] **9.2** Point the frontend admin panel at the per-service admin APIs (paths
      unchanged via the gateway).
- [ ] **9.3** **Retire** the monolith: remove it from `docker-compose.*` /
      `scripts/dev-*.sh` and stop deploying it, but **keep `src/backend/api` + its tests
      in the repo as a reference** (do NOT delete). Update [DEPLOYMENT.md],
      [ARCHITECTURE.md], [LOCAL_DEV.md] to mark it reference-only.
- [ ] **9.4** Tests: admin CRUD per service through the gateway; full smoke of all
      flipped routes; update [ADMIN_PANEL.md] + TESTING.

**Commit:** `refactor: retire monolith (kept as reference), finalize microservices`.

---

## Phase 10 — Hardening (optional, resume polish) `[ ]`
- [ ] **10.1** Per-service health checks + Kafka consumer-lag dashboards in Grafana.
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
