# TESTING — Microservices Platform Foundations (Phase 0)

Covers the shared `building-blocks` library and the YARP gateway scaffolded in
Phase 0 of the [microservices migration](../MICROSERVICES_ROADMAP.md). The monolith
still serves all traffic, so these are the only new automated tests for Phase 0.

## How to run

```bash
cd src/backend
dotnet test building-blocks/BuildingBlocks.Tests/Sellevate.BuildingBlocks.Tests.csproj
dotnet test gateway/Gateway.Tests/Sellevate.Gateway.Tests.csproj
# or the whole backend solution:
dotnet build Sellevate.sln
```

## Automated tests

### `building-blocks` (`BuildingBlocks.Tests`)
- **EventEnvelope** — `Create` stamps id/time/type/version; `DataAs<T>` round-trips
  the payload; full JSON serialize→deserialize preserves the envelope; blank event
  type is rejected.
- **IdentityHeaders** — `ResolveUserId` reads `sub` and falls back to NameIdentifier;
  `ResolveRole` reads the role claim; both return null for an anonymous principal.

### `gateway` (`Gateway.Tests`)
- **IdentityForwarding** — authenticated request gets `X-User-Id`/`X-User-Role` set
  from claims; **client-supplied identity headers are always stripped** (anti-spoof);
  anonymous request carries no identity headers.
- **GatewayHost** — the gateway boots in-process (JWT + YARP config load) and
  `/healthz` returns `200 { status: "ok", service: "gateway" }` even with no monolith
  running.

## Manual checklist (local stack)

Prerequisite: `scripts/dev-infra.sh` (Kafka + Kafka UI up), `scripts/dev-backend.sh`,
then `scripts/dev-gateway.sh`.

- [ ] Kafka broker reachable on `localhost:9092`; Kafka UI loads at http://localhost:8085
      and shows the `sellevate-local` cluster.
- [ ] `GET http://localhost:5000/healthz` → `200` with `{ "status": "ok" }`.
- [ ] `GET http://localhost:5000/swagger` (or any monolith path) proxies through to the
      backend on `5001` unchanged — same response as hitting `5001` directly.
- [ ] A request to a protected monolith endpoint **with** a valid Bearer token through
      the gateway succeeds (gateway validates + forwards; monolith still authorizes).
- [ ] A request **without** a token to a protected endpoint still returns `401` from the
      monolith (gateway does not block anonymous traffic in Phase 0).
- [ ] Sending a spoofed `X-User-Id` header from the client does not reach the backend
      with that value (stripped by the gateway).

## Notes
- The monolith does not yet produce or consume Kafka events; the publisher /
  idempotent-consumer base are exercised by unit tests and will get integration
  coverage when the first service is extracted (Phase 1+).
