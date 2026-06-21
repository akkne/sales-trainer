# Decisions

Non-trivial engineering decisions with their alternatives and rationale. Newest first.

---

## 2026-06-21 — Phase 3 (Shared User read-model replica) — resolved as satisfied/superseded

- **Context:** [MICROSERVICES_ROADMAP.md](MICROSERVICES_ROADMAP.md) Phase 3 ("Shared User
  read-model replica") was still `[ ]`, but the established database-per-service pattern had
  already realized it by the time the domain services were extracted (Phases 5–8). This entry
  records the per-task verdict so the roadmap reflects reality rather than leaving a phantom
  open phase.

### Per-task verdict

- **3.1 — UserReplica table + `user.*` consumer in BuildingBlocks, reusable by every service →
  Satisfied.** The shared `UserReplica` entity lives in BuildingBlocks since Phase 0.1
  ([src/backend/building-blocks/BuildingBlocks/Identity/UserReplica.cs](../src/backend/building-blocks/BuildingBlocks/Identity/UserReplica.cs)),
  alongside the `user.*` topic constants
  ([Eventing/Topics.cs](../src/backend/building-blocks/BuildingBlocks/Eventing/Topics.cs) lines 17–20)
  and the reusable idempotent consumer base `KafkaConsumerBackgroundService` (Phase 0.4).
  Every extracted domain service keeps **its own** replica table, fed by its own idempotent
  `user.*` consumer (dedupe on `eventId`) plus its own EF config:
  - gamification-service: [Identity/UserReplica.cs](../src/backend/gamification-service/Gamification/Identity/UserReplica.cs),
    [Eventing/UserReplicaConsumer.cs](../src/backend/gamification-service/Gamification/Eventing/UserReplicaConsumer.cs),
    [Infrastructure/Data/UserReplicaEntityConfiguration.cs](../src/backend/gamification-service/Gamification/Infrastructure/Data/UserReplicaEntityConfiguration.cs)
  - ai-service: [Identity/UserReplica.cs](../src/backend/ai-service/Ai/Identity/UserReplica.cs),
    [Eventing/UserReplicaConsumer.cs](../src/backend/ai-service/Ai/Eventing/UserReplicaConsumer.cs),
    [Infrastructure/Data/UserReplicaEntityConfiguration.cs](../src/backend/ai-service/Ai/Infrastructure/Data/UserReplicaEntityConfiguration.cs)
  - social-service: [Identity/UserReplica.cs](../src/backend/social-service/Social/Identity/UserReplica.cs),
    [Eventing/UserReplicaConsumer.cs](../src/backend/social-service/Social/Eventing/UserReplicaConsumer.cs),
    [Infrastructure/Data/UserReplicaEntityConfiguration.cs](../src/backend/social-service/Social/Infrastructure/Data/UserReplicaEntityConfiguration.cs)
  - learning-service: [Identity/UserReplica.cs](../src/backend/learning-service/Learning/Identity/UserReplica.cs),
    [Eventing/UserReplicaConsumer.cs](../src/backend/learning-service/Learning/Eventing/UserReplicaConsumer.cs),
    [Infrastructure/Data/UserReplicaEntityConfiguration.cs](../src/backend/learning-service/Learning/Infrastructure/Data/UserReplicaEntityConfiguration.cs)

  (notification-service and analytics-service are Redis-only with no relational store, so they
  consume `user.*`/funnel events directly and need no `UserReplica` table — consistent with the
  pattern.)

- **3.2 — Wire the replica into the still-monolithic remaining features so they stop joining
  Identity tables → Superseded by Phases 5–8 + Phase 9.** The strangler migration extracted
  **all** domain services, each owning a local replica seeded from `user.*` events, and the
  monolith is being retired in Phase 9 (kept only as reference). There are no remaining
  monolithic features left to "wire onto the replica," so this task is superseded by the actual
  extraction work rather than skipped arbitrarily.

- **3.3 — Tests: replica seed / update / delete → Satisfied per-service.** Each service's replica
  consumer is covered by that service's own test suite; the canonical explicit example is
  [src/backend/social-service/Social.Tests/Unit/UserReplicaConsumerTests.cs](../src/backend/social-service/Social.Tests/Unit/UserReplicaConsumerTests.cs)
  (seed on `user.registered`, idempotent re-seed, update on `user.updated`, delete on
  `user.deleted`).

### Alternative considered

- **A single central User replica service** that every other service queries over REST/gRPC,
  instead of each service holding its own copy. **Rejected:** it reintroduces a synchronous
  cross-service dependency on a shared store — the exact coupling database-per-service exists to
  remove (see [DATA_OWNERSHIP.md](DATA_OWNERSHIP.md)). Database-per-service + a local
  event-fed `UserReplica` per service is the locked decision.

### Reusable-extraction assessment

- Considered extracting a shared `UserReplicaConsumer` base / EF config into BuildingBlocks to
  remove the near-identical per-service consumers. **Not done:** each consumer is bound to its
  own `DbContext` type and its own per-service event DTOs
  (e.g. [gamification IncomingIntegrationEvents.cs](../src/backend/gamification-service/Gamification/Eventing/IncomingIntegrationEvents.cs)),
  so a shared base would require generics over `DbContext` plus shared event contracts, touching
  every service's migrations. That exceeds the "removes real duplication at low risk" bar, so
  this resolution is **documentation-only** (no code extracted).

---

## 2026-06-15 — Email verification by code

### MailerSend as the email provider

- **Decision:** Send the verification email through MailerSend.
- **Why:** EU-hosted (matches the European server), free tier covers low-volume verification
  mail, simple Bearer-token HTTP API, supports sending from a custom verified domain.
- **Alternatives:** Brevo (also EU/free), Amazon SES (cheapest at scale, more setup),
  self-hosted SMTP (rejected — new-IP deliverability is poor). The `IEmailSender` abstraction
  keeps the provider swappable.

### Store codes in Postgres, not Redis

- **Decision:** Persist `EmailVerificationCodes` in Postgres via EF, despite Redis being wired up.
- **Why:** Redis is registered but otherwise unused, with no established pattern; the codebase is
  EF-centric and the integration-test harness runs a real Postgres but only a stub Redis. Postgres
  gives a testable, well-trodden path plus expiry/attempt columns and a Hangfire cleanup job.
- **Trade-off:** Codes need a periodic cleanup job (added) instead of Redis TTL auto-expiry.

### Hash codes; one active code per email

- **Decision:** Store only the SHA-256 hash of the code, replace any prior code on each request,
  cap attempts, and rate-limit resends.
- **Why:** Limits blast radius of a DB read, and the attempt cap + short TTL make a 6-digit code
  safe against brute force. BCrypt was considered overkill for a short-lived single-use OTP.

### Register no longer returns tokens

- **Decision:** `/auth/register` returns `RegistrationResultDto` (verification required) instead of
  an `AuthTokenResponseDto`; tokens are issued by `/auth/verify-email`.
- **Why:** Tokens must not be granted before the address is proven. Google sign-in stays
  auto-verified. Existing users are grandfathered verified by the migration.

---

## 2026-06-12 — Discuss photo attachments

### Single polymorphic `DiscussPhotos` table

- **Decision:** Store thread and reply photos in one `DiscussPhotos` table with an
  `(OwnerType, OwnerId)` polymorphic owner, rather than two separate tables (e.g.
  `DiscussThreadPhotos` + `DiscussReplyPhotos`).
- **Why:** Mirrors the existing `DiscussVotes` shape (`TargetType, TargetId`), so the slice stays
  internally consistent and the upload/list/delete code path is shared.
- **Trade-off:** No DB-level FK to the owner row; orphan cleanup is handled in the service on
  thread/reply delete.

### Two-step create (JSON create + multipart photo sub-resource)

- **Decision:** Keep the existing JSON create endpoints for threads/replies unchanged and add a
  separate multipart photo sub-resource (`POST .../photos`). Alternative considered: switch the
  create endpoints themselves to `multipart/form-data`.
- **Why:** Lowest-risk change — the existing create endpoints and their callers stay untouched.
- **Trade-off:** A post can exist with a failed photo upload. The frontend surfaces this as a
  non-fatal, retryable error rather than discarding the created post.

### Service-level max-10 enforcement (no DB constraint)

- **Decision:** Enforce the 10-photos-per-owner cap in the service, not via a DB constraint.
- **Why:** Matches the slice's existing service-enforced validation style (the same approach
  used elsewhere in Discuss).

### Duplicated image magic-byte validator

- **Decision:** Accept a duplicated `ImageContentValidator` between the Avatars and Discuss slices
  rather than extracting a shared utility now.
- **Why:** Bounded scope; the two slices are independent. A future shared utility is possible if a
  third consumer appears.

### Style note: mirror the existing slice conventions

- **Decision:** New Discuss-photo files intentionally mirror the existing Discuss/Avatars slice
  conventions — `public class` EF configs, `{ get; set; }` + `= null!` entities, `ct` parameter
  name, and inline cache / `nosniff` headers like `AvatarsController` — rather than the strict
  letter of [CODESTYLE.md](CODESTYLE.md).
- **Why:** Keeps the slice internally consistent with the code it lives next to.

## Email notifications

### Shared email transport in BuildingBlocks (not duplicated per service)

- **Decision:** Move the MailerSend email stack (`IEmailSender`, `EmailMessage`,
  `MailerSendEmailSender`, `MailerSendConfiguration`) out of the identity service into
  `Sellevate.BuildingBlocks.Email`, exposed via `AddSellevateEmail()`. Alternative considered:
  copy the sender into the notification service.
- **Why:** Two services now send transactional email (identity verification codes, notification
  emails); one shared implementation avoids divergent MailerSend wiring and config drift.
- **Trade-off:** BuildingBlocks gains an HTTP/email concern, but it already references
  `Microsoft.AspNetCore.App` (so `IHttpClientFactory`/`AddHttpClient` are available).

### Redis user replica in the notification service (no database)

- **Decision:** Resolve a recipient's email/display name from a Redis-backed user replica
  (`notifications:user:{userId}`) fed by `UserReplicaConsumer`, rather than introducing EF/Postgres
  or a synchronous call to identity.
- **Why:** The notification service is deliberately Redis-only; a Redis projection keeps that
  property and matches the `UserReplica` pattern other services use (just without EF).
- **Trade-off:** Eventually consistent — a brand-new user with no replicated email yet is simply
  not emailed (logged, never throws).

### Delayed unread-chat email via a Redis sorted set + watermark

- **Decision:** Implement "email if a message is unread after 5 minutes" with a Redis sorted set
  of pending emails (scored by due time) plus a per-(recipient, conversation) read watermark, polled
  by a background dispatcher. A `chat.message.read` event updates the watermark; the dispatcher
  skips messages read before they came due. Alternative considered: Hangfire delayed jobs.
- **Why:** Keeps the service Redis-only (no Hangfire/DB), and a watermark is simpler and more
  replay-safe than scheduling + cancelling individual jobs.
- **Trade-off:** Delivery is approximate to within one poll interval (default 30s); acceptable for
  a "you missed a message" email.

### OOP email templates (template-method) over inline HTML strings

- **Decision:** Generate notification email HTML inside the notification service via a template
  hierarchy — `NotificationEmailTemplate` (abstract) + per-type subclasses + a shared
  `NotificationEmailLayout` and a `NotificationEmailRenderer` that selects by `NotificationType`.
- **Why:** Adding an email for a new type is one small subclass; the shared, client-safe chrome and
  HTML-encoding live in one place. Matches the request to "use OOP and separate helpers".
- **Trade-off:** More files than a single string builder, but each is small and isolated.
