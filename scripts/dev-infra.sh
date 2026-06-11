#!/usr/bin/env bash
# Start ONLY the infrastructure services (postgres, mongo, redis, loki,
# prometheus, grafana) in Docker. Backend + frontend run on the host.
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

cd "$REPO_ROOT"
echo "==> Starting infrastructure (docker-compose.infra.yml)..."
"$DOCKER_BIN" compose -f docker-compose.infra.yml up -d "$@"
echo "==> Infrastructure is up:"
echo "    Postgres   localhost:${LOCAL_POSTGRES_PORT}"
echo "    Mongo      localhost:${LOCAL_MONGO_PORT}"
echo "    Redis      localhost:${LOCAL_REDIS_PORT}"
echo "    Loki       localhost:${LOCAL_LOKI_PORT}"
echo "    Prometheus localhost:9090"
echo "    Grafana    localhost:3001"
