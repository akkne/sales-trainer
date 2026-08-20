# Deployment

> **Microservices note (Phase 9):** the public API is now served by the **YARP
> gateway**, which routes every path to its owning microservice (identity, learning,
> gamification, ai, social, analytics, notification, company). The original monolith
> (`src/backend/api`) is **retired and removed from `main`** — it is preserved on the
> `monolith-legacy` branch for rollback/reference. In the Traefik prod overlay,
> `api.${DOMAIN}` now points at the `gateway` service; "backend" below refers to this
> gateway + service mesh unless it explicitly says monolith.

There is one supported production shape, plus an optional Kubernetes alternative:

- **Option A — all-in-one server (current production).** Everything (frontend + backend + infra) runs via Docker Compose on a single EU cloud server behind Traefik with automatic HTTPS. The frontend is **self-hosted** here too — served by its own container, at `sellevate.site` — there is no separate frontend hosting provider. See below.
- **Option C — Kubernetes via Helm (optional).** See below.

> EU server is required: the backend calls OpenAI and Deepgram, which block Russian IPs. The production server runs in Amsterdam (Timeweb), Ubuntu 24.04.

---

## Option A — all-in-one server with Traefik + auto HTTPS

Files:
- `docker-compose.yml` — base stack. All infra host ports are bound to `127.0.0.1` (Docker bypasses UFW, so they must not publish to `0.0.0.0`).
- `docker-compose.prod.yml` — overlay that adds **Traefik** (reverse proxy) and routes `sellevate.site` → frontend, `api.sellevate.site` → the **gateway** (which fans out to the microservices), `grafana.sellevate.site` → Grafana, issuing Let's Encrypt certs automatically.

> **Grafana** is published at `https://grafana.sellevate.site`, protected by Grafana's own login (anonymous + signup disabled). Set a strong `GRAFANA_ADMIN_PASSWORD` in the root `.env` before exposing it (the default `admin`/`admin` is a hole). Alternatively keep it private and reach it via SSH tunnel (`ssh -L 3001:localhost:3001 user@server` → http://localhost:3001) without the Traefik route.

Launch:
```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up --build -d
```

Requirements in root `.env`: `DOMAIN` (base domain; defaults to `sellevate.site` if unset — Traefik derives `DOMAIN`/`api.DOMAIN`/`grafana.DOMAIN` from it), `ACME_EMAIL`, `FRONTEND_URL=https://sellevate.site,http://localhost:3000`, plus all secrets. The frontend build also needs `src/frontend/.env.production` → `NEXT_PUBLIC_API_URL=https://api.sellevate.site`.

DNS: A records for `sellevate.site` and `*.sellevate.site` → server IP; firewall open on 80/443. The wildcard already covers `grafana.sellevate.site` (and `api.`), so no extra record is needed; add a dedicated `grafana` A record only if you do not run a wildcard.

> The full step-by-step server provisioning checklist (user, UFW, swap, Docker, build, verify) lives in the **gitignored `SERVER_SETUP.md`** at the repo root — it may contain the server IP and personal notes, so it is not committed.

---

## Option C — Kubernetes via Helm (Phase 10.5, optional)

A generic Helm chart at `infrastructure/helm/sellevate-service` deploys any one service or
the gateway as its own release, parameterized by `infrastructure/helm/values/<service>.yaml`
(one each for `gateway`, `identity`, `learning`, `gamification`, `ai`, `social`,
`analytics`, `notification`). Each release renders a Deployment + Service with
`livenessProbe` → `/healthz` and `readinessProbe` → `/readyz` (the shared Phase 10.1
endpoints), pulling config/secrets from a per-service ConfigMap + Secret. Infra deps
(Postgres/Mongo/Redis/Kafka, Loki/Prometheus/Grafana) are deployed separately and
referenced through those ConfigMaps/Secrets. `gamification` and `gateway` are the
fully-worked references; the others share the same chart with equivalent values. See
[infrastructure/helm/README.md](../infrastructure/helm/README.md) for install/validate
commands. This is an alternative to the Docker Compose shapes above, not the current
production deployment.

### Health probes
Every service and the gateway expose `/healthz` (liveness — process up) and `/readyz`
(readiness — dependencies reachable, with a per-check breakdown). Use `/healthz` for
liveness probes and load-balancer "is the process alive" checks, and `/readyz` for
readiness gating so traffic only reaches a pod once its Postgres/Redis/Kafka/Mongo
dependencies are up. See [MONITORING.md](MONITORING.md#health-checks-phase-101).

---

## 1. Backend + infra — Docker Compose

The whole backend stack deploys via the root `docker-compose.yml`.

```bash
# On the deploy host, with a filled-in root .env (see Environment variables below):
docker compose up --build -d gateway identity learning gamification ai social analytics notification company \
  postgres mongo redis minio kafka loki prometheus grafana
```

(The monolith `backend` service no longer exists in `docker-compose.yml`; the gateway
plus the per-service backends serve all traffic.)

(The `frontend` service in `docker-compose.yml` **is** deployed in production — see [§2 below](#2-frontend--self-hosted-via-docker-compose). It is the same self-hosted container for local full-Docker workflow and production; only the Traefik routing in `docker-compose.prod.yml` differs.)

The backend listens on container port `8080`, published as `5001` on the host. Put it behind a reverse proxy / TLS so it is reachable as e.g. `https://api.sellevate.<domain>`.

### CORS

The backend CORS allow-list comes from the `Frontend__Url` config key (`Frontend:Url` in `appsettings.json`). It accepts a **comma-separated list** of origins, so dev and prod can both be allowed:

```
Frontend__Url=http://localhost:3000,https://sellevate.site
```

This is wired in `docker-compose.yml` from the `FRONTEND_URL` root-`.env` variable (defaults to the line above). `AllowCredentials()` is on, so origins must be listed explicitly — no wildcard.

---

## 2. Frontend — self-hosted via Docker Compose

There is no separate frontend host or dashboard. `src/frontend/Dockerfile` multi-stage
builds the Next.js app with `output: "standalone"` (`next.config.ts`) into a small
`node:23-alpine` runner that serves it with `node server.js` on container port `3000`.
The `frontend` service in `docker-compose.yml` builds that image and binds it to
`127.0.0.1:3000` (Docker bypasses UFW, so it is not published to `0.0.0.0`); the
`docker-compose.prod.yml` Traefik overlay then routes `Host(${DOMAIN:-sellevate.site})`
to it and terminates TLS via Let's Encrypt (see [Option A](#option-a--all-in-one-server-with-traefik--auto-https)
above). It ships and deploys with the exact same command as the rest of the stack:

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up --build -d
```

There is no separate "deploy the frontend" step and no automatic per-push deploy hook —
a new frontend release ships whenever this command is re-run on the server with new code.

Build-time and runtime variables come from two places:
1. **Build args**, passed from the root `.env` (see `NEXT_PUBLIC_GOOGLE_CLIENT_ID` in the
   `frontend.build.args` block of `docker-compose.yml`).
2. **`src/frontend/.env.production`**, committed (no secrets) and copied into the build —
   sets `NEXT_PUBLIC_API_URL=https://api.sellevate.site` at build time and `LOKI_URL` at
   container runtime via `env_file`.

### After the first deploy

- **Google OAuth**: add `https://sellevate.site` to *Authorized JavaScript origins* in Google Cloud Console.
- Confirm the backend's `Frontend__Url` includes `https://sellevate.site` (it does by default).

---

## Environment variables

### Frontend — `src/frontend/.env.production` + Docker build args

| Variable | Example | Notes |
|----------|---------|-------|
| `NEXT_PUBLIC_API_URL` | `https://api.sellevate.site` | Public URL of the deployed backend. **Must be HTTPS** (browser blocks mixed content). Set in `src/frontend/.env.production`, baked in at build time. |
| `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | `xxx.apps.googleusercontent.com` | Google OAuth client id. Passed as a Docker build arg from the root `.env`'s `GOOGLE_CLIENT_ID`. Build-time. |
| `LOKI_URL` | `http://loki:3100` | Optional — only if shipping frontend logs to Loki; omit otherwise. Set in `src/frontend/.env.production`, read at container runtime via `env_file`. |

> `NEXT_PUBLIC_*` are inlined at build time, so changing them requires rebuilding the `frontend` image (`docker compose ... up --build -d frontend`).

### Backend host (root `.env`, read by docker-compose)

| Variable | Purpose |
|----------|---------|
| `DOMAIN` | Base domain for the Traefik prod overlay (`DOMAIN`/`api.`/`grafana.`). Defaults to `sellevate.site`. |
| `ACME_EMAIL` | Let's Encrypt notification email (prod overlay only) |
| `FRONTEND_URL` | CORS allow-list, e.g. `http://localhost:3000,https://sellevate.site` |
| `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` | Postgres credentials |
| `JWT_KEY` | JWT signing key (≥32 chars) |
| `GOOGLE_CLIENT_ID` | Google OAuth client id (backend token validation) |
| `SUPERADMIN_EMAIL` / `SUPERADMIN_PASSWORD` / `SUPERADMIN_DISPLAY_NAME` | Seeded super-admin |
| `OPENAI_API_KEY` / `OPENAI_BASE_URL` / `OPENAI_CHAT_COMPLETIONS_PATH` | LLM provider |
| `DEEPGRAM_API_KEY` | Speech-to-text |
| `YANDEX_TTS_API_KEY` | Text-to-speech |
| `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` | Object storage (avatars/photos) |
| `GRAFANA_ADMIN_USER` / `GRAFANA_ADMIN_PASSWORD` | Grafana dashboard login |

See `.env.example` for the full template.

> **Note:** in `docker-compose.yml` the backend currently runs with `ASPNETCORE_ENVIRONMENT=Development` (exposes Swagger at `/swagger`). For a real production deploy, switch it to `Production`.
