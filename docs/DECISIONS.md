# Decisions

Non-trivial engineering decisions with their alternatives and rationale. Newest first.

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
