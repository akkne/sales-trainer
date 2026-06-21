#!/usr/bin/env bash
#
# migrate-monolith-to-services.sh
# --------------------------------
# One-shot data migration: split the single monolith Postgres database into the
# per-service databases used by the microservices stack (database-per-service).
#
# WHAT IT DOES
#   1. Pre-flight checks (Postgres reachable, tools present, target DBs exist,
#      and a column-parity report in --dry-run).
#   2. Copies each owned table's DATA from the monolith DB into the owning
#      service's DB (identity / learning / ai / gamification / social), on the
#      SAME Postgres instance.
#   3. Seeds every service's `UserReplicas` projection from the monolith `Users`.
#
# WHERE THE POSTGRES TOOLS COME FROM (psql / pg_dump)
#   The script does NOT need psql/pg_dump installed on the host. By default it
#   auto-detects and runs them INSIDE the running `postgres` Docker container
#   (so the client version always matches the server — pg_dump refuses to dump a
#   newer server with an older client). It falls back to host-installed tools if
#   no container is found. Force a mode with:
#       PG_MODE=docker   (run inside the container; default when one is running)
#       PG_MODE=host     (use host psql/pg_dump against PGHOST:PGPORT)
#   Override the container/compose file with PG_CONTAINER (default: postgres) and
#   COMPOSE_FILE (default: <repo>/docker-compose.yml).
#
# WHAT IT DOES *NOT* DO
#   - It does NOT create schemas. The services create their own database + tables
#     on first start (DatabaseBootstrapper.EnsureDatabaseExistsAsync + EF
#     migrations). => Start the services ONCE before running this so the empty
#     schemas exist, then STOP the app services, then run this. See PRODUCTION_MIGRATION.md.
#   - It does NOT touch Mongo. The monolith and both Mongo-using services
#     (ai: dialog_sessions, social: chat_conversations) already share the SAME
#     physical database ("sallevate"), split only by collection name.
#   - It does NOT touch Redis/MinIO (notifications live in Redis; blobs in S3/MinIO).
#
# DATA, NOT SCHEMA: tables are copied with `--data-only --column-inserts
# --disable-triggers`, so column ORDER differences are tolerated and FK ordering
# is not a problem. Content the services re-seed on startup (Achievements,
# GamificationSettings, LeagueSettings, DefaultAvatars, SuperAdmin) is NOT copied.
#
# SAFETY
#   - 100% READ-ONLY on the monolith database.
#   - Refuses to load into a target table that already has rows unless --force
#     (with --force it TRUNCATEs the target first; the SQL is printed before it runs).
#   - Idempotent with --force.
#
# USAGE
#     ./scripts/migrate-monolith-to-services.sh --dry-run   # plan + column-parity, writes nothing
#     ./scripts/migrate-monolith-to-services.sh             # migrate into empty service DBs
#     ./scripts/migrate-monolith-to-services.sh --force     # re-run: truncate + reload
#   Connection comes from the root .env (POSTGRES_USER/PASSWORD/DB); override per run, e.g.
#     MONOLITH_DB=sallevate PG_MODE=docker ./scripts/migrate-monolith-to-services.sh --dry-run
#
set -euo pipefail

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Read a single KEY from a docker-compose-style .env WITHOUT executing it.
# (Sourcing breaks on values containing spaces, e.g. SUPERADMIN_DISPLAY_NAME=Super Morfov.)
dotenv_get() {
  local key="$1" file="$2" val
  [[ -f "$file" ]] || return 0
  val="$(grep -E "^[[:space:]]*${key}=" "$file" | tail -n1)" || return 0
  val="${val#*=}"
  val="${val%\"}"; val="${val#\"}"
  val="${val%\'}"; val="${val#\'}"
  printf '%s' "$val"
}

POSTGRES_USER="${POSTGRES_USER:-$(dotenv_get POSTGRES_USER "$REPO_ROOT/.env")}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-$(dotenv_get POSTGRES_PASSWORD "$REPO_ROOT/.env")}"
POSTGRES_DB="${POSTGRES_DB:-$(dotenv_get POSTGRES_DB "$REPO_ROOT/.env")}"

PGUSER="${PGUSER:-${POSTGRES_USER:-st}}"
export PGPASSWORD="${PGPASSWORD:-${POSTGRES_PASSWORD:-}}"
MONOLITH_DB="${MONOLITH_DB:-${POSTGRES_DB:-sallevate}}"

# host-mode connection (only used when PG_MODE=host)
PGHOST="${PGHOST:-127.0.0.1}"
PGPORT="${PGPORT:-5433}"
# docker-mode connection (inside the container postgres listens on 5432)
PG_CONTAINER="${PG_CONTAINER:-postgres}"
PG_INNER_PORT="${PG_INNER_PORT:-5432}"
COMPOSE_FILE="${COMPOSE_FILE:-$REPO_ROOT/docker-compose.yml}"
PG_MODE="${PG_MODE:-auto}"

FORCE=0
DRY_RUN=0
for arg in "$@"; do
  case "$arg" in
    --force)   FORCE=1 ;;
    --dry-run) DRY_RUN=1 ;;
    -h|--help) grep '^#' "$0" | sed 's/^# \{0,1\}//' | head -70; exit 0 ;;
    *) echo "Unknown argument: $arg" >&2; exit 2 ;;
  esac
done

log()  { printf '\033[0;36m[migrate]\033[0m %s\n' "$*"; }
warn() { printf '\033[0;33m[migrate] WARN:\033[0m %s\n' "$*"; }
die()  { printf '\033[0;31m[migrate] ERROR:\033[0m %s\n' "$*" >&2; exit 1; }

# ---------------------------------------------------------------------------
# Resolve docker compose CLI (v2 "docker compose" or v1 "docker-compose")
# ---------------------------------------------------------------------------
DC=""
if command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1; then
  DC="docker compose -f $COMPOSE_FILE --project-directory $REPO_ROOT"
elif command -v docker-compose >/dev/null 2>&1; then
  DC="docker-compose -f $COMPOSE_FILE --project-directory $REPO_ROOT"
fi

container_running() {
  [[ -n "$DC" ]] || return 1
  local cid; cid="$($DC ps -q "$PG_CONTAINER" 2>/dev/null | head -n1)" || return 1
  [[ -n "$cid" ]] || return 1
  [[ "$(docker inspect -f '{{.State.Running}}' "$cid" 2>/dev/null)" == "true" ]]
}

host_tools_present() { command -v psql >/dev/null 2>&1 && command -v pg_dump >/dev/null 2>&1; }

# ---------------------------------------------------------------------------
# Mode resolution
# ---------------------------------------------------------------------------
if [[ "$PG_MODE" == "auto" ]]; then
  if container_running; then PG_MODE="docker"
  elif host_tools_present; then PG_MODE="host"
  else
    die "no way to reach Postgres: the '$PG_CONTAINER' container is not running and psql/pg_dump are not on the host.
       Start the stack (so the postgres container runs) and re-run, or install postgresql-client-17 on the host."
  fi
fi

if [[ "$PG_MODE" == "docker" ]]; then
  container_running || die "PG_MODE=docker but container '$PG_CONTAINER' is not running (compose file: $COMPOSE_FILE)."
  # Resolve the container id ONCE, then use plain `docker exec` for every call.
  # (Using `docker compose exec` re-parses the compose file each time, which spams
  #  "variable is not set" warnings for any unset ${VAR} in docker-compose.yml.)
  PG_CID="$($DC ps -q "$PG_CONTAINER" 2>/dev/null | head -n1)"
  [[ -n "$PG_CID" ]] || die "could not resolve container id for service '$PG_CONTAINER'"
  PG_CONN="-h 127.0.0.1 -p $PG_INNER_PORT -U $PGUSER"
  # -i forwards stdin so the COPY/dump pipes work; -e passes the password in.
  psql_db()   { local db="$1"; shift; docker exec -i -e PGPASSWORD="$PGPASSWORD" "$PG_CID" psql    $PG_CONN -d "$db" "$@"; }
  pgdump_db() { local db="$1"; shift; docker exec -i -e PGPASSWORD="$PGPASSWORD" "$PG_CID" pg_dump $PG_CONN -d "$db" "$@"; }
  log "Postgres access: docker exec into '$PG_CONTAINER' (client version matches server)"
else
  host_tools_present || die "PG_MODE=host but psql/pg_dump are not installed. Install postgresql-client-17 or run with the postgres container up (PG_MODE=docker)."
  PG_CONN="-h $PGHOST -p $PGPORT -U $PGUSER"
  psql_db()   { local db="$1"; shift; psql    $PG_CONN -d "$db" "$@"; }
  pgdump_db() { local db="$1"; shift; pg_dump $PG_CONN -d "$db" "$@"; }
  log "Postgres access: host tools -> $PGUSER@$PGHOST:$PGPORT"
fi

q() { psql_db "$1" -v ON_ERROR_STOP=1 -tAc "$2"; }   # scalar/tuples
x() { psql_db "$1" -v ON_ERROR_STOP=1 -c  "$2"; }    # exec

# ---------------------------------------------------------------------------
# Ownership map: "<target_db>:<monolith_table>[:<target_table>]"
# ---------------------------------------------------------------------------
MAPPING=(
  "identity:Users"
  "identity:UserProfiles"
  "identity:RefreshTokens"
  "identity:EmailVerificationCodes"
  "identity:DefaultAvatars"

  "learning:Skills"
  "learning:SkillStages"
  "learning:Topics"
  "learning:Lessons"
  "learning:Exercises"
  "learning:ExerciseTypePrompts"
  "learning:ReferenceMaterials"
  "learning:DailyQuotes"
  "learning:Techniques"
  "learning:TechniqueSkills"
  "learning:TechniqueCoaches"
  "learning:UserSkillProgressRecords"
  "learning:UserLessonProgressRecords"
  "learning:UserExerciseAttempts"
  "learning:UserTechniqueProgressRecords:UserTechniqueProgress"   # the one rename

  "ai:DialogBundles"
  "ai:DialogModes"

  "gamification:UserXpRecords"
  "gamification:UserStreaks"
  "gamification:ExerciseTypeRewards"
  "gamification:StreakMilestones"
  "gamification:Achievements"
  "gamification:UserAchievements"
  "gamification:Leagues"
  "gamification:LeagueTiers"
  "gamification:LeagueMemberships"
  "gamification:LeagueSettings"

  "social:Friendships"
  "social:DiscussThreads"
  "social:DiscussReplies"
  "social:DiscussVotes"
  "social:DiscussTags"
  "social:DiscussThreadTags"
  "social:DiscussPhotos"
)
REPLICA_DBS=(ai gamification social learning)

# ---------------------------------------------------------------------------
# Pre-flight
# ---------------------------------------------------------------------------
log "Pre-flight checks"
[[ -n "$PGPASSWORD" ]] || die "POSTGRES_PASSWORD/PGPASSWORD is empty (not found in .env or env)"
q "$MONOLITH_DB" "SELECT 1" >/dev/null 2>&1 || die "cannot connect to monolith db '$MONOLITH_DB' (mode=$PG_MODE)"
log "Monolith database: '$MONOLITH_DB' reachable"

declare -A TARGET_SEEN=()
for entry in "${MAPPING[@]}"; do TARGET_SEEN["${entry%%:*}"]=1; done
for db in "${!TARGET_SEEN[@]}"; do
  if ! q "postgres" "SELECT 1 FROM pg_database WHERE datname='$db'" | grep -q 1; then
    die "target database '$db' does not exist. Start the services once so they bootstrap their schemas, then stop the app services and re-run. See docs/PRODUCTION_MIGRATION.md."
  fi
done
log "All target databases present: ${!TARGET_SEEN[*]}"

# columns of a table as a sorted, comma-free list (empty if table missing)
cols_of() { q "$1" "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='$2' ORDER BY column_name"; }

# ---------------------------------------------------------------------------
# Dry run: plan + column-parity report (surfaces schema drift before any load)
# ---------------------------------------------------------------------------
if [[ "$DRY_RUN" == 1 ]]; then
  log "DRY RUN — plan only, nothing will be written:"
  drift=0
  for entry in "${MAPPING[@]}"; do
    IFS=':' read -r tdb src tgt <<<"$entry"; tgt="${tgt:-$src}"
    if ! q "$MONOLITH_DB" "SELECT to_regclass('public.\"$src\"')" | grep -q .; then
      printf '   %-9s %-28s -> %s.%s   (source table absent — skipped)\n' "(n/a)" "$src" "$tdb" "$tgt"; continue
    fi
    n=$(q "$MONOLITH_DB" "SELECT count(*) FROM \"$src\"")
    printf '   %-9s %-28s -> %s.%s\n' "${n} rows" "$src" "$tdb" "$tgt"
    # column parity: any source column missing in target would break the INSERT
    if q "$tdb" "SELECT to_regclass('public.\"$tgt\"')" | grep -q .; then
      missing="$(comm -23 <(cols_of "$MONOLITH_DB" "$src") <(cols_of "$tdb" "$tgt"))"
      if [[ -n "$missing" ]]; then
        drift=1
        warn "  columns in monolith.$src NOT in $tdb.$tgt (would fail on load): $(echo "$missing" | tr '\n' ' ')"
      fi
    else
      drift=1; warn "  target table $tdb.$tgt is missing (service schema not migrated yet)"
    fi
  done
  printf '   UserReplicas    seed into: %s (from monolith Users)\n' "${REPLICA_DBS[*]}"
  # Replica parity: the seed supplies exactly these columns; flag any NOT NULL
  # column (without default) in a service's UserReplicas that we do NOT supply.
  provided=" UserId Email DisplayName AvatarKey UpdatedAt "
  for col in Id Email DisplayName AvatarKey; do
    q "$MONOLITH_DB" "SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='Users' AND column_name='$col'" | grep -q 1 \
      || { drift=1; warn "  monolith Users is missing column '$col' needed for the UserReplicas seed"; }
  done
  for db in "${REPLICA_DBS[@]}"; do
    while read -r req; do
      [[ -z "$req" ]] && continue
      [[ "$provided" == *" $req "* ]] || { drift=1; warn "  $db.UserReplicas requires column '$req' that the seed does not supply"; }
    done < <(q "$db" "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='UserReplicas' AND is_nullable='NO' AND column_default IS NULL")
  done
  if [[ "$drift" == 1 ]]; then
    warn "Schema drift detected above. Those tables need a column tweak before a real run; everything else is fine."
  else
    log "Column parity OK for all mapped tables — safe to run for real."
  fi
  exit 0
fi

# ---------------------------------------------------------------------------
# Copy owned tables
# ---------------------------------------------------------------------------
copy_table() {
  local tdb="$1" src="$2" tgt="$3" src_rows tgt_rows loaded
  if ! q "$MONOLITH_DB" "SELECT to_regclass('public.\"$src\"')" | grep -q .; then
    warn "source table \"$src\" absent in monolith — skipping"; return 0
  fi
  src_rows=$(q "$MONOLITH_DB" "SELECT count(*) FROM \"$src\"")
  q "$tdb" "SELECT to_regclass('public.\"$tgt\"')" | grep -q . \
    || die "target table $tdb.\"$tgt\" does not exist — service schema not migrated. Aborting."
  tgt_rows=$(q "$tdb" "SELECT count(*) FROM \"$tgt\"")

  if [[ "$tgt_rows" -gt 0 ]]; then
    if [[ "$FORCE" == 1 ]]; then
      warn "TRUNCATE \"$tgt\" CASCADE;  ($tgt_rows existing rows in $tdb)"
      x "$tdb" "TRUNCATE TABLE \"$tgt\" CASCADE;"
    else
      die "target $tdb.\"$tgt\" already has $tgt_rows rows. Re-run with --force to truncate+reload."
    fi
  fi

  log "copy  $src ($src_rows rows) -> $tdb.$tgt"
  pgdump_db "$MONOLITH_DB" \
      --data-only --column-inserts --disable-triggers --no-owner --no-privileges \
      --table="public.\"$src\"" \
    | { if [[ "$src" != "$tgt" ]]; then
          sed -E "s/INSERT INTO public\.\"$src\"/INSERT INTO public.\"$tgt\"/g"
        else cat; fi; } \
    | psql_db "$tdb" -v ON_ERROR_STOP=1 -q >/dev/null

  loaded=$(q "$tdb" "SELECT count(*) FROM \"$tgt\"")
  [[ "$loaded" == "$src_rows" ]] || warn "row count mismatch for $tgt: source=$src_rows loaded=$loaded"
}

log "=== Copying owned tables (mode: $PG_MODE) ==="
for entry in "${MAPPING[@]}"; do
  IFS=':' read -r tdb src tgt <<<"$entry"; tgt="${tgt:-$src}"
  copy_table "$tdb" "$src" "$tgt"
done

# ---------------------------------------------------------------------------
# Seed UserReplicas (UserId, Email, DisplayName, AvatarKey, UpdatedAt) from monolith
# Users. Each service's UserReplicas has Email + DisplayName as NOT NULL (max 320 /
# 200); the monolith Users row supplies both. Streamed STDOUT->STDIN (no temp files).
# ---------------------------------------------------------------------------
log "=== Seeding UserReplicas projections ==="
for db in "${REPLICA_DBS[@]}"; do
  existing=$(q "$db" "SELECT count(*) FROM \"UserReplicas\"")
  if [[ "$existing" -gt 0 ]]; then
    if [[ "$FORCE" == 1 ]]; then
      warn "TRUNCATE \"UserReplicas\";  ($existing rows in $db)"
      x "$db" "TRUNCATE TABLE \"UserReplicas\";"
    else
      warn "$db.UserReplicas already has $existing rows — skipping (use --force to reload)"; continue
    fi
  fi
  psql_db "$MONOLITH_DB" -v ON_ERROR_STOP=1 \
      -c "\copy (SELECT \"Id\", \"Email\", \"DisplayName\", \"AvatarKey\", now() FROM \"Users\") TO STDOUT WITH (FORMAT csv)" \
    | psql_db "$db" -v ON_ERROR_STOP=1 \
      -c "\copy \"UserReplicas\" (\"UserId\",\"Email\",\"DisplayName\",\"AvatarKey\",\"UpdatedAt\") FROM STDIN WITH (FORMAT csv)"
  seeded=$(q "$db" "SELECT count(*) FROM \"UserReplicas\"")
  log "seed  UserReplicas -> $db ($seeded rows)"
done

log "=== DONE ==="
log "Verify a few counts, then start the services. The Kafka user.* consumers keep"
log "the replicas in sync from here on; this seed just back-fills existing users."
