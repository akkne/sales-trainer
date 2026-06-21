#!/usr/bin/env bash
#
# migrate-monolith-to-services.sh
# --------------------------------
# One-shot data migration: split the single monolith Postgres database into the
# per-service databases used by the microservices stack (database-per-service).
#
# WHAT IT DOES
#   1. Pre-flight checks (tools, connectivity, target databases exist).
#   2. Copies each owned table's DATA from the monolith DB into the owning
#      service's DB (identity / learning / ai / gamification / social), on the
#      SAME Postgres instance.
#   3. Seeds every service's `UserReplicas` projection from the monolith `Users`.
#
# WHAT IT DOES *NOT* DO
#   - It does NOT create schemas. The services create their own database + tables
#     on first start (DatabaseBootstrapper.EnsureDatabaseExistsAsync + EF
#     migrations). => Start the services ONCE before running this so the empty
#     schemas exist, then STOP them, then run this script. See PRODUCTION_MIGRATION.md.
#   - It does NOT touch Mongo. The monolith and both Mongo-using services
#     (ai: dialog_sessions, social: chat_conversations) already share the SAME
#     physical database ("sallevate"), split only by collection name — so the
#     existing data is already in the right place.
#   - It does NOT touch Redis/MinIO (notifications live in Redis; blobs in S3/MinIO).
#
# DATA, NOT SCHEMA: tables are copied with `--data-only --column-inserts
# --disable-triggers`, so column ORDER differences are tolerated and FK ordering
# is not a problem. Content that the services re-seed on startup (Achievements,
# GamificationSettings, LeagueSettings, DefaultAvatars, SuperAdmin) is NOT copied.
#
# SAFETY
#   - 100% READ-ONLY on the monolith database.
#   - Refuses to load into a target table that already has rows unless --force
#     (with --force it TRUNCATEs the target first; the SQL is printed before it runs).
#   - Idempotent with --force.
#
# USAGE
#   Configure via env (defaults match docker-compose.yml / .env):
#     PGHOST=127.0.0.1 PGPORT=5433 PGUSER=$POSTGRES_USER PGPASSWORD=$POSTGRES_PASSWORD \
#     MONOLITH_DB=sallevate ./scripts/migrate-monolith-to-services.sh [--force] [--dry-run]
#
#   Typical (reads the root .env automatically):
#     ./scripts/migrate-monolith-to-services.sh --dry-run     # show the plan, change nothing
#     ./scripts/migrate-monolith-to-services.sh               # migrate into empty service DBs
#     ./scripts/migrate-monolith-to-services.sh --force        # re-run: truncate + reload
#
set -euo pipefail

# ---------------------------------------------------------------------------
# Config (env-overridable). Load root .env if present so PG* and the DB names
# come straight from the same file the rest of the stack uses.
# ---------------------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Read a single KEY from a docker-compose-style .env WITHOUT executing it.
# (Sourcing breaks on values containing spaces / special chars, e.g.
#  SUPERADMIN_DISPLAY_NAME=Super Morfov -> bash tries to run "Morfov".)
# Returns the last matching line's value, with optional surrounding quotes stripped.
dotenv_get() {
  local key="$1" file="$2" val
  [[ -f "$file" ]] || return 0
  val="$(grep -E "^[[:space:]]*${key}=" "$file" | tail -n1)" || return 0
  val="${val#*=}"
  val="${val%\"}"; val="${val#\"}"          # strip matching double quotes
  val="${val%\'}"; val="${val#\'}"          # strip matching single quotes
  printf '%s' "$val"
}

# Only the values this script actually uses; existing env vars win over .env.
POSTGRES_USER="${POSTGRES_USER:-$(dotenv_get POSTGRES_USER "$REPO_ROOT/.env")}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-$(dotenv_get POSTGRES_PASSWORD "$REPO_ROOT/.env")}"
POSTGRES_DB="${POSTGRES_DB:-$(dotenv_get POSTGRES_DB "$REPO_ROOT/.env")}"

PGHOST="${PGHOST:-127.0.0.1}"
PGPORT="${PGPORT:-5433}"
PGUSER="${PGUSER:-${POSTGRES_USER:-st}}"
export PGPASSWORD="${PGPASSWORD:-${POSTGRES_PASSWORD:-}}"
MONOLITH_DB="${MONOLITH_DB:-${POSTGRES_DB:-sallevate}}"

FORCE=0
DRY_RUN=0
for arg in "$@"; do
  case "$arg" in
    --force)   FORCE=1 ;;
    --dry-run) DRY_RUN=1 ;;
    -h|--help) grep '^#' "$0" | sed 's/^# \{0,1\}//' | head -60; exit 0 ;;
    *) echo "Unknown argument: $arg" >&2; exit 2 ;;
  esac
done

# ---------------------------------------------------------------------------
# Ownership map: "<target_db>:<monolith_table>[:<target_table>]"
# target_table is only given when it differs from the monolith table name.
# Content that is re-seeded on service startup is intentionally NOT listed.
# ---------------------------------------------------------------------------
MAPPING=(
  # --- identity-db ---
  "identity:Users"
  "identity:UserProfiles"
  "identity:RefreshTokens"
  "identity:EmailVerificationCodes"
  "identity:DefaultAvatars"

  # --- learning-db (content + user progress) ---
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
  "learning:UserTechniqueProgressRecords:UserTechniqueProgress"   # <-- the one rename

  # --- ai-db (roleplay config) ---
  "ai:DialogBundles"
  "ai:DialogModes"

  # --- gamification-db (XP economy + leagues + achievements progress) ---
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

  # --- social-db ---
  "social:Friendships"
  "social:DiscussThreads"
  "social:DiscussReplies"
  "social:DiscussVotes"
  "social:DiscussTags"
  "social:DiscussThreadTags"
  "social:DiscussPhotos"
)

# Services that keep a UserReplicas projection (seeded from monolith Users).
REPLICA_DBS=(ai gamification social learning)

# Tables intentionally NOT copied (documented so the operator knows it is on purpose):
#   Notifications              -> moved to Redis (capped per-user list)
#   OpenQuestionGlobalContexts -> not part of any extracted service schema
#   GamificationSettings       -> re-seeded by gamification on startup
#   Achievements (definitions) -> note: progress IS copied; if you also customised
#                                 definitions in prod, set COPY_ACHIEVEMENT_DEFS=1
#   DefaultAvatars             -> re-seeded by identity (kept here anyway, harmless)

psql_q() { psql -v ON_ERROR_STOP=1 -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$1" -tAc "$2"; }

log()  { printf '\033[0;36m[migrate]\033[0m %s\n' "$*"; }
warn() { printf '\033[0;33m[migrate] WARN:\033[0m %s\n' "$*"; }
die()  { printf '\033[0;31m[migrate] ERROR:\033[0m %s\n' "$*" >&2; exit 1; }

# ---------------------------------------------------------------------------
# Pre-flight
# ---------------------------------------------------------------------------
log "Pre-flight checks"
command -v psql    >/dev/null || die "psql not found (install postgresql-client)"
command -v pg_dump >/dev/null || die "pg_dump not found (install postgresql-client)"
[[ -n "$PGPASSWORD" ]] || die "PGPASSWORD / POSTGRES_PASSWORD is empty"

log "Postgres: $PGUSER@$PGHOST:$PGPORT  monolith db: '$MONOLITH_DB'"
psql_q "$MONOLITH_DB" "SELECT 1" >/dev/null || die "cannot connect to monolith db '$MONOLITH_DB'"

# Every target db must already exist (i.e. services were started once to bootstrap them).
declare -A TARGET_SEEN=()
for entry in "${MAPPING[@]}"; do TARGET_SEEN["${entry%%:*}"]=1; done
for db in "${!TARGET_SEEN[@]}"; do
  if ! psql_q "postgres" "SELECT 1 FROM pg_database WHERE datname='$db'" | grep -q 1; then
    die "target database '$db' does not exist. Start the services once so they bootstrap their schemas, then stop them and re-run. See docs/PRODUCTION_MIGRATION.md."
  fi
done
log "All target databases present: ${!TARGET_SEEN[*]}"

if [[ "$DRY_RUN" == 1 ]]; then
  log "DRY RUN — plan only, nothing will be written:"
  for entry in "${MAPPING[@]}"; do
    IFS=':' read -r tdb src tgt <<<"$entry"; tgt="${tgt:-$src}"
    n=$(psql_q "$MONOLITH_DB" "SELECT count(*) FROM \"$src\"" 2>/dev/null || echo "?")
    printf '   %-13s %-28s -> %s.%s\n' "($n rows)" "$src" "$tdb" "$tgt"
  done
  printf '   UserReplicas    seed into: %s (from monolith Users)\n' "${REPLICA_DBS[*]}"
  exit 0
fi

# ---------------------------------------------------------------------------
# Copy owned tables
# ---------------------------------------------------------------------------
copy_table() {
  local tdb="$1" src="$2" tgt="$3"
  local src_rows tgt_rows
  src_rows=$(psql_q "$MONOLITH_DB" "SELECT count(*) FROM \"$src\"")

  # target table must exist (created by EF migration)
  if ! psql_q "$tdb" "SELECT to_regclass('public.\"$tgt\"')" | grep -q .; then
    die "target table $tdb.\"$tgt\" does not exist — service schema not migrated. Aborting."
  fi
  tgt_rows=$(psql_q "$tdb" "SELECT count(*) FROM \"$tgt\"")

  if [[ "$tgt_rows" -gt 0 ]]; then
    if [[ "$FORCE" == 1 ]]; then
      warn "TRUNCATE \"$tgt\" CASCADE;  ($tgt_rows existing rows in $tdb)"
      psql -v ON_ERROR_STOP=1 -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$tdb" \
        -c "TRUNCATE TABLE \"$tgt\" CASCADE;"
    else
      die "target $tdb.\"$tgt\" already has $tgt_rows rows. Re-run with --force to truncate+reload."
    fi
  fi

  log "copy  $src ($src_rows rows) -> $tdb.$tgt"
  # --data-only: data, not schema. --column-inserts: column-order-independent + the
  # only safe way to rename a table mid-stream. --disable-triggers: skip FK checks
  # during load (requires table owner / superuser, which $PGUSER is here).
  pg_dump -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$MONOLITH_DB" \
      --data-only --column-inserts --disable-triggers --no-owner --no-privileges \
      --table="public.\"$src\"" \
    | { if [[ "$src" != "$tgt" ]]; then
          # rewrite only the qualified INSERT target, leave data values untouched
          sed -E "s/INSERT INTO public\.\"$src\"/INSERT INTO public.\"$tgt\"/g"
        else cat; fi; } \
    | psql -v ON_ERROR_STOP=1 -q -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$tdb" >/dev/null

  local loaded; loaded=$(psql_q "$tdb" "SELECT count(*) FROM \"$tgt\"")
  [[ "$loaded" == "$src_rows" ]] || warn "row count mismatch for $tgt: source=$src_rows loaded=$loaded"
}

log "=== Copying owned tables ==="
for entry in "${MAPPING[@]}"; do
  IFS=':' read -r tdb src tgt <<<"$entry"; tgt="${tgt:-$src}"
  copy_table "$tdb" "$src" "$tgt"
done

# ---------------------------------------------------------------------------
# Seed UserReplicas (UserId, DisplayName, AvatarKey, UpdatedAt) from monolith Users
# ---------------------------------------------------------------------------
log "=== Seeding UserReplicas projections ==="
REPLICA_CSV="$(mktemp)"; trap 'rm -f "$REPLICA_CSV"' EXIT
psql -v ON_ERROR_STOP=1 -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$MONOLITH_DB" \
  -c "\copy (SELECT \"Id\", \"DisplayName\", \"AvatarKey\", now() FROM \"Users\") TO '$REPLICA_CSV' WITH (FORMAT csv)"
replica_count=$(wc -l < "$REPLICA_CSV" | tr -d ' ')

for db in "${REPLICA_DBS[@]}"; do
  existing=$(psql_q "$db" "SELECT count(*) FROM \"UserReplicas\"")
  if [[ "$existing" -gt 0 ]]; then
    if [[ "$FORCE" == 1 ]]; then
      warn "TRUNCATE \"UserReplicas\";  ($existing rows in $db)"
      psql -v ON_ERROR_STOP=1 -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$db" -c "TRUNCATE TABLE \"UserReplicas\";"
    else
      warn "$db.UserReplicas already has $existing rows — skipping (use --force to reload)"; continue
    fi
  fi
  psql -v ON_ERROR_STOP=1 -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$db" \
    -c "\copy \"UserReplicas\" (\"UserId\",\"DisplayName\",\"AvatarKey\",\"UpdatedAt\") FROM '$REPLICA_CSV' WITH (FORMAT csv)"
  log "seed  UserReplicas -> $db ($replica_count rows)"
done

log "=== DONE ==="
log "Verify a few counts, then start the services. The Kafka user.* consumers will"
log "keep the replicas in sync from here on; this seed just back-fills existing users."
