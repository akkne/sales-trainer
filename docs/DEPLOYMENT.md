# Deployment

Production runs as a **hybrid**:

- **Frontend (Next.js)** → **Vercel** — `https://sellevate.vercel.app`
- **Backend (.NET API) + infra (Postgres, Mongo, Redis, MinIO, Loki, Prometheus, Grafana)** → **Docker Compose** on a host with a public HTTPS address.

Vercel cannot host the .NET server or the stateful infra, so only the frontend lives there. The Vercel frontend talks to the backend over the public API URL, and the backend whitelists the Vercel origin via CORS.

---

## 1. Backend + infra — Docker Compose

The whole backend stack deploys via the root `docker-compose.yml`.

```bash
# On the deploy host, with a filled-in root .env (see Environment variables below):
docker compose up --build -d backend postgres mongo redis minio loki prometheus grafana
```

(The `frontend` service in `docker-compose.yml` is **not** deployed in production — the frontend goes to Vercel. It stays in the file only for the local full-Docker workflow.)

The backend listens on container port `8080`, published as `5001` on the host. Put it behind a reverse proxy / TLS so it is reachable as e.g. `https://api.sellevate.<domain>`.

### CORS

The backend CORS allow-list comes from the `Frontend__Url` config key (`Frontend:Url` in `appsettings.json`). It accepts a **comma-separated list** of origins, so dev and prod can both be allowed:

```
Frontend__Url=http://localhost:3000,https://sellevate.vercel.app
```

This is wired in `docker-compose.yml` from the `FRONTEND_URL` root-`.env` variable (defaults to the line above). `AllowCredentials()` is on, so origins must be listed explicitly — no wildcard.

---

## 2. Frontend — Vercel

1. **Import the repo** in the Vercel dashboard → Add New → Project.
2. **Root Directory** → `src/frontend` (monorepo layout; this is required so Vercel finds the Next.js app and `vercel.json`).
3. Framework preset auto-detects as **Next.js**. Build/install commands come from `src/frontend/vercel.json` (`npm run build`, `npm ci`). The `output: "standalone"` in `next.config.ts` is for Docker and is ignored by Vercel.
4. Set the **Environment Variables** (see below) in Project Settings.
5. Deploy. Every push to the production branch triggers an automatic deploy.

### After the first deploy

- **Google OAuth**: add `https://sellevate.vercel.app` to *Authorized JavaScript origins* in Google Cloud Console.
- Confirm the backend's `Frontend__Url` includes `https://sellevate.vercel.app` (it does by default).

---

## Environment variables

### Vercel (frontend) — Project Settings → Environment Variables

| Variable | Example | Notes |
|----------|---------|-------|
| `NEXT_PUBLIC_API_URL` | `https://api.sellevate.<domain>` | Public URL of the deployed backend. **Must be HTTPS** (browser blocks mixed content). Baked in at build time. |
| `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | `xxx.apps.googleusercontent.com` | Google OAuth client id. Build-time. |
| `LOKI_URL` | `https://loki.<domain>` | Optional — only if shipping frontend logs to Loki; omit otherwise. Runtime. |

> `NEXT_PUBLIC_*` are inlined at build time, so changing them requires a redeploy.

### Backend host (root `.env`, read by docker-compose)

| Variable | Purpose |
|----------|---------|
| `FRONTEND_URL` | CORS allow-list, e.g. `http://localhost:3000,https://sellevate.vercel.app` |
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
