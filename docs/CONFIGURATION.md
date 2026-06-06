# Configuration & Secrets Layout

Updated: 2026-06-06

## Principle

Secrets and infrastructure credentials live in the **root `.env`** (gitignored).
Service-specific, non-secret config lives in each service's own files —
this prepares the codebase for splitting into microservices.

## File map

| File | Committed | Purpose |
|------|-----------|---------|
| `.env` | no | All secrets: DB credentials, JWT key, API keys (OpenAI, Deepgram, Voicer, Yandex TTS), Google client id, superadmin, Grafana admin |
| `.env.example` | yes | Template for `.env` with placeholders |
| `src/backend/api/.env.docker` | yes | Backend wiring inside docker network: Mongo/Redis URLs, JWT issuer/audience, frontend URL, Loki URL |
| `src/backend/api/appsettings.json` | yes | Backend defaults: models, voice settings, limits (no secrets — placeholders only) |
| `src/backend/api/appsettings.Development.json` | no (gitignored, mounted by compose) | Dev-only overrides (models, providers). Secrets stripped — they come from env vars |
| `src/frontend/.env.production` | yes | `NEXT_PUBLIC_API_URL` (build-time) and `LOKI_URL` (runtime via compose `env_file`) |
| `src/frontend/.env.local.example` | yes | Template for bare-metal `npm run dev` |

## How values flow

```
.env ──(interpolation)──> docker-compose.yml environment ──> backend env vars
                                                              (override appsettings.json)
src/backend/api/.env.docker ──(env_file)──> backend env vars
src/frontend/.env.production ──(COPY . . at build)──> next build (NEXT_PUBLIC_*)
                             ──(env_file)──> frontend runtime (LOKI_URL)
GOOGLE_CLIENT_ID ──(build arg)──> NEXT_PUBLIC_GOOGLE_CLIENT_ID
```

ASP.NET config precedence: env vars > `appsettings.{Environment}.json` > `appsettings.json`.
Double underscore maps to a section: `YandexTts__ApiKey` → `YandexTts:ApiKey`.

## Env variables in root `.env`

| Variable | Used by | Maps to |
|----------|---------|---------|
| `POSTGRES_DB/USER/PASSWORD` | postgres, backend | `ConnectionStrings:Postgres` |
| `JWT_KEY` | backend | `Jwt:Key` |
| `GOOGLE_CLIENT_ID` | backend, frontend build | `Google:ClientId`, `NEXT_PUBLIC_GOOGLE_CLIENT_ID` |
| `SUPERADMIN_EMAIL/PASSWORD/DISPLAY_NAME` | backend | `SuperAdmin:*` |
| `OPENAI_API_KEY/BASE_URL/CHAT_COMPLETIONS_PATH` | backend | `OpenAI:*` |
| `DEEPGRAM_API_KEY` | backend | `Deepgram:ApiKey` |
| `VOICER_API_KEY` | backend | `VoicerTts:ApiKey` |
| `YANDEX_TTS_API_KEY` | backend | `YandexTts:ApiKey` |
| `GRAFANA_ADMIN_USER/PASSWORD` | grafana | `GF_SECURITY_ADMIN_*` |

## Yandex TTS key

Yandex SpeechKit API key of a service account with the `ai.speechkit-tts.user` role.
Create at [console.yandex.cloud](https://console.yandex.cloud): service account → API keys → create.
Sent as `Authorization: Api-Key <key>` header (see `YandexTtsService`).
