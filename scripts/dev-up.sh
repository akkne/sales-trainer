#!/usr/bin/env bash
# One-shot LOCAL dev launcher:
#   1. infra in Docker (docker-compose.infra.yml)
#   2. frontend (Next.js) on the host
#
# The monolith (src/backend/api) has been removed from main (kept on the
# `monolith-legacy` branch). The frontend talks to the API gateway, which routes
# to the per-service backends.
#
# The frontend runs in the background; its logs go to logs/.
# Stop everything with scripts/dev-down.sh.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/lib-local-env.sh"

LOG_DIR="$REPO_ROOT/logs"
PID_DIR="$REPO_ROOT/.local-run"
mkdir -p "$LOG_DIR" "$PID_DIR"

# 1) Infra
"$SCRIPT_DIR/dev-infra.sh"

echo "==> Waiting for Postgres to be healthy..."
for _ in $(seq 1 30); do
  if "$DOCKER_BIN" compose -f "$REPO_ROOT/docker-compose.infra.yml" ps postgres \
      | grep -q "healthy"; then
    break
  fi
  sleep 2
done

# 2) Frontend (background)
echo "==> Launching frontend in background -> $LOG_DIR/frontend.log"
nohup "$SCRIPT_DIR/dev-frontend.sh" > "$LOG_DIR/frontend.log" 2>&1 &
echo $! > "$PID_DIR/frontend.pid"

cat <<EOF

==> Local dev stack starting.
    Frontend : http://localhost:${LOCAL_FRONTEND_PORT}   (logs: logs/frontend.log)

    The monolith is retired; start the per-service backends + gateway as needed
    (scripts/dev-gateway.sh, scripts/dev-identity.sh, scripts/dev-company.sh, ...).

    Tail logs:  tail -f logs/frontend.log
    Stop all :  scripts/dev-down.sh
EOF
