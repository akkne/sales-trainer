#!/usr/bin/env bash
# Run the AI Engine service LOCALLY on the host, pointed at the Docker infra.
# Infra (Postgres, Mongo, Redis, Kafka, Loki) must already be running (scripts/dev-infra.sh).
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

load_root_env


export ASPNETCORE_ENVIRONMENT="Development"
export ASPNETCORE_URLS="http://localhost:${LOCAL_AI_PORT}"

export ConnectionStrings__Postgres="Host=localhost;Port=${LOCAL_POSTGRES_PORT};Database=ai;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
export ConnectionStrings__Mongo="mongodb://localhost:${LOCAL_MONGO_PORT}"
export ConnectionStrings__Redis="localhost:${LOCAL_REDIS_PORT}"
export Mongo__DatabaseName="sallevate"
export Logging__Loki__Url="http://localhost:${LOCAL_LOKI_PORT}"
export Kafka__BootstrapServers="localhost:${LOCAL_KAFKA_PORT}"

export Jwt__Key="${JWT_KEY}"
export OpenAI__ApiKey="${OPENAI_API_KEY}"
export OpenAI__BaseUrl="${OPENAI_BASE_URL}"
export OpenAI__ChatCompletionsPath="${OPENAI_CHAT_COMPLETIONS_PATH}"
# Provider selects the auth header/schema: OpenAi=Bearer, F5Ai=X-Auth-Token. Must be F5Ai
# when routing through api.f5ai.ru, otherwise the gateway rejects the Bearer header with 401.
export OpenAI__Provider="${OPENAI_PROVIDER:-OpenAi}"
# --- AI tunables (override in .env; defaults are the in-code values) ---
export OpenAI__DialogModel="${OPENAI_DIALOG_MODEL:-gpt-4o}"
export OpenAI__OpenQuestionModel="${OPENAI_OPEN_QUESTION_MODEL:-gpt-4.1}"
export OpenAI__DialogTemperature="${OPENAI_DIALOG_TEMPERATURE:-0.7}"
export OpenAI__MaximumDialogTokenCount="${OPENAI_MAX_TOKENS_DIALOG:-500}"
export OpenAI__MaximumFeedbackTokenCount="${OPENAI_MAX_TOKENS_FEEDBACK:-1500}"
export OpenAI__MaximumOpenQuestionTokenCount="${OPENAI_MAX_TOKENS_OPEN_QUESTION:-300}"
export YandexTts__ApiKey="${YANDEX_TTS_API_KEY}"
# Phase 40.33 — platform-wide quota defaults (an absent OrganizationQuotas row means these).
export AiQuotas__DefaultVoiceDailyLimitMinutes="${AI_QUOTA_VOICE_DAILY_MINUTES:-600}"
export AiQuotas__DefaultVoiceMonthlyLimitMinutes="${AI_QUOTA_VOICE_MONTHLY_MINUTES:-6000}"
export AiQuotas__DefaultLlmMonthlyTokenLimit="${AI_QUOTA_LLM_MONTHLY_TOKENS:-20000000}"
export AiQuotas__DefaultBatchReservePercent="${AI_QUOTA_BATCH_RESERVE_PERCENT:-10}"
export GoogleTts__ApiKey="${GOOGLE_TTS_API_KEY:-}"

echo "==> AI service -> http://localhost:${LOCAL_AI_PORT} (ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT)"
cd "$REPO_ROOT/src/backend/ai-service/Ai"
exec "$DOTNET_BIN" run --project Sellevate.Ai.csproj --no-launch-profile "$@"
