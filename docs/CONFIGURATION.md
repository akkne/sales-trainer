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

## Keys promoted out of code by the cleanliness audit (2026-08-18)

The audit's rule was the one the owner set: a **secret** belongs in the root `.env` and reaches the
service as `INJECTED_FROM_ENV`; **operational tuning** a human may want to change without a rebuild
belongs in that service's `appsettings.json` behind an Options class; a **domain invariant** stays in
code as a constant. The keys below are the tuning half — every default is byte-identical to the
literal it replaced, so nothing changed behaviour.

**No new secret was introduced and no `.env` variable was added or removed.**

### gateway

| Key | Default | Meaning |
|---|---|---|
| `Frontend:Url` | `http://localhost:3000` | Comma-separated CORS allow-list, used **only** for answers the gateway generates itself (404 unknown route, 502 unreachable upstream, 504 timeout). Proxied answers carry the downstream service's headers. Every other service already had this key; the gateway did not, and `docker-compose.yml` did not pass `Frontend__Url` to it either — see the fix in the same audit |

### identity-service

| Key | Default | Meaning |
|---|---|---|
| `BackgroundJobs:ExpiredRefreshTokenCleanupIntervalHours` | `24` | How often revoked and expired refresh tokens are swept |
| `BackgroundJobs:ExpiredEmailVerificationCleanupIntervalHours` | `24` | How often expired verification codes are swept |

### company-service

| Key | Default | Meaning |
|---|---|---|
| `Companies:DescriptionExcerptLength` | `160` | How much of a company description the list view carries |
| `Companies:RecentGoalCount` | `5` | Distinct recent practice goals offered by the goal picker |
| `Companies:RecentCallLogCountForBriefing` | `5` | Newest call-log entries fed to the briefing prompt |
| `Companies:ReadinessNoFeedbackCacheMinutes` | `2` | TTL of the negative "no usable feedback yet" readiness cache |
| `FollowUpReminder:PublishMaxAttempts` | `3` | Publish attempts per company before the tick gives up |
| `FollowUpReminder:PublishRetryBaseDelayMilliseconds` | `100` | Base of the linear backoff between publish attempts |

`MaxSessionIdsForReadiness` (50) deliberately stayed a constant: it mirrors ai-service's own hard
guard, so it is a cross-service contract rather than tuning — raising it locally would get the whole
call rejected.

### notification-service

| Key | Default | Meaning |
|---|---|---|
| `NotificationEmail:MaximumFlushBatchSize` | `100` | How many due unread-chat emails one dispatcher tick claims. The claim is destructive, so this is also the most emails a single crashed tick can lose — raise it only together with `DispatcherPollIntervalSeconds` |

### gamification-service

| Key | Default | Meaning |
|---|---|---|
| `Gamification:RecurringJobs:DailyStreakResetCron` | `5 0 * * *` | Cron for `StreakResetJob` |
| `Gamification:RecurringJobs:WeeklyLeagueClosureCron` | `*/15 * * * *` | Cron for `WeeklyLeagueClosureJob`. Every 15 minutes, not weekly, because each organization now runs its own league week |

Both values are unchanged from the constants they replaced. The Hangfire **job identifiers** stayed
in `HangfireJobIdentifiers` on purpose — Hangfire persists them as storage keys, so they are identity,
not configuration. `Gamification:StreakTimezone` (`UTC`) already existed and is now bound through
`StreakConfiguration` instead of being read as a raw key.

### ai-service

| Key | Default | Meaning |
|---|---|---|
| `Whisper:ResponseFormat` | `verbose_json` | Response shape asked of Whisper. The verbose form is the only one carrying the detected `language` field the endpoint returns |
| `YandexTts:SynthesizePath` | `/speech/v1/tts:synthesize` | Path appended to `YandexTts:BaseUrl` |
| `YandexTts:AudioFormat` | `lpcm` | Container asked of the provider — raw PCM, because the WAV header is written locally |
| `YandexTts:SampleRateHertz` | `48000` | Must match the header the service writes, or playback is pitch-shifted |
| `YandexTts:MeteredModelName` | `yandex-tts` | The name synthesis is billed under on the spend report **and** the lookup key in `AiQuotas:PricePerMillionTokens`. Renaming one without the other silently prices TTS at the fallback price |
| `Voice:MaximumCacheableTextLength` | `80` | Longest text worth caching, in characters |
| `Voice:AudioCacheMaximumTotalBytes` | `33554432` | Total synthesized audio the process-wide cache may hold (32 MB) |
| `Voice:AudioCacheEntryLifetimeHours` | `24` | Cached-phrase TTL |

Three ai-service numbers were deliberately **not** touched, because each is an open owner decision
recorded in [DONT_FORGET.md](DONT_FORGET.md): the shared OpenAI client's timeout budget (30 s per
attempt, 90 s total, 2 retries) against the content pipeline's declared 300 s; and
`AiQuotas:EstimatedCharactersPerToken` (4), which under-estimates Russian by 1.5–2× but whose
correction would rewrite historical spend figures.

### Placeholder handling — behaviour change worth knowing

`INJECTED_FROM_ENV` is now recognised as a placeholder, not as a credential. Previously the guards in
ai-service knew only the `REPLACE_WITH_…` family from `.env.example`, so a deployment missing
`OPENAI_API_KEY` or `YANDEX_TTS_API_KEY` reported the feature as **configured** and sent the literal
marker string to the provider as the key — a 401 from a paid endpoint on every dialog turn, voice turn
and transcription, instead of the intended degradation to an empty bundle list, text-only voice and a
stub transcript. Both families are now rejected, in one place
(`Ai/Common/Constants/AiSecretPlaceholders.cs`), which the three sites that carried their own weaker
copies call.

### Configuration keys that exist but do nothing

Found while auditing, left in place because fixing each changes behaviour. Recorded so nobody tunes
them expecting an effect:

- **`Voice:VadSilenceMs` never binds.** The bound property is `VadSilenceMilliseconds`. Both happen
  to be 1200, so behaviour is correct today, but editing the key has no effect.
- **`Voice:TtsProvider` is dead.** `TtsRouter` derives the provider from whichever API key is set;
  nothing reads the option, yet the exception it throws tells the operator to set it.
- **`VoiceUsageLimitsConfiguration` is registered and resolved nowhere**, and it binds the same
  `Voice` section as `VoiceFeatureConfiguration` while defaulting the two limits to `0` instead of
  30/300 — so the first caller to inject it would silently get "window disabled".
