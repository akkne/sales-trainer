#!/usr/bin/env bash
#
# tenancy-gamification-organization-rollout.sh
# ----------------------------------------
# Phase 40.13 — roll `organization_id` out across gamification-db.
#
# WHAT IT DOES
#   backfill : docs/TENANCY/sql/40.13_gamification_organization_backfill.sql
#              gives every pre-existing row in all seven tenant tables the default organization.
#              The catalogues and installation-wide configuration (Achievements, LeagueTiers,
#              GamificationSettings, StreakMilestones, ExerciseTypeRewards), the cross-organization
#              UserReplicas projection and OutboxMessages have no organization column at all and are
#              left alone. Run with gamification-service stopped, right after its EF migration has
#              applied: until it runs, row-level security hides every XP record, streak, achievement,
#              league and league period from everybody.
#   indexes  : docs/TENANCY/sql/40.13_gamification_organization_indexes_concurrently.sql
#              rebuilds the read indexes on the two tables that grow without bound (UserXpRecords
#              and LeagueMemberships) with "OrganizationId" first, adds a plain ("LeagueId") index so
#              the FK to Leagues stays indexed, checks pg_index.indisvalid, and only then drops the
#              superseded ones. Safe to run with the service up; deliberately NOT part of any EF
#              migration or of DatabaseBootstrapper (it would stall readiness and race the replicas).
#
#              Unlike 40.10-40.12 the EF migration DOES do some index work: four constraints in this
#              database are load-bearing for correctness in the window between deploy and this
#              script, so the migration swaps them itself. See the migration's remarks.
#
#   The SQL files are the migration — read them. This script only chooses the connection, passes
#   the organization id in, and refuses to run steps out of order.
#
# WHAT IT DOES *NOT* DO
#   - It does not create or migrate the schema. gamification-service must have run its EF migrations
#     first (20260815213223_AddOrganizationId); the script checks and stops if it has not.
#   - It never runs by itself, from CI, or from any build. A human runs it.
#   - There is no --rollback. Undoing 40.13 means reverting the EF migration
#     ('dotnet ef database update 20260814200714_AddOutboxMessageOrganizationId'), whose Down drops
#     the columns and the RLS policies — and the indexes with them, since Postgres drops indexes
#     that reference a dropped column. A separate destructive rollback file would only duplicate it.
#
# SAFETY
#   - Default mode is --dry-run: it counts what would change and writes nothing.
#   - The backfill SQL is idempotent, contains no DELETE / DROP / TRUNCATE, and refuses to re-point
#     a database that was already backfilled at a different organization.
#   - Never point this at production before running it against a restored copy. See
#     docs/MICROSERVICES_PRODUCTION_MIGRATION.md → "Раскатка тенантов" and docs/DONT_FORGET.md.
#
# USAGE
#     ./scripts/tenancy-gamification-organization-rollout.sh                  # plan only
#     ./scripts/tenancy-gamification-organization-rollout.sh --backfill
#     ./scripts/tenancy-gamification-organization-rollout.sh --indexes
#
#   Connection comes from the repo .env, same as scripts/tenancy-default-organization-backfill.sh.
#   Overridable per run:
#     ORGANIZATION_ID   default 00000000-0000-4000-8000-000000000001 (must match 40.9)
#     GAMIFICATION_DB       default gamification
#     PG_MODE           docker | host   (default: auto)
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SQL_DIR="$REPO_ROOT/docs/TENANCY/sql"

ORGANIZATION_ID="${ORGANIZATION_ID:-00000000-0000-4000-8000-000000000001}"
GAMIFICATION_DB="${GAMIFICATION_DB:-gamification}"

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
    --indexes)  MODE="indexes" ;;
    -h|--help)  grep '^#' "$0" | sed 's/^# \{0,1\}//' | head -47; exit 0 ;;
    *) echo "Unknown argument: $argument" >&2; exit 2 ;;
  esac
done

log()  { printf '\033[0;36m[tenancy-40.13]\033[0m %s\n' "$*"; }
die()  { printf '\033[0;31m[tenancy-40.13] ERROR:\033[0m %s\n' "$*" >&2; exit 1; }

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
  psql_db "$GAMIFICATION_DB" \
    -v ON_ERROR_STOP=1 \
    -v organization_id="'$ORGANIZATION_ID'" \
    -f - < "$sql_file"
}

require_migration_applied() {
  if [[ "$(query_scalar "$GAMIFICATION_DB" "SELECT to_regclass('public.\"UserXpRecords\"') IS NOT NULL;")" != "t" ]]; then
    die "database '$GAMIFICATION_DB' has no UserXpRecords table — is this the right database?"
  fi
  local has_column
  has_column="$(query_scalar "$GAMIFICATION_DB" "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'UserXpRecords' AND column_name = 'OrganizationId');")"
  if [[ "$has_column" != "t" ]]; then
    die "gamification-db has not run the 40.13 EF migration yet (UserXpRecords has no OrganizationId).
       Start gamification-service once so Database.Migrate() applies 20260815213223_AddOrganizationId,
       or apply it with 'dotnet ef database update', then re-run."
  fi
}

require_migration_applied

PLACEHOLDER="00000000-0000-0000-0000-000000000000"

log "organization id : $ORGANIZATION_ID"
log "database        : $GAMIFICATION_DB"
log "mode            : $MODE"

case "$MODE" in
  dry-run)
    log "--- plan (nothing will be written) ---"
    for table in UserXpRecords UserStreaks UserLearningProgress UserAchievements LeagueSettings Leagues LeagueMemberships; do
      log "$(printf '%-22s' "$table") rows awaiting an owner: $(query_scalar "$GAMIFICATION_DB" "SELECT count(*) FROM \"$table\" WHERE \"OrganizationId\" = '$PLACEHOLDER';")"
    done
    log "league memberships in a different organization than their league (must be 0): $(query_scalar "$GAMIFICATION_DB" 'SELECT count(*) FROM "LeagueMemberships" m JOIN "Leagues" l ON l."Id" = m."LeagueId" WHERE m."OrganizationId" <> l."OrganizationId";')"
    log "league settings rows (one per organization after 40.13): $(query_scalar "$GAMIFICATION_DB" 'SELECT count(*) FROM "LeagueSettings";')"
    log "invalid indexes right now (a failed concurrent build): $(query_scalar "$GAMIFICATION_DB" "SELECT count(*) FROM pg_index JOIN pg_class ON pg_class.oid = pg_index.indexrelid JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace WHERE pg_namespace.nspname = 'public' AND NOT pg_index.indisvalid;")"
    echo
    log "Re-run with --backfill (service stopped), then --indexes (service may be up)."
    ;;

  backfill)
    log "Applying $SQL_DIR/40.13_gamification_organization_backfill.sql ..."
    run_sql_file "$SQL_DIR/40.13_gamification_organization_backfill.sql"
    log "Done. Start gamification-service and check that a real user still sees their XP and streak."
    ;;

  indexes)
    if [[ "$(query_scalar "$GAMIFICATION_DB" "SELECT count(*) FROM \"UserXpRecords\" WHERE \"OrganizationId\" = '$PLACEHOLDER';")" != "0" ]]; then
      die "there are still rows in the placeholder organization — run --backfill first."
    fi
    log "Applying $SQL_DIR/40.13_gamification_organization_indexes_concurrently.sql ..."
    log "This can take a while on a large table; it holds no exclusive lock while it runs."
    run_sql_file "$SQL_DIR/40.13_gamification_organization_indexes_concurrently.sql"
    log "Done. The two growing tables lead with OrganizationId, the FK to Leagues stays indexed, every index valid."
    ;;
esac
