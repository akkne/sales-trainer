#!/usr/bin/env bash
# RETIRED: the monolith (src/backend/api) no longer serves traffic. All routes were
# extracted into per-service backends and the gateway dropped its catch-all to the
# monolith (Phase 9). The code is kept in the repo as a reference only.
#
# This script remains solely so the reference monolith can still be launched on demand
# (e.g. to compare behaviour). It is NOT part of the default local-dev stack and is no
# longer started by scripts/dev-up.sh. Pass --force to run it anyway.
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

if [[ "${1:-}" != "--force" ]]; then
  cat >&2 <<'EOF'
The monolith is retired and not part of the local-dev stack.
It is kept as a reference only. Re-run with --force to launch it anyway.
EOF
  exit 0
fi
shift

load_root_env
export_backend_env

echo "==> [reference] Monolith -> http://localhost:${LOCAL_BACKEND_PORT} (ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT)"
cd "$REPO_ROOT/src/backend/api"
# --no-launch-profile: ignore Properties/launchSettings.json, whose http profile
# pins applicationUrl=http://localhost:5188 and would override ASPNETCORE_URLS.
exec "$DOTNET_BIN" run --project Sellevate.Api.csproj --no-launch-profile "$@"
