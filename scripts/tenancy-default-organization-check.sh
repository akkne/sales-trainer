#!/usr/bin/env bash
#
# tenancy-default-organization-check.sh
# -------------------------------------
# Phase 40.9 — reads the LIVE identity and organization databases and answers one question: did the
# default-organization backfill actually land, and did it land consistently in both of them?
#
# WHY THIS IS NOT tenancy-default-organization-verify.sh
#   That script tests the SQL *files*. It builds two throwaway databases from the services' own EF
#   migrations (`dotnet ef migrations script`), replays the forward migration and the rollback
#   against a hand-made fixture, asserts the rollback restores the fixture byte for byte, and drops
#   both databases again. It proves the migration is correct — and it says nothing whatsoever about
#   your server. It also needs the .NET SDK and it creates and drops databases, neither of which
#   belongs on a production host. It runs on a developer machine or in CI.
#
#   This script is the production-side counterpart. SELECT only: no SDK, no DDL, no fixture, no
#   temporary database. It looks at the rows that are really there.
#
# WHAT IT CHECKS
#   organization-db  the default organization exists and is Active, and the bookkeeping row names
#                    the organization id that was really used
#   identity-db      the same organization id — the two databases have no foreign key between them,
#                    and them drifting apart is trap #2 in RUNBOOK.md — plus: every user has a
#                    membership, nobody still holds the removed global Admin role, and the two rows
#                    that token issuance and POST /auth/login/start depend on are present
#   both             name and slug agree between the registry and identity's replica of it
#
#   Every check runs even after one fails, so a single run tells you everything that is wrong
#   rather than the first thing.
#
# EXIT CODE
#   0 = every check passed. Non-zero = at least one failed, and the failing lines say which.
#
# USAGE
#     ./scripts/tenancy-default-organization-check.sh
#
#   Same connection resolution as scripts/tenancy-default-organization-backfill.sh.
#     ORGANIZATION_ID   default 00000000-0000-4000-8000-000000000001
#     IDENTITY_DB       default identity
#     ORGANIZATION_DB   default organization
#     PG_MODE           docker | host   (default: auto)
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

ORGANIZATION_ID="${ORGANIZATION_ID:-00000000-0000-4000-8000-000000000001}"
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

log()  { printf '\033[0;36m[check]\033[0m %s\n' "$*"; }
pass() { printf '\033[0;32m[check] OK:\033[0m %s\n' "$*"; }
fail() { printf '\033[0;31m[check] FAIL:\033[0m %s\n' "$*" >&2; FAILURES=$(( FAILURES + 1 )); }
die()  { printf '\033[0;31m[check] FAIL:\033[0m %s\n' "$*" >&2; exit 1; }

FAILURES=0

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

# A failing query is an answer too — a missing table means the backfill never ran — so this returns
# an empty string rather than killing the run, and the caller turns that into a named failure.
scalar() {
  local database="$1" statement="$2" value=""
  value="$(psql_db "$database" -tAc "$statement" 2>/dev/null | tr -d '[:space:]')" || value=""
  printf '%s' "$value"
}

# Same, but keeps inner whitespace: names are compared, and "Acme Corp" must not become "AcmeCorp".
scalar_text() {
  local database="$1" statement="$2" value=""
  value="$(psql_db "$database" -tAc "$statement" 2>/dev/null)" || value=""
  printf '%s' "${value%$'\n'}"
}

table_exists() {
  [[ "$(scalar "$1" "SELECT to_regclass('public.\"$2\"') IS NOT NULL;")" == "t" ]]
}

expect_equal() {
  local label="$1" actual="$2" expected="$3"
  if [[ "$actual" == "$expected" ]]; then
    pass "$label = $expected"
  else
    fail "$label: expected '$expected', got '${actual:-<no answer>}'"
  fi
}

log "organization id : $ORGANIZATION_ID"
log "databases       : $ORGANIZATION_DB, $IDENTITY_DB"
echo

# ---------------------------------------------------------------------------------------------
# organization-db — the tenant registry
# ---------------------------------------------------------------------------------------------

log "--- $ORGANIZATION_DB (tenant registry) ---"

if ! table_exists "$ORGANIZATION_DB" Organizations; then
  die "'$ORGANIZATION_DB' has no \"Organizations\" table. organization-service has not run its EF
       migrations against this database — start it and wait for InitialOrganizationSchema."
fi

REGISTRY_NAME="$(scalar_text "$ORGANIZATION_DB" "SELECT \"Name\" FROM \"Organizations\" WHERE \"Id\" = '$ORGANIZATION_ID';")"
REGISTRY_SLUG="$(scalar_text "$ORGANIZATION_DB" "SELECT \"Slug\" FROM \"Organizations\" WHERE \"Id\" = '$ORGANIZATION_ID';")"

expect_equal "the default organization exists" \
  "$(scalar "$ORGANIZATION_DB" "SELECT count(*) FROM \"Organizations\" WHERE \"Id\" = '$ORGANIZATION_ID';")" "1"
expect_equal "its status" \
  "$(scalar "$ORGANIZATION_DB" "SELECT \"Status\" FROM \"Organizations\" WHERE \"Id\" = '$ORGANIZATION_ID';")" "Active"

if table_exists "$ORGANIZATION_DB" tenancy_backfill_40_9; then
  expect_equal "the backfill bookkeeping row names this organization" \
    "$(scalar "$ORGANIZATION_DB" "SELECT organization_id FROM tenancy_backfill_40_9 WHERE id = 1;")" \
    "$ORGANIZATION_ID"
else
  fail "'$ORGANIZATION_DB' has no tenancy_backfill_40_9 table — the 40.9 backfill never ran here."
fi

log "  registry says: name='${REGISTRY_NAME:-<none>}' slug='${REGISTRY_SLUG:-<none>}'"
echo

# ---------------------------------------------------------------------------------------------
# identity-db — memberships, login configuration, the registry projection
# ---------------------------------------------------------------------------------------------

log "--- $IDENTITY_DB (memberships and auth) ---"

if ! table_exists "$IDENTITY_DB" Memberships; then
  die "'$IDENTITY_DB' has no \"Memberships\" table. identity-service has not run its Phase 40
       migrations against this database — start it and wait for RefreshTenantPoliciesForPlatformStaff."
fi

USER_COUNT="$(scalar "$IDENTITY_DB" 'SELECT count(*) FROM "Users";')"
DEFAULT_MEMBERSHIPS="$(scalar "$IDENTITY_DB" "SELECT count(*) FROM \"Memberships\" WHERE \"OrganizationId\" = '$ORGANIZATION_ID';")"
ACTIVE_MEMBERSHIPS="$(scalar "$IDENTITY_DB" "SELECT count(*) FROM \"Memberships\" WHERE \"OrganizationId\" = '$ORGANIZATION_ID' AND \"Status\" = 0;")"
ORG_ADMINS="$(scalar "$IDENTITY_DB" "SELECT count(*) FROM \"Memberships\" WHERE \"OrganizationId\" = '$ORGANIZATION_ID' AND \"Role\" = 1;")"

log "  users: ${USER_COUNT:-?}   memberships in the default organization: ${DEFAULT_MEMBERSHIPS:-?} (${ACTIVE_MEMBERSHIPS:-?} active, ${ORG_ADMINS:-?} OrgAdmin)"

# The whole point of the backfill. A user with no membership cannot get a token with an org_id
# claim, and every tenant-scoped route then answers 403 for them.
expect_equal "users without any membership" \
  "$(scalar "$IDENTITY_DB" 'SELECT count(*) FROM "Users" u WHERE NOT EXISTS (SELECT 1 FROM "Memberships" m WHERE m."UserId" = u."Id");')" "0"

# Value 1 was the global Admin role, removed in 40.6 and deliberately left unassigned so a survivor
# fails loudly instead of quietly meaning something else.
expect_equal "users still holding the removed global Admin role" \
  "$(scalar "$IDENTITY_DB" 'SELECT count(*) FROM "Users" WHERE "Role" = 1;')" "0"

# Without this row POST /auth/login/start cannot resolve a method for the organization.
expect_equal "login configuration for the default organization" \
  "$(scalar "$IDENTITY_DB" "SELECT count(*) FROM \"OrganizationAuthConfigurations\" WHERE \"OrganizationId\" = '$ORGANIZATION_ID';")" "1"

# Without this row token issuance does not believe the organization exists.
expect_equal "registry projection for the default organization" \
  "$(scalar "$IDENTITY_DB" "SELECT count(*) FROM \"OrganizationReplicas\" WHERE \"OrganizationId\" = '$ORGANIZATION_ID';")" "1"

if table_exists "$IDENTITY_DB" Invites; then
  expect_equal "invites with no organization" \
    "$(scalar "$IDENTITY_DB" 'SELECT count(*) FROM "Invites" WHERE "OrganizationId" IS NULL;')" "0"
fi

if table_exists "$IDENTITY_DB" tenancy_backfill_40_9; then
  expect_equal "the backfill bookkeeping row names this organization" \
    "$(scalar "$IDENTITY_DB" "SELECT organization_id FROM tenancy_backfill_40_9 WHERE id = 1;")" \
    "$ORGANIZATION_ID"
else
  fail "'$IDENTITY_DB' has no tenancy_backfill_40_9 table — the 40.9 backfill never ran here."
fi
echo

# ---------------------------------------------------------------------------------------------
# Across both — the pair no foreign key protects
# ---------------------------------------------------------------------------------------------

log "--- across both databases ---"

# Trap #2 in RUNBOOK.md: the two databases hold the same uuid with nothing enforcing it. A membership
# pointing at an organization identity has no replica of is a user who cannot be issued a token.
expect_equal "memberships pointing at an organization identity does not know" \
  "$(scalar "$IDENTITY_DB" 'SELECT count(DISTINCT m."OrganizationId") FROM "Memberships" m WHERE NOT EXISTS (SELECT 1 FROM "OrganizationReplicas" r WHERE r."OrganizationId" = m."OrganizationId");')" "0"

REPLICA_NAME="$(scalar_text "$IDENTITY_DB" "SELECT \"Name\" FROM \"OrganizationReplicas\" WHERE \"OrganizationId\" = '$ORGANIZATION_ID';")"
REPLICA_SLUG="$(scalar_text "$IDENTITY_DB" "SELECT \"Slug\" FROM \"OrganizationReplicas\" WHERE \"OrganizationId\" = '$ORGANIZATION_ID';")"

# Guarded: with neither row present both sides are the empty string, and comparing them would report
# agreement about an organization that does not exist. The counts above already failed in that case.
if [[ -n "$REGISTRY_NAME" ]]; then
  expect_equal "name agrees between the registry and its replica" "$REPLICA_NAME" "$REGISTRY_NAME"
  expect_equal "slug agrees between the registry and its replica" "$REPLICA_SLUG" "$REGISTRY_SLUG"
else
  fail "the registry has no name for $ORGANIZATION_ID, so there is nothing to compare the replica against"
fi

echo
if (( FAILURES > 0 )); then
  die "$FAILURES check(s) failed. The default organization is not correctly in place — do not
       continue the rollout until this reads clean. Rollback:
         ./scripts/tenancy-default-organization-backfill.sh --rollback --i-have-a-backup"
fi

pass "the default organization is in place and consistent across both databases."
