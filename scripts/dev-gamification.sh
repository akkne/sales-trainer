#!/usr/bin/env bash
# Run the Gamification microservice LOCALLY on the host (Phase 7 of the microservices
# migration). Infra (Postgres, Redis, Kafka, Loki) must already be running
# (scripts/dev-infra.sh). Gamification owns its own database (gamification) on the
# shared local Postgres instance and creates it on first start.
#
# Exercise the full flow through the gateway by also running scripts/dev-gateway.sh:
# the gateway flips /gamification, /league, /profile/achievements, /admin/gamification,
# /admin/leagues to this service.
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

load_root_env
export_gamification_env

echo "==> Gamification -> http://localhost:${LOCAL_GAMIFICATION_PORT} (db: gamification on localhost:${LOCAL_POSTGRES_PORT})"
cd "$REPO_ROOT/src/backend/gamification-service/Gamification"
exec "$DOTNET_BIN" run --project Sellevate.Gamification.csproj --no-launch-profile "$@"
