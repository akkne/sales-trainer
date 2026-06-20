#!/usr/bin/env bash
# Run the API Gateway (YARP) LOCALLY on the host, proxying to the host-run monolith.
# Infra (incl. Kafka) must already be running (scripts/dev-infra.sh) and the backend
# should be up on LOCAL_BACKEND_PORT (scripts/dev-backend.sh).
#
# Optional during Phase 0 of the microservices migration — the monolith still serves
# the frontend directly on port 5001. Use this to exercise the gateway path.
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

load_root_env
export_gateway_env

echo "==> Gateway -> http://localhost:${LOCAL_GATEWAY_PORT} (proxying to http://localhost:${LOCAL_BACKEND_PORT})"
cd "$REPO_ROOT/src/backend/gateway/Gateway"
exec "$DOTNET_BIN" run --project Sellevate.Gateway.csproj --no-launch-profile "$@"
