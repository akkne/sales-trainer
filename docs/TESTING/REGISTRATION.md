# Testing — registration and the no-organization gate (Phase 40.37)

What to click through after touching `/auth/register`, `/auth/google`, the
`EMAIL_VERIFICATION_ENABLED` flag, or `AwaitingOrganizationGate`.

Related: [TENANCY/TENANCY.md](../TENANCY/TENANCY.md) §4.1a (why an account is not access),
[EMAIL_VERIFICATION.md](../EMAIL_VERIFICATION.md) (the flag), [ORG_PANEL.md](ORG_PANEL.md).

## Automated

| Suite | Command | Covers |
|---|---|---|
| identity unit | `dotnet test src/backend/identity-service/Identity.Tests/Sellevate.Identity.Tests.csproj --filter "TestCategory!=Integration"` | `AuthenticationServiceSecurityTests` — sign-up and Google both provision without a membership, both flag positions, the unverified-row rejection, login blocked/admitted by the flag |
| identity integration | same project, `--filter "TestCategory=Integration"` — **needs Docker running** (Testcontainers Postgres) | `AuthFlowTests` — `/auth/register` returns a session with `orgId: null`, a second attempt is `409`, and the registered account can log back in |
| frontend | `npm test` in `src/frontend` | `AwaitingOrganizationGate.test.tsx` — who is held, who passes, and that the gate offers a way out |
| analytics | `dotnet test src/backend/analytics-service/Analytics.Tests/*.csproj` | the page-name whitelist still fits under its cardinality cap with `register` and `invite` in it |

## By hand

### Sign-up with the flag off (the default)

1. `/login` → the footer reads «Нет аккаунта? Зарегистрироваться» and links to `/register`.
2. Register a fresh address → you land in onboarding, then on the **waiting screen**, not the skill
   tree: «Ждём приглашение от компании».
3. The screen offers «Написать в поддержку» and «Выйти». Sign out and log back in with the same
   credentials — no verification code is ever asked for, and you return to the same screen.
4. Register the same address again → the form shows an error, not a second account (`409`).

### The gate's exemptions

- A user **with** a membership (accept an invite, or seed one) sees the app normally — the gate must
  be invisible.
- A platform `Admin`/`SuperAdmin` with no membership anywhere sees the app, **not** the waiting
  screen. This is the case that breaks if the gate is written as `!orgId` alone.
- Logged out, `/tree` behaves exactly as before — the gate must never redirect to `/login` itself.

### Google

- Signing in with a Google account nobody has used before **creates** an account and lands on the
  waiting screen. Before 40.37 this was a `401`.
- The account it creates has no password: it can come back only through Google, until an invite lets
  it set one.

### Sign-up with the flag on

Set `EMAIL_VERIFICATION_ENABLED=true` and restart identity.

1. Register → **no** session; the client goes to `/verify-email`.
2. With MailerSend unconfigured (normal in local dev) the code is not emailed — read it from the
   identity service log, where `MailerSendEmailSender` logs the whole message body.
3. Trying to log in before entering the code → `403`, and the client bounces to `/verify-email`.
4. Enter the code → signed in, and back to the waiting screen (verification proves the address, it
   does not grant a membership).
5. Accepting an invite still skips the code entirely, flag or no flag.

## Traps

- **The waiting screen covers `/settings` too**, which is where logout normally lives. That is why
  the screen carries its own «Выйти» — if it is removed, a person waiting for an invitation can
  never sign out.
- The support address on that screen is a placeholder (`support@sellevate.site`); nothing in the
  repo configures a real one.
- A **deactivated** member is not an active one, so an offboarded person now sees the waiting screen
  rather than a dead-end `401`. That is intended.
