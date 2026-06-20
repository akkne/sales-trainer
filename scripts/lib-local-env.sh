#!/usr/bin/env bash
# Shared helpers for the LOCAL dev profile (backend + frontend on the host,
# infra in Docker via docker-compose.infra.yml).
#
# Sourced by dev-backend.sh / dev-frontend.sh / dev-up.sh — not run directly.

# Repo root = parent of this scripts/ dir.
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Tooling is not always on PATH on this machine (see docs).
DOTNET_BIN="${DOTNET_BIN:-/usr/local/share/dotnet/dotnet}"
DOCKER_BIN="${DOCKER_BIN:-/usr/local/bin/docker}"

# Host ports published by docker-compose.infra.yml.
LOCAL_POSTGRES_PORT="${LOCAL_POSTGRES_PORT:-5433}"
LOCAL_MONGO_PORT="${LOCAL_MONGO_PORT:-27017}"
LOCAL_REDIS_PORT="${LOCAL_REDIS_PORT:-6379}"
LOCAL_ANALYTICS_REDIS_PORT="${LOCAL_ANALYTICS_REDIS_PORT:-6380}"
LOCAL_KAFKA_PORT="${LOCAL_KAFKA_PORT:-9092}"
LOCAL_KAFKA_UI_PORT="${LOCAL_KAFKA_UI_PORT:-8085}"
LOCAL_LOKI_PORT="${LOCAL_LOKI_PORT:-3100}"

# Port the locally-run backend listens on (matches the 5001 the frontend expects).
LOCAL_BACKEND_PORT="${LOCAL_BACKEND_PORT:-5001}"
LOCAL_FRONTEND_PORT="${LOCAL_FRONTEND_PORT:-3000}"
# Port the locally-run API gateway (YARP) listens on.
LOCAL_GATEWAY_PORT="${LOCAL_GATEWAY_PORT:-5000}"

# Load secrets/infra credentials from the root .env into the environment.
# Parsed line-by-line (not `source`d) so unquoted values with spaces — e.g.
# SUPERADMIN_DISPLAY_NAME=Super Admin — work the same as they do for compose.
load_root_env() {
  local env_file="$REPO_ROOT/.env"
  if [[ ! -f "$env_file" ]]; then
    echo "ERROR: $env_file not found. Copy .env.example to .env first." >&2
    return 1
  fi
  local line key val
  while IFS= read -r line || [[ -n "$line" ]]; do
    [[ "$line" =~ ^[[:space:]]*# ]] && continue   # comment
    [[ "$line" != *=* ]] && continue              # not KEY=VALUE
    key="${line%%=*}"
    val="${line#*=}"
    key="${key//[[:space:]]/}"                     # trim spaces around key
    [[ -z "$key" ]] && continue
    export "$key=$val"
  done < "$env_file"
}

# Export the config overrides the backend needs to reach infra on localhost.
# Mirrors the env block in docker-compose.yml, but with host ports + localhost.
export_backend_env() {
  export ASPNETCORE_ENVIRONMENT="Development"
  export ASPNETCORE_URLS="http://localhost:${LOCAL_BACKEND_PORT}"

  export ConnectionStrings__Postgres="Host=localhost;Port=${LOCAL_POSTGRES_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
  export ConnectionStrings__Mongo="mongodb://localhost:${LOCAL_MONGO_PORT}"
  export ConnectionStrings__Redis="localhost:${LOCAL_REDIS_PORT}"
  export Logging__Loki__Url="http://localhost:${LOCAL_LOKI_PORT}"

  export Jwt__Key="${JWT_KEY}"
  export Google__ClientId="${GOOGLE_CLIENT_ID}"
  export OpenAI__ApiKey="${OPENAI_API_KEY}"
  export OpenAI__BaseUrl="${OPENAI_BASE_URL}"
  export OpenAI__ChatCompletionsPath="${OPENAI_CHAT_COMPLETIONS_PATH}"
  export Deepgram__ApiKey="${DEEPGRAM_API_KEY}"
  export YandexTts__ApiKey="${YANDEX_TTS_API_KEY}"
  export SuperAdmin__Email="${SUPERADMIN_EMAIL}"
  export SuperAdmin__Password="${SUPERADMIN_PASSWORD}"
  export SuperAdmin__DisplayName="${SUPERADMIN_DISPLAY_NAME}"

  # Kafka broker (host listener). The monolith does not consume/produce yet, but
  # extracted services started on the host will read this.
  export Kafka__BootstrapServers="localhost:${LOCAL_KAFKA_PORT}"
}

# Config overrides for running the API gateway (YARP) on the host. It validates the
# same JWT the monolith issues and proxies everything to the host-run monolith.
export_gateway_env() {
  export ASPNETCORE_ENVIRONMENT="Development"
  export ASPNETCORE_URLS="http://localhost:${LOCAL_GATEWAY_PORT}"

  export Jwt__Key="${JWT_KEY}"
  export Logging__Loki__Url="http://localhost:${LOCAL_LOKI_PORT}"
  export Kafka__BootstrapServers="localhost:${LOCAL_KAFKA_PORT}"
  # Proxy target = the host-run monolith (appsettings.Development.json already
  # points here, but keep it explicit so a custom LOCAL_BACKEND_PORT is honored).
  export ReverseProxy__Clusters__monolith__Destinations__d1__Address="http://localhost:${LOCAL_BACKEND_PORT}/"
}
