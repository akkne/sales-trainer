#!/usr/bin/env bash
# Stop the full-Docker local stack started by scripts/docker-local-up.sh.
#
# Named volumes are preserved (Postgres/Mongo/Redis/MinIO data survives), the same
# way scripts/dev-down.sh leaves infra data alone. Pass -v yourself if you really
# want to drop the data.
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

cd "$REPO_ROOT"
echo "==> Stopping the full Docker stack..."
"$DOCKER_BIN" compose -f docker-compose.yml -f docker-compose.local.yml down "$@"
echo "==> Stopped. Named volumes were kept."
