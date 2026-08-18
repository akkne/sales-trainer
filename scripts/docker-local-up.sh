#!/usr/bin/env bash
# Run the FULL stack in Docker on this machine — every service, not just infra.
#
# This is the deploy shape (docker-compose.yml) plus docker-compose.local.yml,
# which retargets the two things that only make sense on a server:
#   * gateway is published on 5001 (macOS holds :5000 for AirPlay Receiver)
#   * the frontend image is built against the local gateway, not the production API
#
# The default development profile is still scripts/dev-up.sh (apps on the host,
# hot reload). Use this one when you want to exercise the real container topology.
# See docs/LOCAL_DEV.md.
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

cd "$REPO_ROOT"

DOCKER_LOCAL_GATEWAY_PORT="${GATEWAY_HOST_PORT:-5001}"
COMPOSE_FILES=(-f docker-compose.yml -f docker-compose.local.yml)

if [[ ! -f .env ]]; then
  echo "ERROR: .env not found. Copy .env.example to .env and fill it in." >&2
  exit 1
fi

# The host-profile scripts bind the same ports; running both profiles at once
# just produces confusing "port is already allocated" failures.
if lsof -nP -iTCP:"$LOCAL_FRONTEND_PORT" -sTCP:LISTEN >/dev/null 2>&1; then
  echo "WARNING: port $LOCAL_FRONTEND_PORT is already in use — is scripts/dev-up.sh running?" >&2
  echo "         Stop it with scripts/dev-down.sh first." >&2
fi

# Build one service at a time: `docker compose build` otherwise runs all nine
# concurrently, which is enough to exhaust a modest Docker VM.
#
# Each build is retried. The SIGILL class of failure is handled by building on the
# Alpine SDK (see docker-compose.local.yml), but restore still goes over the network
# on a cold cache and nuget.org times out readily on a slow link. A retry is cheap
# because every layer up to the failing one is cached. See docs/LOCAL_DEV.md.
BUILD_ATTEMPTS="${BUILD_ATTEMPTS:-3}"

echo "==> Building images sequentially (this takes a while on a cold cache)..."
failed=()
for service in identity ai analytics notification gamification social learning company gateway frontend; do
  built=0
  for attempt in $(seq 1 "$BUILD_ATTEMPTS"); do
    echo "    --> $service (attempt $attempt/$BUILD_ATTEMPTS)"
    if "$DOCKER_BIN" compose "${COMPOSE_FILES[@]}" build "$service"; then
      built=1
      break
    fi
    echo "    !!! $service failed on attempt $attempt" >&2
  done
  (( built )) || failed+=("$service")
done

if (( ${#failed[@]} )); then
  echo "==> ERROR: these services did not build after $BUILD_ATTEMPTS attempts: ${failed[*]}" >&2
  echo "    The stack was NOT started. Re-run to retry, or check the error above." >&2
  exit 1
fi

echo "==> Starting the stack..."
"$DOCKER_BIN" compose "${COMPOSE_FILES[@]}" up -d "$@"

echo "==> Full Docker stack is up:"
echo "    Frontend     http://localhost:${LOCAL_FRONTEND_PORT}"
echo "    Gateway/API  http://localhost:${DOCKER_LOCAL_GATEWAY_PORT}"
echo "    Grafana      http://localhost:3001"
echo "    Kafka UI     http://localhost:${LOCAL_KAFKA_UI_PORT}"
echo "    MinIO console http://localhost:9001"
echo
echo "    Logs:  docker compose ${COMPOSE_FILES[*]} logs -f <service>"
echo "    Stop:  scripts/docker-local-down.sh"
