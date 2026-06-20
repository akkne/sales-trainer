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

## Phase 0 — Platform foundations `[ ]`
Goal: the scaffolding every service needs, with the monolith still untouched.

- [ ] **0.1** Create solution layout: every service in its **own folder under
      `src/backend/`** (`src/backend/<service>/<Name>` for code +
      `src/backend/<service>/<Name>.Tests` next to it — no shared test project), plus a
      shared `src/backend/building-blocks` class library (envelope types, Kafka
      producer/consumer base, idempotency store, user-replica entity, JWT helpers).
      See MICROSERVICES.md §2.0 for the exact tree.
- [ ] **0.2** Add **Kafka** (KRaft, single broker) to `docker-compose.infra.yml`
      + `scripts/dev-infra.sh`; add Kafka UI for local debugging.
- [ ] **0.3** Define the **event envelope** + topic-name constants in `BuildingBlocks`
      (`{ eventId, occurredAt, type, version, data }`); pick serialization (JSON now,
      note Avro/Schema-Registry as future).
- [ ] **0.4** Implement a reusable **idempotent consumer** (dedupe on `eventId`,
      per-service `processed_events` table or Redis set).
- [ ] **0.5** Scaffold the **YARP gateway** service: passthrough route to the existing
      monolith for ALL paths (no behaviour change yet), central JWT validation,
      `X-User-Id`/`X-User-Role` header injection.
- [ ] **0.6** Document the event catalogue + envelope in MICROSERVICES.md §4 (keep in
      sync); add a Kafka section to [LOCAL_DEV.md](LOCAL_DEV.md) and
      [ARCHITECTURE.md](ARCHITECTURE.md).
- [ ] **0.7** Decompose the single `AppDbContext`: map each entity → owning service
      (table in MICROSERVICES.md §2 / §5). No code move yet — just the ownership matrix
      + identify every cross-feature reference to break.

**Commit checkpoints:** `feat: kafka + building blocks`, `feat: yarp gateway passthrough`.

---

## Phase 1 — Analytics Service (Redis-only) `[ ]`
Goal: first real extraction. Most isolated, loss-tolerant, no relational data —
proves the gateway + event pipeline end to end.

- [ ] **1.1** Scaffold `analytics-service` (ASP.NET Core 9) with its own Redis.
- [ ] **1.2** Move `Metrics`/tracking/presence logic; expose `POST /tracking/events`,
      presence ping; keep Prometheus exporter + existing Grafana dashboard wiring.
- [ ] **1.3** Consume `user.registered` / `exercise.completed` / `xp.granted` for
      funnels (idempotent; optional, loss-tolerant).
- [ ] **1.4** Flip `/tracking/*` + presence routes at the gateway to the new service;
      stop routing to the monolith's slice (leave its code in place as reference).
- [ ] **1.5** Tests: presence window, counter increments, event consumption, route
      flip; update [MONITORING.md](MONITORING.md) + docs/TESTING.

**Commit:** `feat: extract analytics-service (redis)`.

---

## Phase 2 — Identity Service `[ ]`
Goal: the identity root — must exist before others can own a User replica.

- [ ] **2.1** Scaffold `identity-service` + `identity-db` (Postgres); migrate
      `Users`, `RefreshTokens`, `EmailVerificationCodes`, `UserProfiles`, `DefaultAvatars`.
- [ ] **2.2** Move `Auth`, `Profile`, `Onboarding`, `Avatars` slices; preserve JWT
      issuance (sole issuer) + Google OAuth + MailerSend + S3 avatar storage.
- [ ] **2.3** Produce `user.registered` / `user.updated` / `user.deleted` /
      `user.avatar.changed` on the relevant flows.
- [ ] **2.4** Gateway validates JWT against Identity's signing key; flip `/auth/*`,
      `/profile/*`, `/onboarding/*`, `/avatars/*`; stop routing to the monolith's
      slices (leave their code in place as reference).
- [ ] **2.5** Tests: register/login/refresh, email verification, OAuth, avatar upload,
      event emission; update [EMAIL_VERIFICATION.md], [API_CONTRACTS.md], docs/TESTING.

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

## Phase 4 — Notifications Service (Redis-only) `[ ]`
Goal: pure Kafka consumer + thin REST; second Redis-as-primary store.

- [ ] **4.1** Scaffold `notification-service` with Redis (per-user capped list +
      unread counter, 30-day TTL replacing the Hangfire cleanup job).
- [ ] **4.2** Consume `achievement.unlocked`, `streak.milestone`,
      `friend.request.received`, `friend.request.accepted`, `chat.message.sent`
      (idempotent).
- [ ] **4.3** Expose `/notifications/*` (list, unread count, mark-read).
- [ ] **4.4** Flip routes at the gateway; stop routing to the monolith's slice
      (leave its code in place as reference).
- [ ] **4.5** Tests: event→inbox write, unread count, mark-read, TTL expiry; update
      [NOTIFICATIONS.md] + docs/TESTING.

**Commit:** `feat: extract notification-service (redis)`.

---

## Phase 5 — Social Service `[ ]`
Goal: user-to-user features; becomes a notification event producer.

- [ ] **5.1** Scaffold `social-service` + `social-db` (Postgres) + `social-mongo`
      (chat); migrate `Friendships`, all Discuss tables, `chat_conversations`.
- [ ] **5.2** Move `Friends`, `Discuss`, `Chat` slices; use the `UserReplica` for
      names/avatars; keep S3 for Discuss photos.
- [ ] **5.3** Produce `friend.request.received/accepted`, `chat.message.sent`
      (consumed by Notifications).
- [ ] **5.4** Flip `/friends/*`, `/discuss/*`, `/chat/*`; stop routing to the monolith's
      slices (leave their code in place as reference).
- [ ] **5.5** Tests: friend lifecycle, forum CRUD/voting/photos, chat, event emission;
      update [FRIENDS.md], [DISCUSS.md] + docs/TESTING.

**Commit:** `feat: extract social-service`.

---

## Phase 6 — AI Engine Service `[ ]`
Goal: isolate all LLM/speech compute; expose the sync grading endpoint Learning needs.

- [ ] **6.1** Scaffold `ai-service` + `ai-db` (Mongo for `dialog_sessions`/transcripts).
- [ ] **6.2** Move `Dialog`, `Voice`, `Transcription` + the AI **evaluation strategies**
      out of `Exercises`; preserve `IAsyncEnumerable` streaming through the gateway.
- [ ] **6.3** Expose `POST /ai/evaluate` (sync, for Learning) + flip `/dialog/*`,
      `/voice/*`, `/transcription/*` frontend routes.
- [ ] **6.4** Produce `dialog.evaluated`; consume `gamification.dialog-weights.updated`
      to cache scoring weights locally (no live cross-call).
- [ ] **6.5** Tests: dialog turn + streaming, TTS cache, transcription, evaluate
      contract, weight-cache refresh; update [AI_DIALOG.md], [VOICE_ROLEPLAY.md] + TESTING.

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
