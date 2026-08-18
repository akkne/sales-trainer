# Email Verification by Code

> ⚠️ **SUPERSEDED BY THE INVITE FLOW (Phase 40.7, 2026-08-15).**
> There is no public registration any more — `POST /auth/register` is deleted — so nothing
> reaches this flow on the normal path. **The invite replaces email verification:** possession
> of the single-use invite token already proves control of the address, so
> `POST /auth/invites/{token}/accept` creates the user with `IsEmailVerified = true` and sends
> no code at all. See [TENANCY/TENANCY.md](TENANCY/TENANCY.md) section 4.3 and the
> "Invites & memberships" section of [API_CONTRACTS.md](API_CONTRACTS.md).
>
> The code endpoints (`/auth/verify-email`, `/auth/resend-code`), the
> `EmailVerificationCodes` table and `EmailVerificationService` all remain in the codebase and
> still work — they cover accounts created before invites existed, and any future flow that
> needs to re-prove an address (an email change, say). `POST /auth/login` does not block
> unverified accounts.
>
> Everything below describes that retained mechanism.

Implemented 2026-06-15. Confirming ownership of an address with a short numeric code.

> **Microservices migration:** this flow now runs in the extracted **Identity service**
> (`/auth/*` flipped at the gateway), unchanged. The `EmailVerificationCodes` table moved to
> the Identity service's own `identity-db`. See [IDENTITY_SERVICE.md](IDENTITY_SERVICE.md).

## How an address is proven today (40.7)

1. An `OrgAdmin` creates an invite (`POST /invites`). Only the SHA-256 hash of the single-use
   token is stored; the raw token goes out in the email through the same MailerSend transport
   described below, and degrades to a log line in local dev the same way.
2. The invitee opens `/invite/{token}` and posts to `POST /auth/invites/{token}/accept`.
3. The account is created with `IsEmailVerified = true` and an active membership, and the
   response is a normal `AuthTokenResponseDto` — **no code is ever generated or sent**. The
   token arrived in the invitee's mailbox, which is exactly what a verification code exists to
   demonstrate.
4. Google sign-in (`/auth/google`) is auto-verified — Google has already proven ownership — but
   since 40.7 it only works for an address that already has an account *and* an active
   membership. It never creates one.

## Retained code flow

1. `POST /auth/verify-email {email, code}` validates the code, sets `IsEmailVerified = true`,
   and returns `AuthTokenResponseDto` + the `refreshToken` cookie (same as a login).
2. `POST /auth/resend-code {email}` re-issues a code, subject to a cooldown.
3. Nothing generates a code on the normal path any more, because the route that used to
   (`/auth/register`) no longer exists.

Existing users created before this feature were backfilled to `IsEmailVerified = true`
by the migration, so nobody is locked out.

## Code storage & security

- Codes live in the Postgres `EmailVerificationCodes` table (not Redis — see
  [DECISIONS.md](DECISIONS.md)), one active row per email (a new request replaces the old).
- Only the **SHA-256 hash** of the code is stored; comparison is constant-time.
- Each code has `ExpiresAt` (default 10 min) and an `AttemptCount`. After
  `MaximumVerificationAttempts` (default 5) wrong tries the code is invalidated.
- Resend is rate-limited by `ResendCooldownSeconds` (default 60); during cooldown
  `GenerateAndSendCodeAsync` throws `EmailVerificationCooldownException` → `429` with
  a `Retry-After` header and `{retryAfterSeconds}`.
- `/auth/resend-code` is silent (204) for unknown or already-verified emails to avoid
  account enumeration.
- A daily Hangfire job `expired-email-verification-cleanup` purges expired rows.

## Backend layout

| Concern | Location |
|---------|----------|
| Verification logic | `Features/Auth/Services/{Abstract,Implementation}/*EmailVerificationService*` |
| Auth flow integration | `Features/Auth/Services/Implementation/AuthenticationService.cs` |
| Endpoints | `Features/Auth/AuthController.cs` |
| Code entity | `Features/Auth/Models/EmailVerificationCode.cs` + `Infrastructure/Data/EmailVerificationCodeEntityConfiguration.cs` |
| Exceptions | `Features/Auth/Exceptions/{EmailNotVerifiedException,EmailVerificationCooldownException}.cs` |
| Email transport | `Infrastructure/Email/**` (`IEmailSender` → `MailerSendEmailSender`) |
| Config | `Infrastructure/Configuration/{MailerSendConfiguration,EmailVerificationConfiguration}.cs` |
| Cleanup job | `Features/Auth/ExpiredEmailVerificationCleanupJob.cs` |

## Email transport (MailerSend)

`IEmailSender`/`MailerSendEmailSender` POST to `{BaseUrl}/v1/email` with a Bearer token.
When the API token is unset (placeholder), the sender **logs the message instead of sending**,
so local dev works without an account — the code appears in the backend logs. See
[INTEGRATIONS.md](INTEGRATIONS.md#mailersend-transactional-email) for setup and
[CONFIGURATION.md](CONFIGURATION.md) for the env keys.

## Frontend

- `/verify-email` page reads the pending email from `sessionStorage`, takes the code,
  and calls `useVerifyEmail`; `useResendVerificationCode` drives the resend button (with a
  visible cooldown countdown).
- `useLogin` inspects the typed `ApiError` payload and redirects on `requiresEmailVerification`.
- There is no `useRegister` hook any more. `useAcceptInvite(token)` on `/invite/[token]` is what
  creates an account, and it skips `/verify-email` entirely.

## Tests

See [TESTING/EMAIL_VERIFICATION.md](TESTING/EMAIL_VERIFICATION.md).
