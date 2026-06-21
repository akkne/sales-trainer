# Microservices Migration — Code Review & Remediation Tracker

Status legend: `[ ]` open · `[>]` in progress · `[x]` fixed · `[~]` accepted/deferred (with reason)

This document is the durable record of the post-migration code review (7 services +
gateway + BuildingBlocks) and tracks remediation. Findings are grouped by **system-level**
(cross-service) first, then **per-service**. Each item carries a severity and a status.

> Review performed across identity / learning / social / gamification / notification /
> analytics / ai services and the shared BuildingBlocks + gateway. The decomposition itself
> (database-per-service, UserReplica read model, single event envelope, centralized
> messaging/DLQ, completed strangler-fig cutover) is sound — findings are hardening, not redesign.

---

## System-level findings

| # | Sev | Finding | Status |
|---|-----|---------|--------|
| S1 | 🔴 Critical | `exercise.completed` producer↔consumer contract broken: Learning emits `{userId, exerciseType, score, isCorrect}`, Analytics deserialized into `{UserId, ExerciseId}` → `ExerciseId` always `Guid.Empty`. | `[x]` |
| S2 | 🟠 High | Contract tests only validated producer wire-shape (anonymous objects); never deserialized into consumer record types, so S1/S3 passed green. | `[x]` (Analytics incoming-contract tests added; extend to other consumers) |
| S3 | 🟠 High | `xp.granted` consumer dropped the `source` field (Analytics `ExperiencePointsGrantedEvent` missing `Source`). | `[x]` |
| S4 | 🟠 High | Gateway header-injection trust model (`X-User-Id`/`X-User-Role`) documented but unenforced — all 7 services re-validate JWT and authorize off token claims; only Analytics reads the headers. Two trust models coexist. | `[ ]` |
| S5 | 🟠 High | Inbound event handling non-atomic + Redis-only idempotency. eventId not persisted in service DB → crash mid-handler causes double-apply on redelivery (notably double XP grant). | `[ ]` |
| S6 | 🟡 Medium | Dual-write (lost-event) risk in 6/7 producers — outbox only implemented in gamification. Documented as roadmap 10.3 `[~]`. | `[~]` (deferred, tracked) |
| S7 | 🟡 Medium | No consumer validates `envelope.Version`; versioning advertised but unimplemented. | `[ ]` |
| S8 | 🟡 Medium | `app_users_online` is a per-instance gauge — wrong under >1 analytics replica (`sum` multiplies). | `[ ]` |
| S9 | 🟡 Medium | Orphaned producer `technique.mastery.changed` has no consumer. | `[ ]` |
| S10 | 🟢 Low | No global exception handler / ProblemDetails in any service or gateway (inconsistent error shape). | `[ ]` |
| S11 | — | CORS `AllowCredentials + AllowAnyHeader` flagged per-service is a **false alarm** — every service uses explicit `WithOrigins(configured)`. Only duplication, not a vuln. | `[~]` (no action; consider shared helper) |

---

## Per-service findings

### identity-service
| # | Sev | Finding | Status |
|---|-----|---------|--------|
| ID1 | 🔴 Critical | `/demo/token` mints production-valid JWTs and is not gated to non-production. | `[ ]` |
| ID2 | 🔴 Critical | No unique index/constraint on `Users.Email` → duplicate-account race + full table scans. | `[ ]` |
| ID3 | 🟠 High | No global exception handler — service exceptions leak as raw 500s. | `[ ]` |
| ID4 | 🟠 High | Refresh-token rotation not atomic (reuse-detection defeated under concurrency); no unique index on token. | `[ ]` |
| ID5 | 🟠 High | Refresh tokens stored in plaintext (verification codes are hashed — inconsistent). | `[ ]` |
| ID6 | 🟠 High | Google login auto-links by email and trusts unverified `payload.Email`. | `[ ]` |
| ID7 | 🟡 Medium | HTML email injection via unescaped `displayName`; no rate limiting on login/refresh; admin last-SuperAdmin demotion guard missing; avatar fully buffered in memory. | `[ ]` |

### learning-service
| # | Sev | Finding | Status |
|---|-----|---------|--------|
| LE1 | 🟠 High | Static thread-unsafe `ChatCache` dictionary — unbounded memory leak + concurrency corruption (should be Redis). | `[ ]` |
| LE2 | 🟠 High | Malformed/wrong-typed submission answer → wrong HTTP status (404 / 503 instead of 400). | `[ ]` |
| LE3 | 🟠 High | Lesson marked complete on first correct exercise (multi-exercise lessons over-grant). | `[ ]` |
| LE4 | 🟠 High | `LessonCompletedEvent` hardcodes `bestScore: 100` regardless of real score. | `[ ]` |
| LE5 | 🟡 Medium | Non-atomic event publish (deferred outbox — S6); N+1 in skill-tree progress; `GetExercisesForLesson` leaks answer-key fields; voice-stream sends 200 before validation. | `[ ]` |

### social-service
| # | Sev | Finding | Status |
|---|-----|---------|--------|
| SO1 | 🔴 Critical | Stored-XSS on anonymous photo `GET` (content-type from extension; mitigated by `nosniff`). | `[ ]` |
| SO2 | 🟠 High | No length limits / sanitization on UGC (thread/reply/chat bodies). | `[ ]` |
| SO3 | 🟠 High | Unbounded chat message growth in a single Mongo document (will hit 16MB BSON cap → hard failure). | `[ ]` |
| SO4 | 🟠 High | Chat pagination done in-memory after loading whole conversation; `limit` unclamped. | `[ ]` |
| SO5 | 🟠 High | Reciprocal-friendship race (directional unique index misses `(A,B)`/`(B,A)`). | `[ ]` |
| SO6 | 🟡 Medium | Vote counter non-atomic drift; admin role from JWT not headers (S4); photo upload not transactional with S3; ILIKE wildcard injection; hot-score candidate cap. | `[ ]` |

### gamification-service
| # | Sev | Finding | Status |
|---|-----|---------|--------|
| GA1 | 🔴 Critical | Event handlers non-atomic + no per-event idempotency → double XP on redelivery (= S5). | `[ ]` |
| GA2 | 🟠 High | No unique constraint on `UserStreaks.UserId` → duplicate streak rows under concurrency. | `[ ]` |
| GA3 | 🟠 High | Streak day computed in UTC — breaks streaks for non-UTC users. | `[ ]` |
| GA4 | 🟠 High | No lock on league rollover (cron + admin endpoint race → duplicate next-week leagues). | `[ ]` |
| GA5 | 🟠 High | `GET /league` performs writes (lazy create/join/sync) — race + unsafe GET. | `[ ]` |
| GA6 | 🟡 Medium | Settings getters write-on-read; admin N+1; UTC timestamptz day-bucketing; backdated correction XP. | `[ ]` |

### notification-service
| # | Sev | Finding | Status |
|---|-----|---------|--------|
| NO1 | 🟠 High | Non-atomic Redis read-modify-write on inbox → lost updates / count drift. | `[ ]` |
| NO2 | 🟠 High | Stored body built from untrusted event fields, no sanitization (XSS depends on frontend escaping). | `[ ]` |
| NO3 | 🟠 High | Duplicate notifications on domain-event replay (idempotency keyed on transport id, not business key). | `[ ]` |
| NO4 | 🟡 Medium | Dead unread-counter (written, never read); full-inbox scan per read; surrogate-pair truncation; no global exception handler. | `[ ]` |

### analytics-service
| # | Sev | Finding | Status |
|---|-----|---------|--------|
| AN1 | 🟠 High | `app_users_online` per-instance gauge breaks under scale (= S8). | `[ ]` |
| AN2 | 🟠 High | Prune+count not atomic; all replicas prune on fixed 20s timer with no jitter. | `[ ]` |
| AN3 | 🟡 Medium | Negative/huge `xp.granted` amount poisons the counter (→ DLQ); no controller/auth integration tests; null-body → 500 not 400; whitelist cardinality untested. | `[ ]` |

### ai-service
| # | Sev | Finding | Status |
|---|-----|---------|--------|
| AI1 | 🔴 Critical | Voice usage limit checked before stream, charged after, per wall-clock-second → bypassable (concurrent streams, no per-request cap). | `[ ]` |
| AI2 | 🟠 High | No input-length cap on any LLM-bound text → unbounded prompt cost. | `[ ]` |
| AI3 | 🟠 High | Prompt injection: untrusted transcript concatenated into grader prompt (`[XP:N]` manipulation, clamped). | `[ ]` |
| AI4 | 🟠 High | Provider API keys/error bodies can leak into logs and HTTP responses. | `[ ]` |
| AI5 | 🟠 High | `ParseAiResponse` calls `GetBoolean()/GetInt32()` unguarded → 503 on malformed grader output; rating unclamped. | `[ ]` |
| AI6 | 🟠 High | No retry/circuit-breaker on LLM calls; `TaskCanceledException` unmapped → 500. | `[ ]` |
| AI7 | 🟡 Medium | Lost-update on `Messages` array (`Set` whole array, should `$push`); `/ai/evaluate` anonymous; `f5ai` magic-string routing; streaming `EndOfStream` anti-pattern. | `[ ]` |

---

## Remediation order

1. **S1/S2/S3** — contract drift + consumer-side contract tests. ✅ done.
2. **GA1/S5** — XP idempotency (DB-level) — highest correctness risk.
3. **ID1/ID2** — demo-token gate + Email uniqueness — highest auth/data-integrity risk.
4. **AI1/AI2/AI4** — cost control + secret leakage.
5. **SO2/SO3/SO4 + NO1** — UGC limits + atomic inbox.
6. **S4** — converge auth trust model across services.
7. Remaining High/Medium per service.
