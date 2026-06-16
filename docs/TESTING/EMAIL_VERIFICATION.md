# Testing — Email Verification

Feature: [EMAIL_VERIFICATION.md](../EMAIL_VERIFICATION.md).

## Automated

Backend tests live in the `Sellevate.Tests` project.

- **Unit** — `tests/Unit/EmailVerificationServiceTests.cs`
  - generate stores a single active code and emails it
  - correct code returns true and consumes the code
  - exceeding `MaximumVerificationAttempts` invalidates the code
  - a second request within the cooldown throws `EmailVerificationCooldownException`
- **Unit** — `tests/Unit/AuthenticationServiceTests.cs`
  - register creates an unverified user and sends a code (no tokens)
  - verify with the correct code marks verified and returns a token pair
  - verify with a wrong code throws `UnauthorizedAccessException`
  - login with an unverified email throws `EmailNotVerifiedException`
- **Integration** — `tests/Integration/AuthTests.cs`
  - register returns `requiresEmailVerification` and leaves the user unverified
  - verify-email with the code from the (recording) email sender returns an access token
  - verify-email with a wrong code returns `401`
  - login with an unverified email returns `403` with `requiresEmailVerification`
  - resend-code for an unknown email returns `204` (no enumeration)

Integration tests replace `IEmailSender` with `RecordingEmailSender`
(`tests/Helpers/RecordingEmailSender.cs`), which captures the message so the test can
read back the 6-digit code. No real emails are sent.

Run (unit tests need no Docker; integration tests start a Postgres testcontainer):

```bash
cd src/backend
dotnet test tests/Sellevate.Tests.csproj --filter "FullyQualifiedName~SalesTrainer.Tests.Unit"
dotnet test tests/Sellevate.Tests.csproj   # all, incl. integration (Docker required)
```

## Manual checklist

1. Register with a new email → redirected to `/verify-email`, code arrives (or appears in
   backend logs when MailerSend is unconfigured locally).
2. Wrong code → inline error; correct code → onboarding/tree.
3. Try to log in before verifying → redirected to `/verify-email`.
4. Resend → button shows a countdown; spamming resend yields a `429`.
5. Google sign-in with a brand-new account → no code required.
