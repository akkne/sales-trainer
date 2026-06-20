#!/usr/bin/env bash
# Run the Social microservice LOCALLY on the host (Phase 5 of the microservices
# migration). Infra (Postgres, Mongo, Redis, Kafka, MinIO, Loki) must already be
# running (scripts/dev-infra.sh). Social owns its own database (social) on the shared
# local Postgres instance and creates it on first start; it reuses the shared Mongo
# (chat_conversations) and MinIO (Discuss photos).
#
# Exercise the full flow through the gateway by also running scripts/dev-gateway.sh:
# the gateway flips /friends, /discuss, /admin/discuss, /chat to this service.
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

load_root_env
export_social_env

echo "==> Social -> http://localhost:${LOCAL_SOCIAL_PORT} (db: social on localhost:${LOCAL_POSTGRES_PORT})"
cd "$REPO_ROOT/src/backend/social-service/Social"
exec "$DOTNET_BIN" run --project Sellevate.Social.csproj --no-launch-profile "$@"
