#!/usr/bin/env bash
# Run the backend (.NET) LOCALLY on the host, pointed at the Docker infra.
# Infra must already be running (scripts/dev-infra.sh).
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

load_root_env
export_backend_env

echo "==> Backend -> http://localhost:${LOCAL_BACKEND_PORT} (ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT)"
cd "$REPO_ROOT/src/backend/api"
# --no-launch-profile: ignore Properties/launchSettings.json, whose http profile
# pins applicationUrl=http://localhost:5188 and would override ASPNETCORE_URLS.
exec "$DOTNET_BIN" run --project Sellevate.Api.csproj --no-launch-profile "$@"
