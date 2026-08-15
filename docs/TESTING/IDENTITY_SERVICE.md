# Testing — Identity Service

Tests live in `src/backend/identity-service/Identity.Tests` (own test project beside the
service, per the microservices repo layout). Same tooling as the monolith: NUnit +
FluentAssertions + NSubstitute, EF InMemory for unit tests, Testcontainers Postgres for
integration tests.

## How to run

```bash
DOTNET=/usr/local/share/dotnet/dotnet
# Unit only (no Docker needed):
$DOTNET test src/backend/identity-service/Identity.Tests/Sellevate.Identity.Tests.csproj --filter FullyQualifiedName~Unit
# Everything (integration needs a running Docker daemon for Testcontainers):
$DOTNET test src/backend/identity-service/Identity.Tests/Sellevate.Identity.Tests.csproj
```

## Unit tests (`Unit/`, InMemory — no Docker)

| Test | Asserts |
|---|---|
| `DefaultAvatarIndexResolverTests` | deterministic, in-bounds, throws on bad catalog size |
| `OnboardingServiceTests` | creates profile; idempotent once completed |
| `ProfileServiceTests` | identity fields returned; cross-service aggregates zeroed; throws on missing user; persona upsert |
| `EmailVerificationServiceTests` | sends + stores code; cooldown throws; verify succeeds with emailed code, fails with wrong code |
| `AvatarServiceTests` | upload marks Uploaded + emits `user.avatar.changed`; reset clears key + emits null-key event |
| `KafkaUserEventPublisherTests` | maps each domain event to its canonical topic, keyed by `userId` |
| `PasswordAuthProviderTests` (Phase 40.8) | the single `IAuthProvider`: `Method` is `password`; correct password returns the user; wrong password, unknown address and an account with no `PasswordHash` (Google-only) all fail without reaching `BCrypt.Verify` on a null hash |
| `OrganizationAuthConfigurationResolverTests` (Phase 40.8) | login step 2: unknown address → platform default `password`; a known password-organization address returns the *same* method as an unknown one (anti-enumeration); `AllowedEmailDomains` match returns that organization's method, case-insensitively; an unclaimed domain falls back; an **active membership beats the domain** (the invite path); a deactivated membership resolves nothing; an organization with no configuration row falls back to `password` |
| `AuthenticationServiceSecurityTests` (extended 40.8) | an organization configured for `oidc` refuses password login **despite the correct password** and issues no refresh token — the seam disables passwords rather than downgrading to them; `ResolveLoginMethodAsync` still answers `password` for an unknown address |

## Integration tests (`Integration/`, Testcontainers Postgres + `WebApplicationFactory`)

The factory swaps the outbound side-effects for in-memory recorders
(`RecordingEmailSender`, `RecordingUserEventPublisher`) so tests run without MailerSend or
a Kafka broker, while still asserting the right email/`user.*` event would be produced.

| Test | Asserts |
|---|---|
| `AuthFlowTests.Health_ReturnsOk` | `/healthz` |
| `Register_RequiresVerification_SendsEmail_AndEmitsUserRegistered` | 200 + email sent + `user.registered` emitted |
| `Register_Duplicate_ReturnsConflict` | 409 on repeat email |
| `Login_BeforeVerification_IsForbidden_ThenSucceeds_AfterVerify` | 403 → verify with emailed code → login 200 |
| `VerifyEmail_WithWrongCode_IsUnauthorized` | 401 |
| `Refresh_RotatesToken_ViaCookie` | refresh cookie rotation 200 |
| `ProfileAndOnboardingTests` | `/profile` needs auth; onboarding + persona update; invalid persona 400; unknown avatar 404 |
| `AdminUsersTests` (Phase 9, updated 40.6) | `/admin/users` authz (anon 401, User 403, SuperAdmin 200 — whole controller is `RequireSuperAdmin` now, the global `Admin` role is gone); detail 404 for unknown; rename updates name + rejects <2 chars; role change forbidden for a regular User, allowed for SuperAdmin, rejects unknown roles including the removed `"Admin"` value |
| `MembershipClaimsTests` (Phase 40.6) | Login issues `org_id`/`org_role` JWT claims (and mirrors them in `AuthTokenResponseDto`) for a user with an active `Membership`; omits both for a user with no membership; omits both when the only membership is `Deactivated` |
| `LoginMethodFlowTests` (Phase 40.8) | the three-step login end to end, including the real `text[]` domain lookup the in-memory provider cannot exercise: `/auth/login/start` returns `password` for an unknown address; **byte-identical response bodies for a known and an unknown address**; works with no `X-Organization-Id` header (the step exists because there is none yet); `400` for a malformed address; a configured SSO domain returns `oidc`; `/auth/login` is `401` for that organization despite the correct password, and `200` once the same organization is configured for `password`; the configuration table is readable with no tenant context (the property the pre-auth step depends on) |

The verification code is recovered from the recorded email body (`TestCodeExtractor`),
since only its hash is persisted.

## Not covered / manual

- Google OAuth happy-path (needs a real Google ID token) — only the invalid-token path is
  exercised via the controller's 401 mapping.
- Real S3/MinIO avatar GET of a seeded default avatar (needs MinIO) — covered manually;
  the unknown-user 404 path is automated.
- End-to-end gateway flip (gateway → identity) — verified manually with `dev-gateway.sh` +
  `dev-identity.sh`.
