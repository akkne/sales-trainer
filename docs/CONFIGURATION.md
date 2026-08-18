# Configuration & Secrets Layout

Updated: 2026-06-06

## Principle

- **Root `.env`** (gitignored) — secrets and shared infrastructure credentials only.
- **Backend** — all service config lives in `appsettings.json` (committed, no secrets;
  secret values are marked `INJECTED_FROM_ENV`). `appsettings.Development.json`
  (gitignored, mounted by compose) holds dev-only overrides.
- **Frontend** — service config in `.env.production` (committed, no secrets).

This keeps each service self-contained for a future microservice split.

## File map

| File | Committed | Purpose |
|------|-----------|---------|
| `.env` | no | All secrets: DB credentials, JWT key, API keys (OpenAI, Deepgram, Yandex TTS, MailerSend), Google client id, superadmin, Grafana admin |
| `.env.example` | yes | Template for `.env` with placeholders |
| `src/backend/api/appsettings.json` | yes | All backend config: docker-network hostnames (mongo, redis, loki), models, voice settings, limits. Secrets = `INJECTED_FROM_ENV` |
| `src/backend/api/appsettings.Development.json` | no (mounted by compose) | Dev-only overrides (provider base URLs, models) |
| `src/backend/api/appsettings.Testing.json` | no | Integration-test config with real API keys. Copy from `appsettings.Testing.example.json` |
| `src/backend/api/appsettings.Testing.example.json` | yes | Template for the Testing config |
| `src/frontend/.env.production` | yes | `NEXT_PUBLIC_API_URL` (build-time) and `LOKI_URL` (runtime via compose `env_file`) |
| `src/frontend/.env.local.example` | yes | Template for bare-metal `npm run dev` |

## How values flow

```
.env ──(interpolation)──> docker-compose.yml environment ──> backend env vars
                                                              (override appsettings.json)
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
| `YANDEX_TTS_API_KEY` | backend | `YandexTts:ApiKey` |
| `MAILERSEND_API_TOKEN/FROM_EMAIL/FROM_NAME` | backend | `MailerSend:*` |
| `GRAFANA_ADMIN_USER/PASSWORD` | grafana | `GF_SECURITY_ADMIN_*` |

Non-secret email-verification tuning (`EmailVerification:CodeLength`, `CodeLifetimeMinutes`,
`MaximumVerificationAttempts`, `ResendCooldownSeconds`) lives in `appsettings.json`. See
[EMAIL_VERIFICATION.md](EMAIL_VERIFICATION.md) and [INTEGRATIONS.md](INTEGRATIONS.md#mailersend-transactional-email).

## Company service (Phase 39)

`company-service` introduces no new secrets — it reuses the shared `POSTGRES_USER`/
`POSTGRES_PASSWORD` (own database `company`), `JWT_KEY`, and `FRONTEND_URL` from the
root `.env`. Its `ConnectionStrings:Postgres`, `Jwt:Key`, and `Frontend:Url` config
keys are `INJECTED_FROM_ENV` in `src/backend/company-service/Company/appsettings.json`,
same pattern as the other extracted services.

## Yandex TTS key

Yandex SpeechKit API key of a service account with the `ai.speechkit-tts.user` role.
Create at [console.yandex.cloud](https://console.yandex.cloud): service account → API keys → create.
Sent as `Authorization: Api-Key <key>` header (see `YandexTtsService`).

## AI quotas and the keys learning-service no longer needs (Phase 40.33)

**New section, ai-service only — `AiQuotas`.** The platform-wide defaults every organization is
metered against until it is given limits of its own, plus the price table the spend report is
rendered with.

| Key | Default | Meaning |
|---|---|---|
| `AiQuotas:DefaultVoiceDailyLimitMinutes` | 600 | Organization-wide voice minutes per UTC day. `0` disables the window |
| `AiQuotas:DefaultVoiceMonthlyLimitMinutes` | 6000 | Same, per UTC month |
| `AiQuotas:DefaultLlmMonthlyTokenLimit` | 20 000 000 | Prompt + completion tokens per UTC month, all models. `0` disables the LLM limit |
| `AiQuotas:DefaultBatchReservePercent` | 10 | Share of the LLM allowance background pipelines may not touch, so a batch stops before a conversation does |
| `AiQuotas:SoftWarningPercent` | 80 | Where the spend report turns `warning`. Refuses nothing |
| `AiQuotas:Currency` | `RUB` | Display only |
| `AiQuotas:PricePerMillionTokens` | `{ "yandex-tts": 1300 }` | Per-model price, plus `tts`/`stt` providers priced per million characters. **Display only** — limits are counted in tokens, so editing a price re-renders history and moves no limit |
| `AiQuotas:FallbackPricePerMillionTokens` | 0 | Price for a model absent from the table. At `0` such a model is reported as **unpriced**, never as free |
| `AiQuotas:EstimatedCharactersPerToken` | 4 | Divisor for the one path with no reported usage — a streamed dialog turn |

The four `Default*` keys are also exposed as `AI_QUOTA_*` environment variables in
`docker-compose.yml` and `scripts/dev-ai.sh`. Per-organization overrides are rows in
`OrganizationQuotas`, written through `PUT /admin/ai-quota`; **null in a column means "the platform
default above", never "unlimited"**.

`Voice:DailyLimitMinutes` / `MonthlyLimitMinutes` are unchanged and still mean the **per-user**
allowance. Both windows apply. See [AI_QUOTAS.md](AI_QUOTAS.md).

**Removed from learning-service:** the `OpenAI`, `YandexTts` and `Voice` sections, and with them the
`OPENAI_*` and `YANDEX_TTS_API_KEY` environment variables in its compose block and in
`scripts/lib-local-env.sh`. learning-service reaches the providers through ai-service now and holds
no provider secret of any kind — a smaller secret surface as well as the thing that makes the meter
complete. `AiService:ChatPath`, `ChatStreamPath`, `TextToSpeechPath`, `QuotaPreflightPath` and
`ChatTimeoutSeconds` (90s) replace them, all non-secret and committed.

Those variables stay in the root `.env` and are still consumed by **ai-service**. Nothing needs to be
deleted from `.env`; what changed is which service reads it.
