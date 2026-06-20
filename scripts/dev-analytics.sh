#!/usr/bin/env bash
# Run the Analytics service LOCALLY on the host, pointed at the Docker infra.
# Infra (Redis, Kafka, Loki) must already be running (scripts/dev-infra.sh).
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

load_root_env

LOCAL_ANALYTICS_PORT="${LOCAL_ANALYTICS_PORT:-5005}"

export ASPNETCORE_ENVIRONMENT="Development"
export ASPNETCORE_URLS="http://localhost:${LOCAL_ANALYTICS_PORT}"

export ConnectionStrings__Redis="localhost:${LOCAL_ANALYTICS_REDIS_PORT}"
export Logging__Loki__Url="http://localhost:${LOCAL_LOKI_PORT}"
export Kafka__BootstrapServers="localhost:${LOCAL_KAFKA_PORT}"

export Jwt__Key="${JWT_KEY}"

echo "==> Analytics service -> http://localhost:${LOCAL_ANALYTICS_PORT} (ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT)"
cd "$REPO_ROOT/src/backend/analytics-service/Analytics"
exec "$DOTNET_BIN" run --project Sellevate.Analytics.csproj --no-launch-profile "$@"
