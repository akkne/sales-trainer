#!/usr/bin/env bash
# Run the Company microservice LOCALLY on the host (Phase 39.4 of the Companies feature).
# Infra (Postgres, Loki) must already be running (scripts/dev-infra.sh). Company owns its
# own database (company) on the shared local Postgres instance and creates it on first
# start. It has no Kafka, Redis, or Mongo dependency.
#
# Exercise the full flow through the gateway by also running scripts/dev-gateway.sh:
# the gateway flips /companies to this service.
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

load_root_env
export_company_env

echo "==> Company -> http://localhost:${LOCAL_COMPANY_PORT} (db: company on localhost:${LOCAL_POSTGRES_PORT})"
cd "$REPO_ROOT/src/backend/company-service/Company"
exec "$DOTNET_BIN" run --project Sellevate.Company.csproj --no-launch-profile "$@"
