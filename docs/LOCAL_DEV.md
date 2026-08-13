# Local Dev Profile (app on host, infra in Docker) — DEFAULT for development

> **Microservices note (Phase 9):** the monolith (`src/backend/api`) is **retired**.
> It is no longer part of the dev stack — `scripts/dev-up.sh` only starts the frontend
> + infra, and the per-service backends + gateway are run individually
> (`scripts/dev-gateway.sh`, `scripts/dev-identity.sh`, `scripts/dev-learning.sh`, …).
> The frontend talks to the gateway (port **5000**), which routes each path to its
> owning service. `scripts/dev-backend.sh` now only launches the reference monolith on
> demand (`--force`); the references to "backend"/port 5001 below are that retired
> monolith.

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

## Running the full stack in Docker on this machine

`docker-compose.yml` on its own is the **server** shape and does not come up on a
Mac: it publishes the gateway on `:5000` (macOS ControlCenter holds that port for
AirPlay Receiver) and builds the frontend against the production API. Overlay
`docker-compose.local.yml` to fix exactly those two things:

```bash
scripts/docker-local-up.sh     # build sequentially + up -d
scripts/docker-local-down.sh   # stop, keep volumes
```

which is a wrapper around:

```bash
docker compose -f docker-compose.yml -f docker-compose.local.yml up -d --build
```

| | Deploy (`docker-compose.yml`) | + `docker-compose.local.yml` |
|---|---|---|
| Gateway host port | `5000` | **`5001`** (override `GATEWAY_HOST_PORT`) |
| Frontend `NEXT_PUBLIC_API_URL` | `https://api.sellevate.site` (from `src/frontend/.env.production`) | **`http://localhost:5001`** (build arg) |
| NuGet packages | downloaded from nuget.org | **host `~/.nuget/packages`** mounted in (override `NUGET_CACHE_DIR`) |

The NuGet cache is wired through an overridable build stage: each service
Dockerfile declares `FROM scratch AS nugetcache` (empty, so the deploy build is
unchanged) and mounts it as a NuGet fallback folder for `restore` and `publish`.
The overlay replaces that stage via `additional_contexts` — a named build context
wins over a stage of the same name.

The API URL is baked into the client bundle at **build time**, so switching it
requires rebuilding the `frontend` image — a restart is not enough. The frontend
Dockerfile writes `.env.production.local` (higher priority than `.env.production`)
only when the `NEXT_PUBLIC_API_URL` build arg is non-empty, so the deploy build is
unaffected.

`ports:` entries **merge by appending** across compose files, so the override uses
the `!override` tag; without it the gateway would still try to bind the
unavailable `:5000` as well.

### Three things that break a cold build on a Mac

**1. `exit code 132` / `Illegal instruction` — the .NET JIT, not your machine.**
On arm64 under Apple's Virtualization Framework the JIT in the **glibc** .NET SDK
image dies with SIGILL. It surfaces as `Illegal instruction` during `dotnet restore`
or `csc.dll exited with code 132` during `dotnet publish`. Reproduce it bare:

```bash
docker run --rm mcr.microsoft.com/dotnet/sdk:9.0 sh -c \
  'cd /tmp && dotnet new console -o a >/dev/null && cd a && dotnet build -v q; echo rc=$?'
# rc=132
```

It is **probabilistic, and scales with project size** — which makes it easy to
misdiagnose. Small services often squeeze through (sometimes on the 3rd attempt),
while `learning` failed 5/5 runs. Measured on `learning`:

| Build stage image | extra flags | result |
|---|---|---|
| `sdk:9.0` (glibc) | none | fails |
| `sdk:9.0` | `DOTNET_EnableWriteXorExecute=0` | fails |
| `sdk:9.0` | `+ /p:UseSharedCompilation=false` | fails |
| `sdk:9.0` | `+ DOTNET_TieredCompilation=0 DOTNET_TieredPGO=0` | 1/4 runs — luck, not a fix |
| **`sdk:9.0-alpine` (musl)** | none | **4/4 clean** |

So the fix is the SDK image, not a JIT flag. Each Dockerfile takes
`ARG SDK_IMAGE` (glibc by default, unchanged for deploys) and the overlay switches
it to `-alpine`. The published output is portable IL — no `-r`, and `runtimes/`
carries every RID's native assets — so the glibc runtime image still runs it.

> Careful with `DOTNET_EnableWriteXorExecute=0`: on a *trivial* project it flips
> `rc=132` to `rc=0`, which looks like a fix. On a real one it changes nothing. A
> single green run proves nothing here — repeat it before believing it.

**The Alpine switch is a workaround, not the cure.** The same SIGILL hits the JVM:
the Kafka container dies on startup with

```
SIGILL (0x4) at pc=…  JRE 21.0.4  linux-aarch64
Problematic frame: j java.lang.System.registerNatives()V+0
```

Two unrelated JIT runtimes crashing the same way points at the VM, not at .NET or
Kafka. Observed on **macOS 26.3.1 / Apple M4 Pro / Docker Desktop 4.37.2** — a
Docker Desktop that predates both the OS and the CPU generation it is running on.
**Fix: update Docker Desktop.** Until then Kafka (and `kafka-exporter`) will not
start on this machine; everything else runs, but cross-service events do not flow.

**2. A slow link makes `dotnet restore` fail.** It aborts after 60 s without data
(`The operation has timed out`, `Received an unexpected EOF`). A cold build pulls
hundreds of MB per service, so on a ~100 KB/s link it never finishes. The host's
`~/.nuget/packages` is mounted in as a NuGet fallback folder instead — see the
compose overlay above. With it, `restore` completes in ~25 s with no network at all.

**3. Resizing the Docker VM restarts the daemon**, killing any in-flight build with
`failed to receive status: rpc error … EOF`. Set memory first, then build. Give the
VM ~8 GiB: this is 20 containers, and Kafka alone wants ~1 GiB.

The build is also run **one service at a time** — `docker compose build` otherwise
starts all nine concurrently, and nine parallel `dotnet publish` runs are enough to
exhaust a modest VM.

## Run pieces individually

```bash
scripts/dev-infra.sh      # infra only (docker compose -f docker-compose.infra.yml up -d)
scripts/dev-backend.sh    # backend on host, foreground (Ctrl-C to stop)
scripts/dev-frontend.sh   # frontend on host, foreground
scripts/dev-gateway.sh    # API gateway (YARP) on host, proxying to backend + identity (optional)
scripts/dev-identity.sh   # Identity microservice on host, port 5002 (own identity-db) (optional)
scripts/dev-notifications.sh # Notification microservice on host, port 5004 (Redis-only) (optional)
scripts/dev-analytics.sh  # Analytics microservice on host, port 5005 (own analytics-redis on 6380) (optional)
scripts/dev-company.sh    # Company microservice on host, port 5009 (own company database + Kafka producer, no Redis/Mongo) (optional)
```

> **Identity service (microservices Phase 2).** `scripts/dev-identity.sh` runs the
> extracted Identity service on `http://localhost:5002` with its own Postgres database
> `identity` (auto-created on first start) on the shared local Postgres. With the gateway
> running, `/auth`, `/demo`, `/profile`, `/onboarding`, `/avatars` are proxied to it; the
> monolith serves the rest. See [IDENTITY_SERVICE.md](IDENTITY_SERVICE.md).

> **Notification service (microservices Phase 4).** `scripts/dev-notifications.sh` runs the
> extracted Notification service on `http://localhost:5004`, backed only by the shared local
> Redis (no relational database). With the gateway running, `/notifications` and
> `/notifications/*` are proxied to it; the monolith serves the rest. See
> [NOTIFICATION_SERVICE.md](NOTIFICATION_SERVICE.md).

> **Analytics service (microservices Phase 1).** `scripts/dev-analytics.sh` runs the
> extracted Analytics service on `http://localhost:5005`, backed only by its own local Redis
> (`analytics-redis` on port 6380, separate from the shared Redis). With the gateway running,
> `/tracking/*` is proxied to it; the monolith serves the rest. It owns the product Prometheus
> metrics (`/metrics`). See [ANALYTICS_SERVICE.md](ANALYTICS_SERVICE.md).

> **Company service (Phase 39, Companies feature).** `scripts/dev-company.sh` runs the
> Company service on `http://localhost:5009` with its own Postgres database `company`
> (auto-created on first start) on the shared local Postgres. It has no Redis or Mongo
> dependency. Since Phase 39.11 it produces `company.followup.due` on Kafka (a polling
> reminder background service) — the shared local Kafka broker must be running
> (`scripts/dev-infra.sh`) for reminders to be delivered; a broker outage is logged and
> tolerated, not fatal. With the gateway running, `/companies` and `/companies/*` are
> proxied to it.

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
| `docker-compose.local.yml` | Overlay that makes the full-Docker stack runnable on a Mac (gateway port, frontend API URL). |
| `scripts/docker-local-up.sh` | Build sequentially + start the full Docker stack. |
| `scripts/docker-local-down.sh` | Stop the full Docker stack, keeping volumes. |
| `src/backend/.dockerignore` | Keeps `bin/`+`obj/` out of the build context (2.1 GB → 2.2 MB per service). |

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
