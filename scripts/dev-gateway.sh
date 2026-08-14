#!/usr/bin/env bash
# Run the API Gateway (YARP) LOCALLY on the host. It is the single entrypoint the
# frontend talks to, routing each path to the host-run service that owns it.
#
# Infra (incl. Kafka) must already be running (scripts/dev-infra.sh), and every service
# the gateway proxies to should be up (scripts/dev-identity.sh, dev-ai.sh, ...). Routes
# whose service is down return 502; unknown routes 404 (the monolith catch-all is gone).
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

load_root_env
export_gateway_env

echo "==> Gateway -> http://localhost:${LOCAL_GATEWAY_PORT} (identity:${LOCAL_IDENTITY_PORT} ai:${LOCAL_AI_PORT} notification:${LOCAL_NOTIFICATION_PORT} analytics:${LOCAL_ANALYTICS_PORT} social:${LOCAL_SOCIAL_PORT} gamification:${LOCAL_GAMIFICATION_PORT} learning:${LOCAL_LEARNING_PORT} company:${LOCAL_COMPANY_PORT})"
cd "$REPO_ROOT/src/backend/gateway/Gateway"
exec "$DOTNET_BIN" run --project Sellevate.Gateway.csproj --no-launch-profile "$@"
