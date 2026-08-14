#!/usr/bin/env bash
# Run the Organization microservice LOCALLY on the host (Phase 40.5 — the tenant registry
# scaffold). Infra (Postgres, Loki, Kafka) must already be running (scripts/dev-infra.sh).
# Organization owns its own database (organization) on the shared local Postgres instance and
# creates it on first start. It has no Redis or Mongo dependency.
#
# Produces organization.created / organization.updated / organization.suspended to Kafka; never
# consumes, so a broker outage is logged and tolerated at startup, not fatal.
#
# Exercise the full flow through the gateway by also running scripts/dev-gateway.sh:
# the gateway flips /organizations to this service.
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

load_root_env
export_organization_env

echo "==> Organization -> http://localhost:${LOCAL_ORGANIZATION_PORT} (db: organization on localhost:${LOCAL_POSTGRES_PORT})"
cd "$REPO_ROOT/src/backend/organization-service/Organization"
exec "$DOTNET_BIN" run --project Sellevate.Organization.csproj --no-launch-profile "$@"
