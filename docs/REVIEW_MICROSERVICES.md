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
| S4 | 🟠 High | Gateway header-injection trust model (`X-User-Id`/`X-User-Role`) documented but unenforced — all 7 services re-validate JWT and authorize off token claims; only Analytics reads the headers. Two trust models coexist. | `[x]` (docs converged: JWT-in-each-service is the authz source of truth; forwarded headers are defense-in-depth, not a trust boundary) |
| S5 | 🟠 High | Inbound event handling non-atomic + Redis-only idempotency. eventId not persisted in service DB → crash mid-handler causes double-apply on redelivery (notably double XP grant). | `[x]` (gamification XP now DB-idempotent; pattern available for other consumers) |
| S6 | 🟡 Medium | Dual-write (lost-event) risk in 6/7 producers — outbox only implemented in gamification. Documented as roadmap 10.3 `[~]`. | `[~]` (deferred, tracked in roadmap 10.3) |
| S7 | 🟡 Medium | No consumer validates `envelope.Version`; versioning advertised but unimplemented. | `[x]` (EventMessageProcessor dead-letters version > MaxSupportedVersion) |
| S8 | 🟡 Medium | `app_users_online` is a per-instance gauge — wrong under >1 analytics replica (`sum` multiplies). | `[x]` (= AN1; documented max() aggregation + jitter) |
| S9 | 🟡 Medium | Orphaned producer `technique.mastery.changed` has no consumer. | `[x]` (removed — was never published either; dropped from Topics + producer + catalogue) |
| S10 | 🟢 Low | No global exception handler / ProblemDetails in any service or gateway (inconsistent error shape). | `[x]` (ProblemDetails + UseExceptionHandler in all 7 services) |
| S11 | — | CORS `AllowCredentials + AllowAnyHeader` flagged per-service is a **false alarm** — every service uses explicit `WithOrigins(configured)`. Only duplication, not a vuln. | `[~]` (no action; consider shared helper) |

---

## Per-service findings

### identity-service
| # | Sev | Finding | Status |
|---|-----|---------|--------|
| ID1 | 🔴 Critical | `/demo/token` mints production-valid JWTs and is not gated to non-production. | `[x]` (returns 404 in Production) |
| ID2 | 🔴 Critical | No unique index/constraint on `Users.Email` → duplicate-account race + full table scans. | `[x]` (unique index + migration + DbUpdateException→"Email already registered.") |
| ID3 | 🟠 High | No global exception handler — service exceptions leak as raw 500s. | `[x]` (ProblemDetails IExceptionHandler) |
| ID4 | 🟠 High | Refresh-token rotation not atomic (reuse-detection defeated under concurrency); no unique index on token. | `[x]` (atomic ExecuteUpdateAsync + unique index) |
| ID5 | 🟠 High | Refresh tokens stored in plaintext (verification codes are hashed — inconsistent). | `[x]` (SHA-256 hashed at rest) |
| ID6 | 🟠 High | Google login auto-links by email and trusts unverified `payload.Email`. | `[x]` (require EmailVerified; link only to verified accounts) |
| ID7 | 🟡 Medium | HTML email injection via unescaped `displayName`; no rate limiting on login/refresh; admin last-SuperAdmin demotion guard missing; avatar fully buffered in memory. | `[>]` (HTML-escape + last-SuperAdmin guard done; rate-limiting + avatar streaming deferred) |

### learning-service
| # | Sev | Finding | Status |
|---|-----|---------|--------|
| LE1 | 🟠 High | Static thread-unsafe `ChatCache` dictionary — unbounded memory leak + concurrency corruption (should be Redis). | `[x]` (moved to Redis with TTL) |
| LE2 | 🟠 High | Malformed/wrong-typed submission answer → wrong HTTP status (404 / 503 instead of 400). | `[x]` (validation exception → 400) |
| LE3 | 🟠 High | Lesson marked complete on first correct exercise (multi-exercise lessons over-grant). | `[x]` (completes only when all exercises passed) |
| LE4 | 🟠 High | `LessonCompletedEvent` hardcodes `bestScore: 100` regardless of real score. | `[x]` (emits real max score) |
| LE5 | 🟡 Medium | Non-atomic event publish (deferred outbox — S6); N+1 in skill-tree progress; `GetExercisesForLesson` leaks answer-key fields; voice-stream sends 200 before validation. | `[x]` (N+1, answer-key strip, validate-before-200 done; outbox = S6) |

### social-service
| # | Sev | Finding | Status |
|---|-----|---------|--------|
| SO1 | 🔴 Critical | Stored-XSS on anonymous photo `GET` (content-type from extension; mitigated by `nosniff`). | `[x]` (CSP default-src 'none', Content-Disposition, strict type allowlist, reliable header read) |
| SO2 | 🟠 High | No length limits / sanitization on UGC (thread/reply/chat bodies). | `[x]` (MaxLength on thread/reply/chat DTOs) |
| SO3 | 🟠 High | Unbounded chat message growth in a single Mongo document (will hit 16MB BSON cap → hard failure). | `[ ]` (storage restructure deferred; TODO in code) |
| SO4 | 🟠 High | Chat pagination done in-memory after loading whole conversation; `limit` unclamped. | `[x]` (limit clamped 1–100) |
| SO5 | 🟠 High | Reciprocal-friendship race (directional unique index misses `(A,B)`/`(B,A)`). | `[x]` (canonical LEAST/GREATEST unique pair index) |
| SO6 | 🟡 Medium | Vote counter non-atomic drift; admin role from JWT not headers (S4); photo upload not transactional with S3; ILIKE wildcard injection; hot-score candidate cap. | `[x]` (authoritative vote recount, ILIKE escaping, best-effort S3 cleanup; role model = S4) |

### gamification-service
| # | Sev | Finding | Status |
|---|-----|---------|--------|
| GA1 | 🔴 Critical | Event handlers non-atomic + no per-event idempotency → double XP on redelivery (= S5). | `[x]` (DB-level idempotency: `SourceEventId` + unique filtered index + app-level guard) |
| GA2 | 🟠 High | No unique constraint on `UserStreaks.UserId` → duplicate streak rows under concurrency. | `[x]` (unique index + race-tolerant get-or-create) |
| GA3 | 🟠 High | Streak day computed in UTC — breaks streaks for non-UTC users. | `[x]` (configurable StreakTimezone via IStreakClock) |
| GA4 | 🟠 High | No lock on league rollover (cron + admin endpoint race → duplicate next-week leagues). | `[x]` (DisableConcurrentExecution + transaction + unique index) |
| GA5 | 🟠 High | `GET /league` performs writes (lazy create/join/sync) — race + unsafe GET. | `[x]` (idempotent get-or-create/join, unique-violation tolerant) |
| GA6 | 🟡 Medium | Settings getters write-on-read; admin N+1; UTC timestamptz day-bucketing; backdated correction XP. | `[x]` (startup seeding + read-only getters, N+1 removed, Npgsql pinned UTC) |

### notification-service
| # | Sev | Finding | Status |
|---|-----|---------|--------|
| NO1 | 🟠 High | Non-atomic Redis read-modify-write on inbox → lost updates / count drift. | `[x]` (Lua-script atomic mutations) |
| NO2 | 🟠 High | Stored body built from untrusted event fields, no sanitization (XSS depends on frontend escaping). | `[x]` (control/zero-width stripping + ActionUrl whitelist) |
| NO3 | 🟠 High | Duplicate notifications on domain-event replay (idempotency keyed on transport id, not business key). | `[x]` (business-key dedup: recipient+type+relatedEntityId) |
| NO4 | 🟡 Medium | Dead unread-counter (written, never read); full-inbox scan per read; surrogate-pair truncation; no global exception handler. | `[x]` (counter removed, rune-safe truncation, ProblemDetails handler, Redis conn validation) |

### analytics-service
| # | Sev | Finding | Status |
|---|-----|---------|--------|
| AN1 | 🟠 High | `app_users_online` per-instance gauge breaks under scale (= S8). | `[x]` (documented max() aggregation; single source = Redis) |
| AN2 | 🟠 High | Prune+count not atomic; all replicas prune on fixed 20s timer with no jitter. | `[x]` (prune off count path + slower cadence + startup jitter) |
| AN3 | 🟡 Medium | Negative/huge `xp.granted` amount poisons the counter (→ DLQ); no controller/auth integration tests; null-body → 500 not 400; whitelist cardinality untested. | `[x]` (poison guard, 400 on null body, WebApplicationFactory auth tests, cardinality test) |

### ai-service
| # | Sev | Finding | Status |
|---|-----|---------|--------|
| AI1 | 🔴 Critical | Voice usage limit checked before stream, charged after, per wall-clock-second → bypassable (concurrent streams, no per-request cap). | `[x]` (atomic Redis reserve-before-stream + per-stream cap + refund) |
| AI2 | 🟠 High | No input-length cap on any LLM-bound text → unbounded prompt cost. | `[x]` (MaxLength on DTOs + raw-length guard on /ai/evaluate) |
| AI3 | 🟠 High | Prompt injection: untrusted transcript concatenated into grader prompt (`[XP:N]` manipulation, clamped). | `[x]` (system/user role split + fenced data delimiters) |
| AI4 | 🟠 High | Provider API keys/error bodies can leak into logs and HTTP responses. | `[x]` (generic client errors; redacted+truncated server logs) |
| AI5 | 🟠 High | `ParseAiResponse` calls `GetBoolean()/GetInt32()` unguarded → 503 on malformed grader output; rating unclamped. | `[x]` (type-safe extraction + clamp + graceful degrade) |
| AI6 | 🟠 High | No retry/circuit-breaker on LLM calls; `TaskCanceledException` unmapped → 500. | `[x]` (Polly standard resilience + distinct timeout/cancel mapping) |
| AI7 | 🟡 Medium | Lost-update on `Messages` array (`Set` whole array, should `$push`); `/ai/evaluate` anonymous; `f5ai` magic-string routing; streaming `EndOfStream` anti-pattern. | `[x]` (PushEach/Push, service-secret gate, provider enum; EndOfStream noted) |

---

## Remediation order

1. **S1/S2/S3** — contract drift + consumer-side contract tests. ✅ done.
2. **GA1/S5** — XP idempotency (DB-level) — highest correctness risk.
3. **ID1/ID2** — demo-token gate + Email uniqueness — highest auth/data-integrity risk.
4. **AI1/AI2/AI4** — cost control + secret leakage.
5. **SO2/SO3/SO4 + NO1** — UGC limits + atomic inbox.
6. **S4** — converge auth trust model across services.
7. Remaining High/Medium per service.
