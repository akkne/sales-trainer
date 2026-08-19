#!/usr/bin/env bash
#
# tenancy-rollout.sh
# ------------------
# Phase 40 — one driver for the whole multi-tenancy rollout, in the order that works.
#
# WHAT IT DOES
#   Runs the thirteen-step sequence from docs/TENANCY/RUNBOOK.md part II as numbered steps:
#   preflight checks, a backup, then service starts and data backfills interleaved in the one
#   order that satisfies every constraint the phase discovered — most importantly ai-service
#   before learning-service.
#
#   Each step is idempotent and records itself in a state file, so a run interrupted at step 7
#   resumes at step 7 instead of at the beginning.
#
# WHAT IT DOES *NOT* DO
#   - It does not switch the application to the sellevate_app role and therefore does not turn
#     row-level security on. That is a separate event with its own consequences (seven background
#     jobs go silent) — RUNBOOK.md step 12, by hand.
#   - It does not create organizations or invite anybody. That is an API call, RUNBOOK.md step 13.
#   - It does not decide anything. Every step it runs is one already written and reviewed; this
#     file only sequences them and refuses to run them out of order.
#
# SAFETY
#   - Default mode is --plan: it checks everything, prints every command it would run, and writes
#     nothing. Run it that way first, always.
#   - --apply refuses to start until the preflight passes, and each data-writing step delegates to
#     the existing per-service script, which has its own dry-run and its own guards.
#   - It stops at the first failure rather than continuing into a step whose precondition no
#     longer holds.
#
# USAGE
#     ./scripts/tenancy-rollout.sh                      # plan: check everything, write nothing
#     ./scripts/tenancy-rollout.sh --apply              # run it
#     ./scripts/tenancy-rollout.sh --apply --from 7     # resume from a specific step
#     ./scripts/tenancy-rollout.sh --apply --only 8     # run exactly one step
#     ./scripts/tenancy-rollout.sh --status             # what has been done so far
#     ./scripts/tenancy-rollout.sh --apply --skip-backup
#
#   Environment (all optional):
#     ORGANIZATION_ID   default 00000000-0000-4000-8000-000000000001 — must be the same value in
#                       every step, which is why this script passes one value to all of them
#     COMPOSE_FILES     default: docker-compose.yml plus docker-compose.prod.yml when it exists.
#                       Must match the files the running stack was created with.
#     MONGO_URI         default mongodb://127.0.0.1:27017 — note this is the PRODUCTION port; the
#                       per-service scripts default to 27018, which is the local dev infra
#     BACKUP_DIR        default ./backups/<date>
#     STATE_FILE        default ./.tenancy-rollout-state
#     WAIT_TIMEOUT      seconds to wait for a service to apply its migrations, default 300
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SQL_DIR="$REPO_ROOT/docs/TENANCY/sql"

ORGANIZATION_ID="${ORGANIZATION_ID:-00000000-0000-4000-8000-000000000001}"
MONGO_URI="${MONGO_URI:-mongodb://127.0.0.1:27017}"
STATE_FILE="${STATE_FILE:-$REPO_ROOT/.tenancy-rollout-state}"
WAIT_TIMEOUT="${WAIT_TIMEOUT:-300}"

MODE="plan"
FROM_STEP=1
ONLY_STEP=""
SKIP_BACKUP="no"

# Every Postgres database and the migration that must be present once its service has started on
# the Phase 40 code. Waiting on the migration id — rather than on a health check — is what makes
# "the service is up" mean "the schema is actually there".
FINAL_MIGRATION_organization="20260816113619_RefreshTenantPoliciesForPlatformStaff"
FINAL_MIGRATION_identity="20260816113617_RefreshTenantPoliciesForPlatformStaff"
FINAL_MIGRATION_ai="20260818043308_AddOrganizationQuotas"
FINAL_MIGRATION_learning="20260818040212_AddContentAdaptationBatches"
FINAL_MIGRATION_company="20260816113610_RefreshTenantPoliciesForPlatformStaff"
FINAL_MIGRATION_gamification="20260816113612_RefreshTenantPoliciesForPlatformStaff"
FINAL_MIGRATION_social="20260816113615_RefreshTenantPoliciesForPlatformStaff"

ALL_DATABASES=(identity learning ai company gamification social organization)

log()  { printf '\033[1m[rollout]\033[0m %s\n' "$*"; }
warn() { printf '\033[33m[rollout] WARNING:\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[31m[rollout] ERROR:\033[0m %s\n' "$*" >&2; exit 1; }

while [[ $# -gt 0 ]]; do
  case "$1" in
    --plan)        MODE="plan" ;;
    --apply)       MODE="apply" ;;
    --status)      MODE="status" ;;
    --from)        FROM_STEP="${2:?--from needs a step number}"; shift ;;
    --only)        ONLY_STEP="${2:?--only needs a step number}"; shift ;;
    --skip-backup) SKIP_BACKUP="yes" ;;
    -h|--help)     sed -n '2,60p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *)             die "unknown argument: $1" ;;
  esac
  shift
done

BACKUP_DIR="${BACKUP_DIR:-$REPO_ROOT/backups/$(date +%F)}"

dotenv_get() {
  local key="$1" file="$REPO_ROOT/.env" value
  [[ -f "$file" ]] || return 0
  value="$(grep -E "^[[:space:]]*${key}=" "$file" | tail -n1)" || return 0
  value="${value#*=}"
  value="${value%\"}"; value="${value#\"}"
  value="${value%\'}"; value="${value#\'}"
  printf '%s' "$value"
}

POSTGRES_USER="${POSTGRES_USER:-$(dotenv_get POSTGRES_USER)}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-$(dotenv_get POSTGRES_PASSWORD)}"

if [[ -z "$POSTGRES_USER" || -z "$POSTGRES_PASSWORD" ]]; then
  die "POSTGRES_USER / POSTGRES_PASSWORD are neither exported nor present in $REPO_ROOT/.env"
fi

if [[ -z "${COMPOSE_FILES:-}" ]]; then
  COMPOSE_FILES="-f $REPO_ROOT/docker-compose.yml"
  [[ -f "$REPO_ROOT/docker-compose.prod.yml" ]] && COMPOSE_FILES="$COMPOSE_FILES -f $REPO_ROOT/docker-compose.prod.yml"
fi

# shellcheck disable=SC2086
compose() { docker compose $COMPOSE_FILES "$@"; }

PG_CONTAINER="postgres"
PG_CONTAINER_ID=""

resolve_postgres() {
  PG_CONTAINER_ID="$(compose ps -q "$PG_CONTAINER" 2>/dev/null | head -n1)" || true
  [[ -n "$PG_CONTAINER_ID" ]]
}

# Postgres has to be up before the preflight can ask anything about roles, but starting it is
# itself step 2 — so a first run on a cold server would otherwise deadlock on its own check.
# Apply mode starts it and waits; plan mode says what it could not verify and carries on.
ensure_postgres_running() {
  if resolve_postgres; then
    return 0
  fi

  if [[ "$MODE" != "apply" ]]; then
    warn "the '$PG_CONTAINER' container is not running, so the role-privilege check — the one that
       decides whether the migrations will do anything at all — could not run. It is enforced for
       real when you use --apply."
    return 1
  fi

  log "  '$PG_CONTAINER' is not running; starting it before the preflight"
  compose up -d "$PG_CONTAINER"

  local waited=0
  while (( waited < WAIT_TIMEOUT )); do
    if resolve_postgres && docker exec "$PG_CONTAINER_ID" pg_isready -q -U "$POSTGRES_USER" 2>/dev/null; then
      return 0
    fi
    sleep 3
    waited=$(( waited + 3 ))
  done

  die "'$PG_CONTAINER' did not become ready within ${WAIT_TIMEOUT}s"
}

psql_db() {
  local database="$1"; shift
  docker exec -i -e PGPASSWORD="$POSTGRES_PASSWORD" "$PG_CONTAINER_ID" \
    psql -v ON_ERROR_STOP=1 -h 127.0.0.1 -U "$POSTGRES_USER" -d "$database" "$@"
}

psql_value() {
  local database="$1" statement="$2"
  psql_db "$database" -tAc "$statement" 2>/dev/null | tr -d '[:space:]'
}

state_done()    { [[ -f "$STATE_FILE" ]] && grep -qx "step:$1" "$STATE_FILE"; }
state_record()  { echo "step:$1" >> "$STATE_FILE"; }

should_run() {
  local step="$1"
  if [[ -n "$ONLY_STEP" ]]; then
    [[ "$step" == "$ONLY_STEP" ]]
    return
  fi
  (( step >= FROM_STEP ))
}

# Runs a command, or prints it, depending on the mode. Every mutating action in this file goes
# through here — that is what makes --plan trustworthy rather than approximately trustworthy.
run() {
  if [[ "$MODE" == "apply" ]]; then
    log "\$ $*"
    "$@"
  else
    printf '    would run: %s\n' "$*"
  fi
}

step_header() {
  printf '\n\033[1m=== Step %s — %s\033[0m\n' "$1" "$2"
}

# ---------------------------------------------------------------------------------------------
# Preflight
# ---------------------------------------------------------------------------------------------

preflight() {
  log "Preflight"

  if ensure_postgres_running; then
    local role_info super bypass
    role_info="$(psql_value postgres "SELECT rolsuper::text || ':' || rolbypassrls::text FROM pg_roles WHERE rolname = current_user")"
    super="${role_info%%:*}"
    bypass="${role_info##*:}"

    if [[ "$super" != "true" && "$bypass" != "true" ]]; then
      die "the migration role '$POSTGRES_USER' is neither a superuser nor BYPASSRLS.
       Every migration from 40.9 on turns on FORCE ROW LEVEL SECURITY, which would then filter the
       migrations themselves: they report success and change nothing. Fix first:
         ALTER ROLE $POSTGRES_USER WITH BYPASSRLS;"
    fi
    log "  migration role '$POSTGRES_USER': superuser=$super bypassrls=$bypass — ok"
  fi

  local runtime_role
  runtime_role="$(dotenv_get APP_POSTGRES_USER)"
  if [[ -n "$runtime_role" ]]; then
    warn "APP_POSTGRES_USER is set to '$runtime_role', so the services will run under a role that RLS
       applies to. That is RUNBOOK.md step 12 and it is deliberately NOT part of this rollout —
       seven background jobs go silent the day it takes effect. Unset it for the rollout window."
  fi

  if ! command -v mongosh >/dev/null 2>&1; then
    warn "mongosh is not installed; steps 7b and 9c (Mongo backfills) will fail when they are reached."
  fi

  for sql in 40.9_default_organization_backfill_organization_db.sql 40.16_progress_version_backfill.sql; do
    [[ -f "$SQL_DIR/$sql" ]] || die "missing $SQL_DIR/$sql — is this the right repository?"
  done

  log "  compose files: $COMPOSE_FILES"
  log "  organization id: $ORGANIZATION_ID"
  log "  mongo: $MONGO_URI"
  log "  state file: $STATE_FILE"
}

wait_for_migration() {
  local database="$1" migration="$2" waited=0

  if [[ "$MODE" != "apply" ]]; then
    printf '    would wait for %s to reach migration %s\n' "$database" "$migration"
    return 0
  fi

  log "  waiting for $database to reach $migration (timeout ${WAIT_TIMEOUT}s)"
  while (( waited < WAIT_TIMEOUT )); do
    local applied
    applied="$(psql_value "$database" "SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '$migration'")" || applied=""
    if [[ "$applied" == "1" ]]; then
      log "  $database is at $migration"
      return 0
    fi
    sleep 5
    waited=$(( waited + 5 ))
  done

  die "$database did not reach $migration within ${WAIT_TIMEOUT}s.
       Look at its log: docker compose $COMPOSE_FILES logs --tail=100 ${database}"
}

run_sql_file() {
  local database="$1" file="$2"
  if [[ "$MODE" == "apply" ]]; then
    log "\$ psql $database < $file"
    psql_db "$database" -f - < "$file"
  else
    printf '    would apply %s to database %s\n' "$file" "$database"
  fi
}

rollout_script() {
  local script="$1"; shift
  [[ -x "$REPO_ROOT/scripts/$script" ]] || die "missing or non-executable scripts/$script"
  if [[ "$MODE" == "apply" ]]; then
    log "\$ scripts/$script $*"
    ORGANIZATION_ID="$ORGANIZATION_ID" MONGO_URI="$MONGO_URI" "$REPO_ROOT/scripts/$script" "$@"
  else
    printf '    would run: scripts/%s %s   (its own dry-run first if you are unsure)\n' "$script" "$*"
  fi
}

# ---------------------------------------------------------------------------------------------
# Steps
# ---------------------------------------------------------------------------------------------

step_1_backup() {
  step_header 1 "Backup — seven Postgres databases and Mongo"
  if [[ "$SKIP_BACKUP" == "yes" ]]; then
    warn "  --skip-backup: no backup taken. Every later step assumes one exists."
    return 0
  fi

  run mkdir -p "$BACKUP_DIR"
  for database in "${ALL_DATABASES[@]}"; do
    if [[ "$MODE" == "apply" ]]; then
      if [[ "$(psql_value postgres "SELECT 1 FROM pg_database WHERE datname = '$database'")" != "1" ]]; then
        log "  $database does not exist yet — nothing to back up"
        continue
      fi
      log "  dumping $database"
      docker exec -e PGPASSWORD="$POSTGRES_PASSWORD" "$PG_CONTAINER_ID" \
        pg_dump -U "$POSTGRES_USER" -Fc "$database" > "$BACKUP_DIR/$database.dump"
      [[ -s "$BACKUP_DIR/$database.dump" ]] || die "the dump of $database is empty — stopping"
    else
      printf '    would dump %s to %s/%s.dump\n' "$database" "$BACKUP_DIR" "$database"
    fi
  done

  local mongo_container
  mongo_container="$(compose ps -q mongo 2>/dev/null | head -n1)" || true
  if [[ -n "$mongo_container" ]]; then
    if [[ "$MODE" == "apply" ]]; then
      log "  dumping mongo"
      docker exec "$mongo_container" mongodump --archive --db sallevate > "$BACKUP_DIR/mongo-sallevate.archive"
      [[ -s "$BACKUP_DIR/mongo-sallevate.archive" ]] || die "the Mongo archive is empty — stopping"
    else
      printf '    would dump mongo to %s/mongo-sallevate.archive\n' "$BACKUP_DIR"
    fi
  else
    warn "  the mongo container is not running — no Mongo backup taken"
  fi
}

step_2_infrastructure() {
  step_header 2 "Infrastructure — postgres, mongo, redis, kafka"
  run compose up -d postgres mongo redis kafka
}

step_3_organization() {
  step_header 3 "organization-service — the tenant registry, before everything that references it"
  run compose up -d organization
  wait_for_migration organization "$FINAL_MIGRATION_organization"
}

step_4_identity() {
  step_header 4 "identity-service — memberships, invites, auth config, impersonation audit"
  run compose up -d identity
  wait_for_migration identity "$FINAL_MIGRATION_identity"
}

step_5_default_organization() {
  step_header 5 "Backfill the default organization into organization-db and identity-db"
  rollout_script tenancy-default-organization-backfill.sh --apply
  if [[ "$MODE" == "apply" ]]; then
    local members
    members="$(psql_value identity "SELECT count(*) FROM \"Memberships\" WHERE \"OrganizationId\" = '$ORGANIZATION_ID'")"
    log "  memberships in the default organization: ${members:-unknown}"
  fi
}

step_6_verify_default_organization() {
  step_header 6 "Verify the default organization landed in both databases"
  # tenancy-default-organization-check.sh, not tenancy-default-organization-verify.sh: the latter
  # tests the SQL files against two throwaway databases built with `dotnet ef migrations script`,
  # which needs the .NET SDK and creates and drops databases. That is a developer-machine and CI
  # test, and it says nothing about this server. This one is SELECT-only against the live data.
  rollout_script tenancy-default-organization-check.sh
}

step_7_ai() {
  step_header 7 "ai-service — BEFORE learning-service (dialog.evaluated fields, and the chat/tts seam)"
  run compose up -d ai
  wait_for_migration ai "$FINAL_MIGRATION_ai"
  log "  7b — Mongo backfill for dialog sessions (user-visible: run it now, not later)"
  rollout_script tenancy-ai-organization-rollout.sh --mongo
  log "  7c — index rebuild (CONCURRENTLY, safe with the service up)"
  rollout_script tenancy-ai-organization-rollout.sh --indexes
}

step_8_learning() {
  step_header 8 "learning-service — all sixteen migrations in one start, then the backfills"
  run compose up -d learning
  wait_for_migration learning "$FINAL_MIGRATION_learning"

  log "  8b — organization backfill. Until it runs, existing progress is invisible to its owners."
  rollout_script tenancy-learning-organization-rollout.sh --backfill

  log "  8c — bind historical progress to lesson versions (40.16)"
  run_sql_file learning "$SQL_DIR/40.16_progress_version_backfill.sql"
  run_sql_file learning "$SQL_DIR/40.16_progress_version_indexes_concurrently.sql"

  log "  8d — index rebuild. Until this runs, slugs stay globally unique and a second customer"
  log "       cannot create their own 'objections'."
  rollout_script tenancy-learning-organization-rollout.sh --indexes
}

step_9_remaining_services() {
  step_header 9 "company, gamification, social — start, then backfill each"
  run compose up -d company gamification social
  wait_for_migration company "$FINAL_MIGRATION_company"
  wait_for_migration gamification "$FINAL_MIGRATION_gamification"
  wait_for_migration social "$FINAL_MIGRATION_social"

  rollout_script tenancy-company-organization-rollout.sh --backfill
  rollout_script tenancy-company-organization-rollout.sh --indexes

  rollout_script tenancy-gamification-organization-rollout.sh --backfill
  rollout_script tenancy-gamification-organization-rollout.sh --indexes

  rollout_script tenancy-social-organization-rollout.sh --backfill
  log "  9c — Mongo backfill for chat history"
  rollout_script tenancy-social-organization-rollout.sh --mongo
  rollout_script tenancy-social-organization-rollout.sh --indexes
}

step_10_rest() {
  step_header 10 "notification, analytics, gateway, frontend — no migrations, no topics to create"
  run compose up -d notification analytics gateway frontend
}

step_11_verify() {
  step_header 11 "Verification SQL — read-only, and never executed before this rollout"
  local file name database
  for file in "$SQL_DIR"/*_verify.sql; do
    name="$(basename "$file")"
    case "$name" in
      40.33_*) database="ai" ;;
      *)       database="learning" ;;
    esac
    printf '\n--- %s (%s)\n' "$name" "$database"
    run_sql_file "$database" "$file"
  done

  if [[ "$MODE" == "apply" ]]; then
    printf '\n--- RLS state\n'
    psql_db learning -c "SELECT count(*) AS tenant_policies FROM pg_policies WHERE policyname LIKE '%_tenant_isolation';"
    psql_db learning -c "SELECT current_user, usesuper AS rls_is_inert FROM pg_user WHERE usename = current_user;"
  fi
}

STEPS=(
  "1:step_1_backup"
  "2:step_2_infrastructure"
  "3:step_3_organization"
  "4:step_4_identity"
  "5:step_5_default_organization"
  "6:step_6_verify_default_organization"
  "7:step_7_ai"
  "8:step_8_learning"
  "9:step_9_remaining_services"
  "10:step_10_rest"
  "11:step_11_verify"
)

if [[ "$MODE" == "status" ]]; then
  log "State file: $STATE_FILE"
  for entry in "${STEPS[@]}"; do
    number="${entry%%:*}"
    if state_done "$number"; then
      printf '  [x] step %s\n' "$number"
    else
      printf '  [ ] step %s\n' "$number"
    fi
  done
  exit 0
fi

if [[ "$MODE" == "plan" ]]; then
  log "PLAN MODE — nothing will be written. Re-run with --apply when the plan reads right."
fi

preflight

for entry in "${STEPS[@]}"; do
  number="${entry%%:*}"
  function_name="${entry##*:}"

  if ! should_run "$number"; then
    continue
  fi

  if state_done "$number" && [[ -z "$ONLY_STEP" ]]; then
    log "step $number already recorded as done — skipping (use --only $number to force)"
    continue
  fi

  "$function_name"

  if [[ "$MODE" == "apply" ]]; then
    state_record "$number"
  fi
done

printf '\n'
if [[ "$MODE" == "apply" ]]; then
  log "Rollout finished. What this script deliberately did NOT do:"
else
  log "Plan finished. Re-run with --apply to execute. What it will NOT do even then:"
fi
cat <<'REMAINING'
    - fill in the organization profile (RUNBOOK.md step 13; without it, substitution returns a
      neutral fallback rather than an error)
    - create real organizations and invite people (step 13, API calls)
    - switch the runtime to sellevate_app and turn RLS on (step 12, a separate event: seven
      background jobs go silent that day)
    - the two-organization acceptance run in docs/TESTING/TENANCY.md, which is the only check that
      proves isolation on live data
REMAINING
