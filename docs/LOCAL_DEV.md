# Local Dev Profile (app on host, infra in Docker) — DEFAULT for development

This is the **default way to run SalesTrainer during development.** The
full-Docker stack still exists (it's the production/deploy shape) but is no
longer the default for local iteration.

The motivation: rebuilding the `backend` and `frontend` Docker images on every
code change is slow and piles up image/layer cache that clogs the machine's
disk. In this profile the two **app** services run **directly on the host**
(`dotnet run` / `next dev`, both with hot reload), while all **stateful +
observability** services stay in Docker. No image rebuilds, so no cache buildup.

## The two profiles

| | Local Dev (default) | Full Docker (deploy shape) |
|---|---|---|
| Compose file | `docker-compose.infra.yml` | `docker-compose.yml` |
| backend | host — `dotnet run`, port **5001** | Docker (`code-backend-1`) |
| frontend | host — `next dev`, port **3000** | Docker (`code-frontend-1`) |
| postgres / mongo / redis | Docker (same ports & volumes) | Docker |
| loki / prometheus / grafana | Docker | Docker |
| Rebuild on code change | none — hot reload | image rebuild |

Both profiles publish the **same host ports** and share the **same named
volumes**, so data (Postgres, Mongo, …) is identical whichever profile you use.
Run only one profile at a time — they bind the same ports.

## Quick start

```bash
# Start everything (infra in Docker + backend + frontend on host):
scripts/dev-up.sh

# Tail app logs:
tail -f logs/backend.log logs/frontend.log

# Stop everything (host processes + Docker infra):
scripts/dev-down.sh
```

Open http://localhost:3000 (frontend) → talks to http://localhost:5001 (backend).

> If the full-Docker stack was running, stop its app containers first so they
> free ports 5001/3000:
> `docker stop code-backend-1 code-frontend-1`

## Run pieces individually

```bash
scripts/dev-infra.sh      # infra only (docker compose -f docker-compose.infra.yml up -d)
scripts/dev-backend.sh    # backend on host, foreground (Ctrl-C to stop)
scripts/dev-frontend.sh   # frontend on host, foreground
scripts/dev-gateway.sh    # API gateway (YARP) on host, proxying to backend + identity (optional)
scripts/dev-identity.sh   # Identity microservice on host, port 5002 (own identity-db) (optional)
```

> **Identity service (microservices Phase 2).** `scripts/dev-identity.sh` runs the
> extracted Identity service on `http://localhost:5002` with its own Postgres database
> `identity` (auto-created on first start) on the shared local Postgres. With the gateway
> running, `/auth`, `/demo`, `/profile`, `/onboarding`, `/avatars` are proxied to it; the
> monolith serves the rest. See [IDENTITY_SERVICE.md](IDENTITY_SERVICE.md).

## Files added by this profile

| File | Purpose |
|---|---|
| `docker-compose.infra.yml` | Infra-only stack (no backend/frontend). Shares volumes with `docker-compose.yml`. |
| `infrastructure/prometheus/prometheus.local.yml` | Prometheus scrapes the host backend via `host.docker.internal:5001`. |
| `scripts/lib-local-env.sh` | Shared helper: loads root `.env`, exports backend config overrides. Sourced, not run. |
| `scripts/dev-infra.sh` | Start the Docker infra. |
| `scripts/dev-backend.sh` | Run backend on host, pointed at infra on `localhost`. |
| `scripts/dev-frontend.sh` | Run frontend on host (`next dev`); auto-generates `src/frontend/.env.local`. |
| `scripts/dev-gateway.sh` | Run the YARP API gateway on host (port 5000), proxying to the host backend. |
| `scripts/dev-up.sh` | Start infra + backend + frontend (apps backgrounded, logs in `logs/`). |
| `scripts/dev-down.sh` | Stop host apps + Docker infra. |

`src/frontend/.env.local` and `logs/` / `.local-run/` are gitignored.

## How host services reach infra

In Docker, services address each other by name (`postgres`, `mongo`, `loki`).
On the host they use `localhost` + the published port. `scripts/lib-local-env.sh`
injects these as env-var config overrides (same keys docker-compose sets):

| Config key | Value (host) |
|---|---|
| `ConnectionStrings__Postgres` | `Host=localhost;Port=5433;…` |
| `ConnectionStrings__Mongo` | `mongodb://localhost:27017` |
| `ConnectionStrings__Redis` | `localhost:6379` |
| `Kafka__BootstrapServers` | `localhost:9092` |
| `Logging__Loki__Url` | `http://localhost:3100` |
| `ASPNETCORE_URLS` | `http://localhost:5001` |

Secrets (JWT, Google, OpenAI, Deepgram, Yandex, SuperAdmin) come from the root
`.env`, parsed the same way docker-compose reads it.

## Kafka & the API gateway (microservices migration)

Phase 0 of the [microservices migration](MICROSERVICES_ROADMAP.md) added a Kafka
broker and a YARP gateway to the local stack. Both are part of the infra profile;
the gateway runs on the host like the other apps.

| Service | Host address | Notes |
|---|---|---|
| Kafka broker | `localhost:9092` | Single-broker KRaft (no Zookeeper). In-Docker clients use `kafka:29092`. |
| Kafka UI | http://localhost:8085 | Inspect topics, consumer groups, messages. |
| API gateway (YARP) | http://localhost:5000 | Catch-all proxy → backend on `5001`; validates JWT, injects `X-User-*`. |

- `scripts/dev-infra.sh` now starts Kafka + Kafka UI alongside Postgres/Mongo/Redis.
- `scripts/dev-gateway.sh` runs the gateway on the host (after the backend is up).
  It is **optional** during the migration — the monolith still serves the frontend
  directly on `5001` until routes are flipped at the gateway per service.
- The monolith does not yet produce/consume Kafka events; extracted services will.

The event envelope, topic names, idempotency store and the gateway's identity-header
forwarding live in the shared `src/backend/building-blocks` library. See
[ARCHITECTURE.md](ARCHITECTURE.md) and [MICROSERVICES.md](MICROSERVICES.md).

## Gotchas (learned while setting this up)

- **`dotnet run` ignores `ASPNETCORE_URLS`** unless `--no-launch-profile` is
  passed, because `Properties/launchSettings.json`'s `http` profile pins
  `applicationUrl=http://localhost:5188`. `dev-backend.sh` passes the flag, so
  the backend binds **5001** (the port the frontend expects), not 5188.
- **The "Now listening on…" line is suppressed** — Serilog overrides
  `Microsoft` to `Warning`. Don't wait for it in logs; probe
  `http://localhost:5001/swagger/index.html` instead.
- **Root `.env` has an unquoted value with a space**
  (`SUPERADMIN_DISPLAY_NAME=Super Admin`). `lib-local-env.sh` parses line-by-line
  rather than `source`-ing, so this works exactly as it does for compose.
