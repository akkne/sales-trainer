# Testing — Microservices Hardening (Phase 10)

Offline, mocked tests for the cross-cutting hardening added in Phase 10. All run without
Docker/Testcontainers.

## 10.1 Health checks
`BuildingBlocks.Tests/HealthCheckResponseWriterTests.cs`
- Serializes the aggregate status + per-check entries into the shared
  `{ status, checks: [{ name, status }] }` JSON shape.
- With no registered checks, reports `Healthy` and an empty `checks` array (the liveness
  endpoint contract).

`Gateway.Tests/GatewayHostTests.cs`
- `/healthz` returns `200` with `status = "Healthy"` even with no downstream services
  running (liveness probes nothing).

Manual / integration: hit `GET /readyz` on a running service — expect `200` with each
dependency (`postgres`/`redis`/`kafka`/`mongo`) reporting `Healthy`, or `503` with the
failing check named when a dependency is down.

## 10.2 Dead-letter topics + retry policy
`BuildingBlocks.Tests/EventMessageProcessorTests.cs` — drives `EventMessageProcessor`
(the Kafka-agnostic core of the consumer base) with fake idempotency store + dead-letter
publisher:
- Handler succeeds → marks processed, outcome `Commit`, nothing dead-lettered.
- Handler always throws → retried exactly `MaxHandlerRetries + 1` times.
- Retries exhausted → publishes the original raw value to `<topic>.dlt` with the failure
  reason, marks processed, outcome `DeadLettered`.
- `DeadLetterEnabled=false` → outcome `Redeliver`, nothing published, not marked (the
  legacy redeliver-forever path).
- Already-processed `eventId` → handler skipped, outcome `Commit` (idempotency).
- Unparseable JSON → outcome `Commit` without invoking the handler (can't ever succeed).

Config: `ConsumerResilienceSettings` binds from `Kafka:ConsumerResilience`
(`MaxHandlerRetries`, `RetryDelayMilliseconds`, `DeadLetterEnabled`). Defaults: 3 / 500 / true.

## 10.4 Kafka schema contract tests
`BuildingBlocks.Tests/EventContractCatalogTests.cs` asserts that every produced event's
serialized JSON shape (camelCase field names) round-trips into the exact record the
consuming service deserializes, across the full cross-service event catalogue in
[MICROSERVICES.md §4](../MICROSERVICES.md). Per-service outgoing-contract tests
(`*/. ../OutgoingEventContractTests.cs`) remain the producer-side source of truth.

## Running
```
dotnet test src/backend/building-blocks/BuildingBlocks.Tests/Sellevate.BuildingBlocks.Tests.csproj
dotnet test src/backend/gateway/Gateway.Tests/Sellevate.Gateway.Tests.csproj
```
