#!/usr/bin/env bash
# Stop the LOCAL dev stack started by scripts/dev-up.sh:
# kills the host backend/frontend processes and stops the Docker infra.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/lib-local-env.sh"

PID_DIR="$REPO_ROOT/.local-run"

stop_pid() {
  local name="$1" file="$PID_DIR/$1.pid"
  if [[ -f "$file" ]]; then
    local pid; pid="$(cat "$file")"
    if kill -0 "$pid" 2>/dev/null; then
      echo "==> Stopping $name (pid $pid) and its children..."
      pkill -P "$pid" 2>/dev/null || true
      kill "$pid" 2>/dev/null || true
    fi
    rm -f "$file"
  fi
}

stop_pid backend
stop_pid frontend

# Backstop: free the ports in case child procs (dotnet/node) outlived the wrapper.
for port in "$LOCAL_BACKEND_PORT" "$LOCAL_FRONTEND_PORT"; do
  lsof -ti "tcp:${port}" 2>/dev/null | xargs -r kill 2>/dev/null || true
done

echo "==> Stopping Docker infrastructure..."
"$DOCKER_BIN" compose -f "$REPO_ROOT/docker-compose.infra.yml" stop

echo "==> Local dev stack stopped."
