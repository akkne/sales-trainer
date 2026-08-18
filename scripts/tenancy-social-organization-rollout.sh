#!/usr/bin/env bash
#
# tenancy-social-organization-rollout.sh
# --------------------------------------
# Phase 40.13 — roll `organization_id` out across social-service's two stores.
#
# WHAT IT DOES
#   backfill : docs/TENANCY/sql/40.13_social_organization_backfill.sql
#              moves every pre-existing friendship, thread, reply, vote, thread-tag link and photo
#              off the all-zeros placeholder and into the default organization, and moves
#              user-typed DiscussTags there too while leaving the curated vocabulary global.
#              Until it runs, RLS hides those rows from everybody — fail-closed as designed, but
#              indistinguishable from "the forum and my friends are gone" to a user.
#   mongo    : docs/TENANCY/mongo/40.13_chat_conversations_organization_backfill.js
#              stamps every chat_conversations document with the organization and builds the three
#              compound indexes that lead with it. Mongo has no RLS, so the same fail-closed shape
#              comes from ChatConversationRepository's filter: an unstamped conversation is
#              invisible, not leaked — and the chat list stays empty until this runs.
#   indexes  : docs/TENANCY/sql/40.13_social_organization_indexes_concurrently.sql
#              rebuilds social-db's read indexes with "OrganizationId" first using
#              CREATE INDEX CONCURRENTLY, checks pg_index.indisvalid before AND after dropping the
#              superseded ones, and keeps every cascade-delete FK indexed. Safe with the service up;
#              deliberately NOT part of any EF migration or of DatabaseBootstrapper, because
#              Database.Migrate() runs at service startup and a concurrent build cannot live there.
#
#   The two unique-index swaps that cannot wait (DiscussTags.Slug and the two Friendships pair
#   indexes) are inside the EF migration instead, not here: without them the second customer is
#   broken the moment the deploy lands.
#
#   The SQL and JS files are the migration — read them. This script only chooses the connection,
#   passes the organization id in, and refuses to run steps out of order.
#
# WHAT IT DOES *NOT* DO
#   - It does not create or migrate the schema. social-service must have run its EF migration first
#     (20260816081204_AddOrganizationId); the script checks and stops if it has not.
#   - It never runs by itself, from CI, or from any build. A human runs it.
#   - It does not touch MinIO. New photo uploads are written under org/{organizationId}/…; existing
#     objects keep their old keys and are still served, because the key is read from the
#     DiscussPhotos row and never recomputed. Renaming live objects would be an operation on live
#     infrastructure for zero correctness gain.
#   - There is no --rollback. Undoing 40.13 in Postgres means reverting the EF migration, whose
#     Down drops the columns and the RLS policies. In Mongo it means
#     `db.chat_conversations.updateMany({}, { $unset: { organizationId: "" } })`, written out here
#     rather than scripted precisely because nobody should run it casually.
#
# SAFETY
#   - Default mode is --dry-run: it counts what would change and writes nothing.
#   - Every step is idempotent; re-running after a partial failure finishes the job rather than
#     re-pointing anything that already has an owner.
#   - Never point this at production before running it against a restored copy. See
#     docs/MICROSERVICES_PRODUCTION_MIGRATION.md → "Раскатка тенантов" and docs/DONT_FORGET.md.
#
# USAGE
#     ./scripts/tenancy-social-organization-rollout.sh              # plan only
#     ./scripts/tenancy-social-organization-rollout.sh --backfill   # step 1 (Postgres)
#     ./scripts/tenancy-social-organization-rollout.sh --mongo      # step 2 (chat history)
#     ./scripts/tenancy-social-organization-rollout.sh --indexes    # step 3 (performance, later)
#
#   Connection comes from the repo .env, same as the 40.10–40.12 rollout scripts.
#   Overridable per run:
#     ORGANIZATION_ID   default 00000000-0000-4000-8000-000000000001 (must match 40.9–40.12)
#     SOCIAL_DB         default social
#     MONGO_URI         default mongodb://127.0.0.1:27018
#     MONGO_DB          default sallevate
#     PG_MODE           docker | host   (default: auto)
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SQL_DIR="$REPO_ROOT/docs/TENANCY/sql"
MONGO_DIR="$REPO_ROOT/docs/TENANCY/mongo"

ORGANIZATION_ID="${ORGANIZATION_ID:-00000000-0000-4000-8000-000000000001}"
SOCIAL_DB="${SOCIAL_DB:-social}"
MONGO_DB="${MONGO_DB:-sallevate}"
MONGO_URI="${MONGO_URI:-mongodb://127.0.0.1:27018}"

dotenv_get() {
  local key="$1" file="$2" value
  [[ -f "$file" ]] || return 0
  value="$(grep -E "^[[:space:]]*${key}=" "$file" | tail -n1)" || return 0
  value="${value#*=}"
  value="${value%\"}"; value="${value#\"}"
  value="${value%\'}"; value="${value#\'}"
  printf '%s' "$value"
}

POSTGRES_USER="${POSTGRES_USER:-$(dotenv_get POSTGRES_USER "$REPO_ROOT/.env")}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-$(dotenv_get POSTGRES_PASSWORD "$REPO_ROOT/.env")}"

PGUSER="${PGUSER:-${POSTGRES_USER:-st}}"
export PGPASSWORD="${PGPASSWORD:-${POSTGRES_PASSWORD:-}}"

PGHOST="${PGHOST:-127.0.0.1}"
PGPORT="${PGPORT:-5433}"
PG_CONTAINER="${PG_CONTAINER:-postgres}"
PG_INNER_PORT="${PG_INNER_PORT:-5432}"
COMPOSE_FILE="${COMPOSE_FILE:-$REPO_ROOT/docker-compose.yml}"
PG_MODE="${PG_MODE:-auto}"

MODE="dry-run"
for argument in "$@"; do
  case "$argument" in
    --dry-run)  MODE="dry-run" ;;
    --backfill) MODE="backfill" ;;
    --mongo)    MODE="mongo" ;;
    --indexes)  MODE="indexes" ;;
    -h|--help)  grep '^#' "$0" | sed 's/^# \{0,1\}//' | head -68; exit 0 ;;
    *) echo "Unknown argument: $argument" >&2; exit 2 ;;
  esac
done

log()  { printf '\033[0;36m[tenancy-40.13-social]\033[0m %s\n' "$*"; }
die()  { printf '\033[0;31m[tenancy-40.13-social] ERROR:\033[0m %s\n' "$*" >&2; exit 1; }

DOCKER_COMPOSE=""
if command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1; then
  DOCKER_COMPOSE="docker compose -f $COMPOSE_FILE --project-directory $REPO_ROOT"
elif command -v docker-compose >/dev/null 2>&1; then
  DOCKER_COMPOSE="docker-compose -f $COMPOSE_FILE --project-directory $REPO_ROOT"
fi

container_running() {
  [[ -n "$DOCKER_COMPOSE" ]] || return 1
  local container_id
  container_id="$($DOCKER_COMPOSE ps -q "$PG_CONTAINER" 2>/dev/null | head -n1)" || return 1
  [[ -n "$container_id" ]] || return 1
  [[ "$(docker inspect -f '{{.State.Running}}' "$container_id" 2>/dev/null)" == "true" ]]
}

if [[ "$PG_MODE" == "auto" ]]; then
  if container_running; then PG_MODE="docker"
  elif command -v psql >/dev/null 2>&1; then PG_MODE="host"
  else die "no way to reach Postgres: the '$PG_CONTAINER' container is not running and psql is not on the host."
  fi
fi

if [[ "$PG_MODE" == "docker" ]]; then
  container_running || die "PG_MODE=docker but container '$PG_CONTAINER' is not running (compose file: $COMPOSE_FILE)."
  PG_CONTAINER_ID="$($DOCKER_COMPOSE ps -q "$PG_CONTAINER" 2>/dev/null | head -n1)"
  [[ -n "$PG_CONTAINER_ID" ]] || die "could not resolve container id for service '$PG_CONTAINER'"
  psql_db() { local database="$1"; shift; docker exec -i -e PGPASSWORD="$PGPASSWORD" "$PG_CONTAINER_ID" psql -h 127.0.0.1 -p "$PG_INNER_PORT" -U "$PGUSER" -d "$database" "$@"; }
  log "Postgres access: docker exec into '$PG_CONTAINER'"
else
  command -v psql >/dev/null 2>&1 || die "PG_MODE=host but psql is not installed."
  psql_db() { local database="$1"; shift; psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$database" "$@"; }
  log "Postgres access: host psql -> $PGUSER@$PGHOST:$PGPORT"
fi

query_scalar() { psql_db "$1" -v ON_ERROR_STOP=1 -tAc "$2"; }

# The SQL files live on the host, so they are piped in rather than passed as -f <path>: in docker
# mode the container cannot see the repository.
run_sql_file() {
  local sql_file="$1"
  psql_db "$SOCIAL_DB" \
    -v ON_ERROR_STOP=1 \
    -v organization_id="'$ORGANIZATION_ID'" \
    -f - < "$sql_file"
}

run_mongo_script() {
  local apply="$1"
  command -v mongosh >/dev/null 2>&1 || die "mongosh is not installed — it is the only supported way to run the Mongo step."
  mongosh "$MONGO_URI/$MONGO_DB" \
    --quiet \
    --eval "var ORGANIZATION_ID = \"$ORGANIZATION_ID\", APPLY = $apply" \
    "$MONGO_DIR/40.13_chat_conversations_organization_backfill.js"
}

require_migration_applied() {
  if [[ "$(query_scalar "$SOCIAL_DB" "SELECT to_regclass('public.\"Friendships\"') IS NOT NULL;")" != "t" ]]; then
    die "database '$SOCIAL_DB' has no Friendships table — is this the right database?"
  fi
  local has_column
  has_column="$(query_scalar "$SOCIAL_DB" "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Friendships' AND column_name = 'OrganizationId');")"
  if [[ "$has_column" != "t" ]]; then
    die "social-db has not run the 40.13 EF migration yet (Friendships has no OrganizationId).
       Start social-service once so Database.Migrate() applies 20260816081204_AddOrganizationId,
       or apply it with 'dotnet ef database update', then re-run."
  fi
}

require_migration_applied

PLACEHOLDER_ORGANIZATION_ID="00000000-0000-0000-0000-000000000000"

log "organization id : $ORGANIZATION_ID"
log "postgres db     : $SOCIAL_DB"
log "mongo           : $MONGO_URI/$MONGO_DB"
log "mode            : $MODE"

case "$MODE" in
  dry-run)
    log "--- plan (nothing will be written) ---"
    for table in Friendships DiscussThreads DiscussReplies DiscussVotes DiscussThreadTags DiscussPhotos; do
      log "$table still on the placeholder organization: $(query_scalar "$SOCIAL_DB" "SELECT count(*) FROM \"$table\" WHERE \"OrganizationId\" = '$PLACEHOLDER_ORGANIZATION_ID';")"
    done
    log "DiscussTags global (curated vocabulary, stays global): $(query_scalar "$SOCIAL_DB" 'SELECT count(*) FROM "DiscussTags" WHERE "OrganizationId" IS NULL;')"
    log "invalid indexes right now (a failed concurrent build): $(query_scalar "$SOCIAL_DB" "SELECT count(*) FROM pg_index JOIN pg_class ON pg_class.oid = pg_index.indexrelid JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace WHERE pg_namespace.nspname = 'public' AND NOT pg_index.indisvalid;")"
    echo
    log "--- mongo (dry run) ---"
    run_mongo_script false
    echo
    log "Re-run with --backfill, then --mongo (both user-visible), then --indexes."
    ;;

  backfill)
    log "Applying $SQL_DIR/40.13_social_organization_backfill.sql ..."
    run_sql_file "$SQL_DIR/40.13_social_organization_backfill.sql"
    log "Done. NEXT: --mongo, or the chat list stays empty."
    ;;

  mongo)
    log "Applying $MONGO_DIR/40.13_chat_conversations_organization_backfill.js ..."
    run_mongo_script true
    log "Done. Open the chat of a real user and check their conversation history is still there."
    ;;

  indexes)
    log "Applying $SQL_DIR/40.13_social_organization_indexes_concurrently.sql ..."
    log "This can take a while on a large table; it holds no exclusive lock while it runs."
    run_sql_file "$SQL_DIR/40.13_social_organization_indexes_concurrently.sql"
    log "Done. Social reads are indexed per organization and every index reported valid."
    ;;
esac
