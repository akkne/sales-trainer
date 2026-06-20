#!/usr/bin/env bash
# Run the Notifications service LOCALLY on the host, pointed at the Docker infra.
# Infra (Redis, Kafka, Loki) must already be running (scripts/dev-infra.sh).
# This service is Redis-only — no Postgres/Mongo database to bootstrap.
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

load_root_env

LOCAL_NOTIFICATION_PORT="${LOCAL_NOTIFICATION_PORT:-5004}"

export ASPNETCORE_ENVIRONMENT="Development"
export ASPNETCORE_URLS="http://localhost:${LOCAL_NOTIFICATION_PORT}"

export ConnectionStrings__Redis="localhost:${LOCAL_REDIS_PORT}"
export Logging__Loki__Url="http://localhost:${LOCAL_LOKI_PORT}"
export Kafka__BootstrapServers="localhost:${LOCAL_KAFKA_PORT}"

export Jwt__Key="${JWT_KEY}"

echo "==> Notifications service -> http://localhost:${LOCAL_NOTIFICATION_PORT} (ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT)"
cd "$REPO_ROOT/src/backend/notification-service/Notification"
exec "$DOTNET_BIN" run --project Sellevate.Notification.csproj --no-launch-profile "$@"
