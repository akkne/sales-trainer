#!/usr/bin/env bash
# Run the AI Engine service LOCALLY on the host, pointed at the Docker infra.
# Infra (Postgres, Mongo, Redis, Kafka, Loki) must already be running (scripts/dev-infra.sh).
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

load_root_env

LOCAL_AI_PORT="${LOCAL_AI_PORT:-5003}"

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
export YandexTts__ApiKey="${YANDEX_TTS_API_KEY}"
export GoogleTts__ApiKey="${GOOGLE_TTS_API_KEY:-}"

echo "==> AI service -> http://localhost:${LOCAL_AI_PORT} (ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT)"
cd "$REPO_ROOT/src/backend/ai-service/Ai"
exec "$DOTNET_BIN" run --project Sellevate.Ai.csproj --no-launch-profile "$@"
