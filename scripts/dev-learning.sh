#!/usr/bin/env bash
# Run the Learning microservice LOCALLY on the host (Phase 8 of the microservices
# migration). Infra (Postgres, Redis, Kafka, Loki) must already be running
# (scripts/dev-infra.sh). Learning owns its own database (learning) on the shared
# local Postgres instance and creates it on first start. For AI-graded exercise types
# it calls the AI service POST /ai/evaluate, so run scripts/dev-ai.sh alongside it.
#
# Exercise the full flow through the gateway by also running scripts/dev-gateway.sh:
# the gateway flips /skills, /skill-tree, /lessons, /topics, /exercises, /reference,
# /techniques, /daily-quote and the learning /admin/* routes to this service.
set -euo pipefail

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib-local-env.sh"

load_root_env
export_learning_env

echo "==> Learning -> http://localhost:${LOCAL_LEARNING_PORT} (db: learning on localhost:${LOCAL_POSTGRES_PORT})"
cd "$REPO_ROOT/src/backend/learning-service/Learning"
exec "$DOTNET_BIN" run --project Sellevate.Learning.csproj --no-launch-profile "$@"
