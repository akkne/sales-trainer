#!/usr/bin/env bash
# Run the Identity microservice LOCALLY on the host (Phase 2 of the microservices
# migration). Infra (Postgres, Kafka, MinIO, Loki) must already be running
# (scripts/dev-infra.sh). Identity owns its own database (identity-db) on the shared
# local Postgres instance and creates it on first start.
#
# Exercise the full flow through the gateway by also running scripts/dev-gateway.sh:
# the gateway flips /auth, /demo, /profile, /onboarding, /avatars to this service.
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

load_root_env
export_identity_env

echo "==> Identity -> http://localhost:${LOCAL_IDENTITY_PORT} (db: identity on localhost:${LOCAL_POSTGRES_PORT})"
cd "$REPO_ROOT/src/backend/identity-service/Identity"
exec "$DOTNET_BIN" run --project Sellevate.Identity.csproj --no-launch-profile "$@"
