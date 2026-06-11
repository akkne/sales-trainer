#!/usr/bin/env bash
# One-shot LOCAL dev launcher:
#   1. infra in Docker (docker-compose.infra.yml)
#   2. backend (.NET) on the host
#   3. frontend (Next.js) on the host
#
# Backend & frontend run in the background; their logs go to logs/.
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

# 2) Backend (background)
echo "==> Launching backend in background -> $LOG_DIR/backend.log"
nohup "$SCRIPT_DIR/dev-backend.sh" > "$LOG_DIR/backend.log" 2>&1 &
echo $! > "$PID_DIR/backend.pid"

# 3) Frontend (background)
echo "==> Launching frontend in background -> $LOG_DIR/frontend.log"
nohup "$SCRIPT_DIR/dev-frontend.sh" > "$LOG_DIR/frontend.log" 2>&1 &
echo $! > "$PID_DIR/frontend.pid"

cat <<EOF

==> Local dev stack starting.
    Backend  : http://localhost:${LOCAL_BACKEND_PORT}   (logs: logs/backend.log)
    Frontend : http://localhost:${LOCAL_FRONTEND_PORT}   (logs: logs/frontend.log)

    Tail logs:  tail -f logs/backend.log logs/frontend.log
    Stop all :  scripts/dev-down.sh
EOF
