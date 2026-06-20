# Identity Service (microservices Phase 2)

Implemented on branch `service/identity`. First **stateful** service extracted from the
monolith per [MICROSERVICES_ROADMAP.md](MICROSERVICES_ROADMAP.md) Phase 2. It is the
identity root of the platform and the **sole JWT issuer**; every other service trusts
JWTs validated at the gateway and keeps a local `UserReplica` fed by the `user.*` events
this service produces.

> Strangler-fig note: the monolith's `Auth`/`Profile`/`Onboarding`/`Avatars` slices are
> **left in place as reference** (project rule — never delete monolith code). Traffic is
> moved by flipping route prefixes at the YARP gateway, not by deleting code.

## Location & shape

```
src/backend/identity-service/
  Identity/                     ← ASP.NET Core 9 service (RootNamespace Sellevate.Identity)
    Program.cs                  ← host: Serilog→Loki, EF/Postgres, JWT, CORS, Kafka producer
    Features/{Auth,Profile,Onboarding,Avatars}/
    Eventing/                   ← user.* integration-event contracts + Kafka publisher
    Infrastructure/{Configuration,Data,Email,Storage}/
    Infrastructure/Data/Migrations/  ← own EF migrations (InitialIdentitySchema)
    Dockerfile                  ← build context = src/backend (needs building-blocks)
  Identity.Tests/               ← unit (InMemory) + integration (Testcontainers Postgres)
```

## Owns (Postgres database `identity-db`)

`Users`, `RefreshTokens`, `EmailVerificationCodes`, `UserProfiles`, `DefaultAvatar`.
The service has **its own database**, separate from the monolith's. `DatabaseBootstrapper`
creates the `identity` database on startup if missing (idempotent), then EF `Migrate()`
builds the schema — so it works against a fresh or an already-populated shared Postgres
instance.

## Frontend REST (unchanged paths, served via the gateway)

`/auth/*`, `/demo/*`, `/profile/*`, `/onboarding/*`, `/avatars/*` — identical request /
response contracts to the monolith (see [API_CONTRACTS.md](API_CONTRACTS.md)). JWT
issuance, Google OAuth, MailerSend email verification and S3/MinIO avatar storage are all
preserved verbatim.

## Kafka events produced (`user.*`)

| Topic | When | Payload |
|---|---|---|
| `user.registered` | new email user, new Google user, super-admin seed | `{userId, email, displayName, avatarKey}` |
| `user.avatar.changed` | avatar upload / reset | `{userId, avatarKey}` (null on reset) |
| `user.updated` | (contract ready; no trigger yet — no rename endpoint exists) | `{userId, displayName, avatarKey}` |
| `user.deleted` | (contract ready; no trigger yet — no delete-account endpoint) | `{userId}` |

Events go through `IUserEventPublisher` → the shared `KafkaEventPublisher` in
`building-blocks`, keyed by `userId` for per-user ordering. Identity only **produces**
events, so it wires the Kafka publisher but no Redis/idempotency store (those are for
consumers).

## Known transitional limitation — `GET /profile` aggregates

The profile-stats DTO includes streak / XP / completed-skill / average-score numbers that
are owned by **Gamification** and **Learning**, which are not extracted yet (roadmap
phases 7 & 8). Until they exist, the Identity service returns those four aggregate fields
as **0** while serving the identity-owned fields (displayName, email, persona, avatarUrl)
truthfully. The DTO shape is unchanged, so the frontend does not break; once Gamification/
Learning are extracted, `GET /profile` composes the real numbers from them. This is called
out in `ProfileService` and in [API_CONTRACTS.md](API_CONTRACTS.md).

## Adaptations vs the monolith slice

- The monolith's Hangfire daily `ExpiredEmailVerificationCleanupJob` became a lightweight
  `ExpiredEmailVerificationCleanupService` (`BackgroundService`, runs on startup + every
  24h) — the Identity service carries no Hangfire dependency.
- `AppDbContext` → a focused `IdentityDbContext` with only the five owned entities.
- Prometheus login/registration counters from the monolith `AuthController` were dropped
  (Analytics owns product metrics; Identity stays lean).

## Running it

- Full Docker stack: `docker compose up --build -d identity gateway` (plus infra).
- Local dev (host, hot reload): `scripts/dev-identity.sh` after `scripts/dev-infra.sh`;
  run `scripts/dev-gateway.sh` too to exercise the flipped routes end to end.
- Direct: `http://localhost:5002` · health: `GET /healthz` · Swagger in Development.

See [LOCAL_DEV.md](LOCAL_DEV.md) for ports and [TESTING/IDENTITY_SERVICE.md](TESTING/IDENTITY_SERVICE.md)
for the test plan.
