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

The verification code is recovered from the recorded email body (`TestCodeExtractor`),
since only its hash is persisted.

## Not covered / manual

- Google OAuth happy-path (needs a real Google ID token) — only the invalid-token path is
  exercised via the controller's 401 mapping.
- Real S3/MinIO avatar GET of a seeded default avatar (needs MinIO) — covered manually;
  the unknown-user 404 path is automated.
- End-to-end gateway flip (gateway → identity) — verified manually with `dev-gateway.sh` +
  `dev-identity.sh`.
