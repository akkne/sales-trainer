# Testing — Core Strategy

## Why tests?

Multiple features were breaking silently. This suite provides a safety net for all backend features.

## Tooling

| Tool | Purpose |
|---|---|
| NUnit 4.2 | Test framework |
| FluentAssertions 6.12 | Readable assertions |
| EF Core InMemory | Unit test DB (no real Postgres needed) |
| Testcontainers.PostgreSql | Real Postgres for integration tests |
| NSubstitute 5 | Mocking (only HttpMessageHandler for FreeText strategy) |
| Microsoft.AspNetCore.Mvc.Testing | WebApplicationFactory for HTTP integration tests |
| Hangfire.InMemory | Replaces Hangfire.PostgreSql in integration test host |

## Project location

```
src/backend/tests/
```

## How to run

```bash
# All tests (requires Docker for integration tests)
dotnet test src/backend/tests

# With verbose output
dotnet test src/backend/tests --verbosity normal
```

## Unit vs Integration split

**Unit tests** — test service business logic in isolation using EF Core InMemory DB.
- No network, no real Postgres, no Docker needed.
- Fast (< 1 sec per test).
- Cover: evaluation strategies, auth logic, exercise submission flow, league rank calculations.

**Integration tests** — test full HTTP stack using a real Postgres container (Testcontainers).
- Require Docker.
- Cover: HTTP status codes, auth policies, cookie handling, array/jsonb column behavior.
- One Postgres container is shared across all integration tests (started once via `[SetUpFixture]`).

## Test count

| Block | Count | Type |
|---|---|---|
| Evaluation Strategies | 8 | Unit |
| AuthenticationService | 10 | Unit |
| ExerciseService | 9 | Unit |
| LeagueService | 6 | Unit |
| Auth Endpoints | 8 | Integration |
| Exercise Endpoints | 9 | Integration |
| Onboarding + SkillTree | 7 | Integration |
| Admin CRUD + Policies | 13 | Integration |
| **Total** | **70** | |

## Notes on cookie tests

The `TestWebApplicationFactory` sets environment to `Testing` (not Development), which makes
auth cookies `Secure=true`. Since the test server uses HTTP, cookies with `Secure` flag cannot
be stored by the cookie container. Refresh/Logout tests seed tokens directly in the DB and pass
the cookie via the `Cookie` request header instead.

## See Also

- [BACKEND_UNIT.md](BACKEND_UNIT.md) — Unit test roadmap
- [BACKEND_INTEGRATION.md](BACKEND_INTEGRATION.md) — Integration test roadmap
- [FRONTEND.md](FRONTEND.md) — Frontend test setup
- [MANUAL_CHECKLISTS.md](MANUAL_CHECKLISTS.md) — Manual test checklists for features
