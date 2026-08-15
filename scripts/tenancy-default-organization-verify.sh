#!/usr/bin/env bash
#
# tenancy-default-organization-verify.sh
# --------------------------------------
# Proves that the Phase 40.9 backfill and its rollback are correct, WITHOUT going anywhere near
# real data.
#
# HOW
#   1. Creates two throwaway databases (default: tenancy_verify_identity / tenancy_verify_organization).
#      If they already exist they are dropped first — they belong to this script and to nothing else.
#   2. Builds their schema from the services' own EF migrations
#      (`dotnet ef migrations script --idempotent`), so the verification runs against the real
#      schema rather than a hand-written imitation of it.
#   3. Seeds a small but awkward fixture: an ordinary user, a user holding the removed global Admin
#      role (value 1), a platform SuperAdmin, and a user who already has a membership.
#   4. Snapshots the fixture.
#   5. Runs the forward migration and asserts its outcome.
#   6. Runs the rollback and asserts the database is byte-for-byte back to the snapshot.
#   7. Drops both throwaway databases.
#
# The DROP DATABASE statements in here target only the two names above, which this script created.
#
# USAGE
#     ./scripts/tenancy-default-organization-verify.sh
#
#   Same connection resolution as scripts/tenancy-default-organization-backfill.sh.
#     VERIFY_IDENTITY_DB      default tenancy_verify_identity
#     VERIFY_ORGANIZATION_DB  default tenancy_verify_organization
#     PG_MODE                 docker | host  (default: auto)
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SQL_DIR="$REPO_ROOT/docs/TENANCY/sql"

VERIFY_IDENTITY_DB="${VERIFY_IDENTITY_DB:-tenancy_verify_identity}"
VERIFY_ORGANIZATION_DB="${VERIFY_ORGANIZATION_DB:-tenancy_verify_organization}"
ORGANIZATION_ID="${ORGANIZATION_ID:-00000000-0000-4000-8000-000000000001}"

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

log()  { printf '\033[0;36m[verify]\033[0m %s\n' "$*"; }
pass() { printf '\033[0;32m[verify] OK:\033[0m %s\n' "$*"; }
die()  { printf '\033[0;31m[verify] FAIL:\033[0m %s\n' "$*" >&2; exit 1; }

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
  PG_CONTAINER_ID="$($DOCKER_COMPOSE ps -q "$PG_CONTAINER" 2>/dev/null | head -n1)"
  [[ -n "$PG_CONTAINER_ID" ]] || die "could not resolve container id for service '$PG_CONTAINER'"
  PG_CONNECTION="-h 127.0.0.1 -p $PG_INNER_PORT -U $PGUSER"
  psql_db() { local database="$1"; shift; docker exec -i -e PGPASSWORD="$PGPASSWORD" "$PG_CONTAINER_ID" psql $PG_CONNECTION -d "$database" "$@"; }
else
  PG_CONNECTION="-h $PGHOST -p $PGPORT -U $PGUSER"
  psql_db() { local database="$1"; shift; psql $PG_CONNECTION -d "$database" "$@"; }
fi

query_scalar() { psql_db "$1" -v ON_ERROR_STOP=1 -tAc "$2" | tr -d '[:space:]'; }
execute()      { psql_db "$1" -v ON_ERROR_STOP=1 -q -c "$2" >/dev/null; }

run_sql_file() {
  local database="$1" sql_file="$2"
  psql_db "$database" \
    -v ON_ERROR_STOP=1 -q \
    -v organization_id="'$ORGANIZATION_ID'" \
    -v organization_name="'Sellevate'" \
    -v organization_slug="'default'" \
    -f - < "$sql_file" >/dev/null
}

expect_equal() {
  local label="$1" actual="$2" expected="$3"
  [[ "$actual" == "$expected" ]] || die "$label: expected '$expected', got '$actual'"
  pass "$label = $expected"
}

drop_verification_databases() {
  # Only ever the two names this script owns. Anything else is out of scope by construction.
  execute postgres "DROP DATABASE IF EXISTS \"$VERIFY_IDENTITY_DB\" WITH (FORCE);"
  execute postgres "DROP DATABASE IF EXISTS \"$VERIFY_ORGANIZATION_DB\" WITH (FORCE);"
}

trap drop_verification_databases EXIT

log "throwaway databases: $VERIFY_IDENTITY_DB, $VERIFY_ORGANIZATION_DB"
drop_verification_databases
execute postgres "CREATE DATABASE \"$VERIFY_IDENTITY_DB\";"
execute postgres "CREATE DATABASE \"$VERIFY_ORGANIZATION_DB\";"

log "generating schema from the services' own EF migrations ..."
IDENTITY_SCHEMA_SQL="$(mktemp)"
ORGANIZATION_SCHEMA_SQL="$(mktemp)"
trap 'rm -f "$IDENTITY_SCHEMA_SQL" "$ORGANIZATION_SCHEMA_SQL"; drop_verification_databases' EXIT

(cd "$REPO_ROOT/src/backend/identity-service/Identity" \
  && dotnet ef migrations script --idempotent --no-build --output "$IDENTITY_SCHEMA_SQL" >/dev/null) \
  || die "could not generate the identity-service migration script"
(cd "$REPO_ROOT/src/backend/organization-service/Organization" \
  && dotnet ef migrations script --idempotent --no-build --output "$ORGANIZATION_SCHEMA_SQL" >/dev/null) \
  || die "could not generate the organization-service migration script"

psql_db "$VERIFY_IDENTITY_DB" -v ON_ERROR_STOP=1 -q -f - < "$IDENTITY_SCHEMA_SQL" >/dev/null
psql_db "$VERIFY_ORGANIZATION_DB" -v ON_ERROR_STOP=1 -q -f - < "$ORGANIZATION_SCHEMA_SQL" >/dev/null
pass "schema built from the real migrations"

log "seeding the fixture ..."
execute "$VERIFY_IDENTITY_DB" "
INSERT INTO \"Users\" (\"Id\", \"Email\", \"PasswordHash\", \"DisplayName\", \"Role\", \"CreatedAt\", \"IsEmailVerified\", \"AvatarType\", \"DefaultAvatarIndex\")
VALUES
  ('11111111-1111-4111-8111-111111111111', 'manager@test.local',    'hash', 'Manager',      0, now(), true, 0, 0),
  ('22222222-2222-4222-8222-222222222222', 'legacyadmin@test.local','hash', 'Legacy Admin', 1, now(), true, 0, 0),
  ('33333333-3333-4333-8333-333333333333', 'super@test.local',      'hash', 'Super Admin',  2, now(), true, 0, 0),
  ('44444444-4444-4444-8444-444444444444', 'joined@test.local',     'hash', 'Already In',   0, now(), true, 0, 0);

INSERT INTO \"Memberships\" (\"UserId\", \"OrganizationId\", \"Role\", \"Status\", \"JoinedAt\")
VALUES ('44444444-4444-4444-8444-444444444444', '99999999-9999-4999-8999-999999999999', 0, 0, now());
"

FIXTURE_USER_ROLE_DIGEST="$(query_scalar "$VERIFY_IDENTITY_DB" \
  'SELECT md5(string_agg("Id"::text || '"'"':'"'"' || "Role"::text, '"'"','"'"' ORDER BY "Id")) FROM "Users";')"
FIXTURE_MEMBERSHIP_DIGEST="$(query_scalar "$VERIFY_IDENTITY_DB" \
  'SELECT coalesce(md5(string_agg("UserId"::text || '"'"':'"'"' || "OrganizationId"::text, '"'"','"'"' ORDER BY "UserId")), '"'"'empty'"'"') FROM "Memberships";')"
FIXTURE_ORGANIZATION_COUNT="$(query_scalar "$VERIFY_ORGANIZATION_DB" 'SELECT count(*) FROM "Organizations";')"
pass "fixture snapshotted"

log "--- the driver's default mode must write nothing ---"
IDENTITY_DB="$VERIFY_IDENTITY_DB" ORGANIZATION_DB="$VERIFY_ORGANIZATION_DB" \
  "$SCRIPT_DIR/tenancy-default-organization-backfill.sh" --dry-run >/dev/null \
  || die "the backfill driver failed in dry-run mode"
expect_equal "memberships after a dry run" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" 'SELECT count(*) FROM "Memberships";')" "1"
expect_equal "organizations after a dry run" \
  "$(query_scalar "$VERIFY_ORGANIZATION_DB" 'SELECT count(*) FROM "Organizations";')" "0"

log "--- forward migration ---"
run_sql_file "$VERIFY_ORGANIZATION_DB" "$SQL_DIR/40.9_default_organization_backfill_organization_db.sql"
run_sql_file "$VERIFY_IDENTITY_DB" "$SQL_DIR/40.9_default_organization_backfill_identity_db.sql"

expect_equal "organizations in the registry" \
  "$(query_scalar "$VERIFY_ORGANIZATION_DB" 'SELECT count(*) FROM "Organizations";')" "1"
expect_equal "users without a membership" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" 'SELECT count(*) FROM "Users" u WHERE NOT EXISTS (SELECT 1 FROM "Memberships" m WHERE m."UserId" = u."Id");')" "0"
expect_equal "memberships in the default organization" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" "SELECT count(*) FROM \"Memberships\" WHERE \"OrganizationId\" = '$ORGANIZATION_ID';")" "3"
expect_equal "the pre-existing membership was left alone" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" "SELECT count(*) FROM \"Memberships\" WHERE \"UserId\" = '44444444-4444-4444-8444-444444444444' AND \"OrganizationId\" = '99999999-9999-4999-8999-999999999999';")" "1"
expect_equal "legacy admin became an OrgAdmin" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" "SELECT \"Role\" FROM \"Memberships\" WHERE \"UserId\" = '22222222-2222-4222-8222-222222222222';")" "1"
expect_equal "platform superadmin became an OrgAdmin" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" "SELECT \"Role\" FROM \"Memberships\" WHERE \"UserId\" = '33333333-3333-4333-8333-333333333333';")" "1"
expect_equal "ordinary user became a Manager" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" "SELECT \"Role\" FROM \"Memberships\" WHERE \"UserId\" = '11111111-1111-4111-8111-111111111111';")" "0"
expect_equal "users still holding the removed Admin role" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" 'SELECT count(*) FROM "Users" WHERE "Role" = 1;')" "0"
expect_equal "platform superadmin kept its platform role" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" "SELECT \"Role\" FROM \"Users\" WHERE \"Id\" = '33333333-3333-4333-8333-333333333333';")" "2"
expect_equal "login configuration seeded" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" "SELECT count(*) FROM \"OrganizationAuthConfigurations\" WHERE \"OrganizationId\" = '$ORGANIZATION_ID';")" "1"
expect_equal "registry projection seeded" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" "SELECT count(*) FROM \"OrganizationReplicas\" WHERE \"OrganizationId\" = '$ORGANIZATION_ID';")" "1"

log "--- idempotency: running the forward migration a second time ---"
run_sql_file "$VERIFY_ORGANIZATION_DB" "$SQL_DIR/40.9_default_organization_backfill_organization_db.sql"
run_sql_file "$VERIFY_IDENTITY_DB" "$SQL_DIR/40.9_default_organization_backfill_identity_db.sql"
expect_equal "memberships after the second run" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" "SELECT count(*) FROM \"Memberships\" WHERE \"OrganizationId\" = '$ORGANIZATION_ID';")" "3"
expect_equal "organizations after the second run" \
  "$(query_scalar "$VERIFY_ORGANIZATION_DB" 'SELECT count(*) FROM "Organizations";')" "1"

log "--- rollback ---"
run_sql_file "$VERIFY_IDENTITY_DB" "$SQL_DIR/40.9_default_organization_rollback_identity_db.sql"
run_sql_file "$VERIFY_ORGANIZATION_DB" "$SQL_DIR/40.9_default_organization_rollback_organization_db.sql"

expect_equal "user roles restored exactly" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" 'SELECT md5(string_agg("Id"::text || '"'"':'"'"' || "Role"::text, '"'"','"'"' ORDER BY "Id")) FROM "Users";')" \
  "$FIXTURE_USER_ROLE_DIGEST"
expect_equal "memberships restored exactly" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" 'SELECT coalesce(md5(string_agg("UserId"::text || '"'"':'"'"' || "OrganizationId"::text, '"'"','"'"' ORDER BY "UserId")), '"'"'empty'"'"') FROM "Memberships";')" \
  "$FIXTURE_MEMBERSHIP_DIGEST"
expect_equal "organization registry restored" \
  "$(query_scalar "$VERIFY_ORGANIZATION_DB" 'SELECT count(*) FROM "Organizations";')" "$FIXTURE_ORGANIZATION_COUNT"
expect_equal "no user was deleted by the rollback" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" 'SELECT count(*) FROM "Users";')" "4"
expect_equal "bookkeeping table removed from identity-db" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" "SELECT count(*) FROM information_schema.tables WHERE table_name = 'tenancy_backfill_40_9';")" "0"

log "--- the rollback must refuse when someone joined after the backfill ---"
run_sql_file "$VERIFY_ORGANIZATION_DB" "$SQL_DIR/40.9_default_organization_backfill_organization_db.sql"
run_sql_file "$VERIFY_IDENTITY_DB" "$SQL_DIR/40.9_default_organization_backfill_identity_db.sql"
execute "$VERIFY_IDENTITY_DB" "
INSERT INTO \"Users\" (\"Id\", \"Email\", \"PasswordHash\", \"DisplayName\", \"Role\", \"CreatedAt\", \"IsEmailVerified\", \"AvatarType\", \"DefaultAvatarIndex\")
VALUES ('55555555-5555-4555-8555-555555555555', 'newcomer@test.local', 'hash', 'Newcomer', 0, now(), true, 0, 0);
INSERT INTO \"Memberships\" (\"UserId\", \"OrganizationId\", \"Role\", \"Status\", \"JoinedAt\")
VALUES ('55555555-5555-4555-8555-555555555555', '$ORGANIZATION_ID', 0, 0, now() + interval '1 hour');
"

if run_sql_file "$VERIFY_IDENTITY_DB" "$SQL_DIR/40.9_default_organization_rollback_identity_db.sql" 2>/dev/null; then
  die "the rollback deleted post-migration memberships instead of refusing"
fi
expect_equal "post-migration membership survived the refused rollback" \
  "$(query_scalar "$VERIFY_IDENTITY_DB" "SELECT count(*) FROM \"Memberships\" WHERE \"UserId\" = '55555555-5555-4555-8555-555555555555';")" "1"

echo
pass "Phase 40.9 backfill and rollback verified against disposable databases."
