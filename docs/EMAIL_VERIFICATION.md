# Email Verification by Code

Implemented 2026-06-15. Email/password registration now requires the user to confirm
ownership of their address with a short numeric code before they can log in.

> **Microservices migration:** this flow now runs in the extracted **Identity service**
> (`/auth/*` flipped at the gateway), unchanged. The `EmailVerificationCodes` table moved to
> the Identity service's own `identity-db`. See [IDENTITY_SERVICE.md](IDENTITY_SERVICE.md).

## Flow

1. `POST /auth/register` creates the user with `IsEmailVerified = false`, generates a
   numeric code, stores its hash, and emails the code via MailerSend. It returns
   `RegistrationResultDto {email, requiresEmailVerification: true}` — **no tokens**.
2. The frontend routes to `/verify-email`. The user enters the code.
3. `POST /auth/verify-email {email, code}` validates the code, sets `IsEmailVerified = true`,
   and returns `AuthTokenResponseDto` + the `refreshToken` cookie (same as a login).
4. `POST /auth/resend-code {email}` re-issues a code, subject to a cooldown.
5. `POST /auth/login` returns `403 {requiresEmailVerification: true, email}` for an
   unverified address; the frontend redirects to `/verify-email`.
6. Google sign-in (`/auth/google`) is auto-verified — Google has already proven ownership —
   and logging in via Google also marks a previously-unverified email account verified.

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
- `useRegister` no longer logs the user in — it stores the email and routes to `/verify-email`.
- `useLogin` inspects the typed `ApiError` payload and redirects on `requiresEmailVerification`.

## Tests

See [TESTING/EMAIL_VERIFICATION.md](TESTING/EMAIL_VERIFICATION.md).
