#!/usr/bin/env bash
#
# tenancy-default-organization-backfill.sh
# ----------------------------------------
# Phase 40.9 — put the existing, pre-tenancy installation into one organization.
#
# WHAT IT DOES
#   1. organization-db: creates the default organization in the tenant registry.
#   2. identity-db:     gives every existing user a membership in it, seeds the login
#                       configuration and the registry projection, and clears the removed
#                       global Admin role.
#
#   The SQL lives in docs/TENANCY/sql/40.9_*.sql — read those files, they are the migration.
#   This script only chooses the connection, passes one organization id to both databases, and
#   prints what is about to happen.
#
# WHAT IT DOES *NOT* DO
#   - It does not create schemas. Both services must have run their EF migrations first.
#   - It does not touch learning / ai / company / gamification / social / notification. Those
#     databases have no organization_id column yet; that is Stage C (roadmap 40.10+).
#   - It does not run by itself, from CI, or from any build. A human runs it.
#
# SAFETY
#   - Default mode is --dry-run: it reports what exists and prints every statement, writes nothing.
#   - --apply runs only the forward files, which contain no DELETE, no DROP and no UPDATE of a row
#     the migration did not create (the single exception, clearing the removed Admin role, records
#     the previous value first so --rollback can restore it).
#   - --rollback is destructive. It prints the full SQL first and refuses unless --i-have-a-backup
#     is also given. The SQL itself refuses if anything has joined the organization since the
#     backfill.
#   - Never point this at a production connection string before it has been run against a restored
#     copy of that database. See docs/MICROSERVICES_PRODUCTION_MIGRATION.md → "Раскатка тенантов".
#
# USAGE
#     ./scripts/tenancy-default-organization-backfill.sh                        # plan only
#     ./scripts/tenancy-default-organization-backfill.sh --apply
#     ./scripts/tenancy-default-organization-backfill.sh --rollback --i-have-a-backup
#
#   Connection comes from the repo .env, same as scripts/migrate-monolith-to-services.sh.
#   Overridable per run:
#     ORGANIZATION_ID     default 00000000-0000-4000-8000-000000000001
#     ORGANIZATION_NAME   default "Sellevate"
#     ORGANIZATION_SLUG   default "default"
#     IDENTITY_DB         default identity
#     ORGANIZATION_DB     default organization
#     PG_MODE             docker | host   (default: auto)
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SQL_DIR="$REPO_ROOT/docs/TENANCY/sql"

# The default organization id is a fixed, recognisable UUID rather than a fresh random one: the
# same value has to be written into two databases that have no foreign key between them, and a
# constant makes a re-run, a rollback and a support question all refer to the same thing.
ORGANIZATION_ID="${ORGANIZATION_ID:-00000000-0000-4000-8000-000000000001}"
ORGANIZATION_NAME="${ORGANIZATION_NAME:-Sellevate}"
ORGANIZATION_SLUG="${ORGANIZATION_SLUG:-default}"
IDENTITY_DB="${IDENTITY_DB:-identity}"
ORGANIZATION_DB="${ORGANIZATION_DB:-organization}"

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
HAS_BACKUP=0
for argument in "$@"; do
  case "$argument" in
    --dry-run)          MODE="dry-run" ;;
    --apply)            MODE="apply" ;;
    --rollback)         MODE="rollback" ;;
    --i-have-a-backup)  HAS_BACKUP=1 ;;
    -h|--help)          grep '^#' "$0" | sed 's/^# \{0,1\}//' | head -55; exit 0 ;;
    *) echo "Unknown argument: $argument" >&2; exit 2 ;;
  esac
done

log()  { printf '\033[0;36m[tenancy-backfill]\033[0m %s\n' "$*"; }
warn() { printf '\033[0;33m[tenancy-backfill] WARN:\033[0m %s\n' "$*"; }
die()  { printf '\033[0;31m[tenancy-backfill] ERROR:\033[0m %s\n' "$*" >&2; exit 1; }

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
  PG_CONNECTION="-h 127.0.0.1 -p $PG_INNER_PORT -U $PGUSER"
  psql_db() { local database="$1"; shift; docker exec -i -e PGPASSWORD="$PGPASSWORD" "$PG_CONTAINER_ID" psql $PG_CONNECTION -d "$database" "$@"; }
  log "Postgres access: docker exec into '$PG_CONTAINER'"
else
  command -v psql >/dev/null 2>&1 || die "PG_MODE=host but psql is not installed."
  PG_CONNECTION="-h $PGHOST -p $PGPORT -U $PGUSER"
  psql_db() { local database="$1"; shift; psql $PG_CONNECTION -d "$database" "$@"; }
  log "Postgres access: host psql -> $PGUSER@$PGHOST:$PGPORT"
fi

# Runs one of the docs/TENANCY/sql files. The file is piped in rather than passed as -f <path>
# because in docker mode it lives on the host, not inside the container.
run_sql_file() {
  local database="$1" sql_file="$2"
  psql_db "$database" \
    -v ON_ERROR_STOP=1 \
    -v organization_id="'$ORGANIZATION_ID'" \
    -v organization_name="'$ORGANIZATION_NAME'" \
    -v organization_slug="'$ORGANIZATION_SLUG'" \
    -f - < "$sql_file"
}

query_scalar() { psql_db "$1" -v ON_ERROR_STOP=1 -tAc "$2"; }

# Both services must have run their EF migrations first — this script backfills data into tables
# it does not create. Checking up front turns "relation does not exist" halfway through into one
# clear sentence before anything is attempted.
require_tables() {
  local database="$1"; shift
  local table missing=()
  for table in "$@"; do
    if [[ "$(query_scalar "$database" "SELECT to_regclass('public.\"$table\"') IS NOT NULL;")" != "t" ]]; then
      missing+=("$table")
    fi
  done
  if [[ ${#missing[@]} -gt 0 ]]; then
    die "database '$database' is missing: ${missing[*]}.
       Start the service once so its EF migrations run (or apply them with 'dotnet ef database update'), then re-run."
  fi
}

require_tables "$ORGANIZATION_DB" Organizations OrganizationProfiles
require_tables "$IDENTITY_DB" Users Memberships Invites OrganizationAuthConfigurations OrganizationReplicas

log "organization id : $ORGANIZATION_ID"
log "organization    : $ORGANIZATION_NAME ($ORGANIZATION_SLUG)"
log "databases       : $ORGANIZATION_DB, $IDENTITY_DB"
log "mode            : $MODE"

case "$MODE" in
  dry-run)
    log "--- plan (nothing will be written) ---"
    log "users in $IDENTITY_DB                 : $(query_scalar "$IDENTITY_DB" 'SELECT count(*) FROM "Users";')"
    log "users already holding a membership    : $(query_scalar "$IDENTITY_DB" 'SELECT count(DISTINCT "UserId") FROM "Memberships";')"
    log "users holding the removed Admin role  : $(query_scalar "$IDENTITY_DB" 'SELECT count(*) FROM "Users" WHERE "Role" = 1;')"
    log "invites without an organization       : $(query_scalar "$IDENTITY_DB" 'SELECT count(*) FROM "Invites" WHERE "OrganizationId" IS NULL;')"
    log "organizations already in the registry : $(query_scalar "$ORGANIZATION_DB" 'SELECT count(*) FROM "Organizations";')"
    echo
    log "The two files that would run, in this order:"
    echo "  1. $ORGANIZATION_DB  <- $SQL_DIR/40.9_default_organization_backfill_organization_db.sql"
    echo "  2. $IDENTITY_DB      <- $SQL_DIR/40.9_default_organization_backfill_identity_db.sql"
    echo
    log "Re-run with --apply to execute them."
    ;;

  apply)
    log "Applying to $ORGANIZATION_DB ..."
    run_sql_file "$ORGANIZATION_DB" "$SQL_DIR/40.9_default_organization_backfill_organization_db.sql"
    log "Applying to $IDENTITY_DB ..."
    run_sql_file "$IDENTITY_DB" "$SQL_DIR/40.9_default_organization_backfill_identity_db.sql"
    log "Done. Every existing user now belongs to $ORGANIZATION_NAME."
    ;;

  rollback)
    warn "ROLLBACK is destructive: it DELETEs memberships, the auth configuration, the registry"
    warn "projection and the organization row, and DROPs the bookkeeping tables."
    echo
    warn "The exact SQL that will run (SAFETY RULES, CLAUDE.md):"
    echo "----------------------------------------------------------------------"
    cat "$SQL_DIR/40.9_default_organization_rollback_identity_db.sql"
    echo "----------------------------------------------------------------------"
    cat "$SQL_DIR/40.9_default_organization_rollback_organization_db.sql"
    echo "----------------------------------------------------------------------"
    echo
    [[ "$HAS_BACKUP" -eq 1 ]] || die "refusing to roll back without --i-have-a-backup."

    log "Rolling back $IDENTITY_DB ..."
    run_sql_file "$IDENTITY_DB" "$SQL_DIR/40.9_default_organization_rollback_identity_db.sql"
    log "Rolling back $ORGANIZATION_DB ..."
    run_sql_file "$ORGANIZATION_DB" "$SQL_DIR/40.9_default_organization_rollback_organization_db.sql"
    log "Done. The installation is back to its pre-40.9 state."
    ;;
esac
