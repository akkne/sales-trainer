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
# Port the locally-run Identity microservice listens on.
LOCAL_IDENTITY_PORT="${LOCAL_IDENTITY_PORT:-5002}"
# Port the locally-run Gamification microservice listens on.
LOCAL_GAMIFICATION_PORT="${LOCAL_GAMIFICATION_PORT:-5007}"
# Port the locally-run Social microservice listens on.
LOCAL_SOCIAL_PORT="${LOCAL_SOCIAL_PORT:-5006}"
# Host port published by docker-compose.infra.yml for MinIO (S3 API).
LOCAL_MINIO_PORT="${LOCAL_MINIO_PORT:-9000}"

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
  # Proxy targets = the host-run monolith + the host-run Identity service. Identity
  # owns /auth, /demo, /profile, /onboarding, /avatars; everything else falls through
  # to the monolith catch-all.
  export ReverseProxy__Clusters__monolith__Destinations__d1__Address="http://localhost:${LOCAL_BACKEND_PORT}/"
  export ReverseProxy__Clusters__identity__Destinations__d1__Address="http://localhost:${LOCAL_IDENTITY_PORT}/"
  export ReverseProxy__Clusters__gamification__Destinations__d1__Address="http://localhost:${LOCAL_GAMIFICATION_PORT}/"
  export ReverseProxy__Clusters__social__Destinations__d1__Address="http://localhost:${LOCAL_SOCIAL_PORT}/"
}

# Config overrides for running the Identity microservice on the host. It owns its own
# Postgres database (identity-db) on the shared local Postgres instance, issues JWTs
# with the same key/issuer/audience as the monolith, and produces user.* events to Kafka.
export_identity_env() {
  export ASPNETCORE_ENVIRONMENT="Development"
  export ASPNETCORE_URLS="http://localhost:${LOCAL_IDENTITY_PORT}"

  export ConnectionStrings__Postgres="Host=localhost;Port=${LOCAL_POSTGRES_PORT};Database=identity;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
  export Kafka__BootstrapServers="localhost:${LOCAL_KAFKA_PORT}"
  export Logging__Loki__Url="http://localhost:${LOCAL_LOKI_PORT}"

  export Jwt__Key="${JWT_KEY}"
  export Google__ClientId="${GOOGLE_CLIENT_ID}"
  export MailerSend__ApiToken="${MAILERSEND_API_TOKEN}"
  export MailerSend__FromEmail="${MAILERSEND_FROM_EMAIL}"
  export MailerSend__FromName="${MAILERSEND_FROM_NAME:-Sellevate}"
  export SuperAdmin__Email="${SUPERADMIN_EMAIL}"
  export SuperAdmin__Password="${SUPERADMIN_PASSWORD}"
  export SuperAdmin__DisplayName="${SUPERADMIN_DISPLAY_NAME}"
}

# Config overrides for running the Gamification microservice on the host. It owns its
# own Postgres database (gamification) on the shared local Postgres instance, consumes
# learning/dialog/user events from Kafka and produces xp/achievement/streak events. Its
# Hangfire jobs (streak reset, weekly league closure) run on the gamification database.
export_gamification_env() {
  export ASPNETCORE_ENVIRONMENT="Development"
  export ASPNETCORE_URLS="http://localhost:${LOCAL_GAMIFICATION_PORT}"

  export ConnectionStrings__Postgres="Host=localhost;Port=${LOCAL_POSTGRES_PORT};Database=gamification;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
  export ConnectionStrings__Redis="localhost:${LOCAL_REDIS_PORT}"
  export Kafka__BootstrapServers="localhost:${LOCAL_KAFKA_PORT}"
  export Logging__Loki__Url="http://localhost:${LOCAL_LOKI_PORT}"

  export Jwt__Key="${JWT_KEY}"
}

# Config overrides for running the Social microservice on the host. It owns its own
# Postgres database (social) on the shared local Postgres instance, reuses the shared
# Mongo (chat_conversations) and MinIO (Discuss photos), keeps a local UserReplica from
# user.* events, and produces friend.request.* / chat.message.sent to Kafka.
export_social_env() {
  export ASPNETCORE_ENVIRONMENT="Development"
  export ASPNETCORE_URLS="http://localhost:${LOCAL_SOCIAL_PORT}"

  export ConnectionStrings__Postgres="Host=localhost;Port=${LOCAL_POSTGRES_PORT};Database=social;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
  export ConnectionStrings__Mongo="mongodb://localhost:${LOCAL_MONGO_PORT}"
  export ConnectionStrings__Redis="localhost:${LOCAL_REDIS_PORT}"
  export Kafka__BootstrapServers="localhost:${LOCAL_KAFKA_PORT}"
  export Logging__Loki__Url="http://localhost:${LOCAL_LOKI_PORT}"

  export Jwt__Key="${JWT_KEY}"

  export Storage__S3__Endpoint="http://localhost:${LOCAL_MINIO_PORT}"
  export Storage__S3__AccessKey="${MINIO_ROOT_USER}"
  export Storage__S3__SecretKey="${MINIO_ROOT_PASSWORD}"
  export Storage__S3__Bucket="sellevate-social"
}
