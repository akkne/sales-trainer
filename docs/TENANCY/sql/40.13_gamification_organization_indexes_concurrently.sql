-- Sellevate — Phase 40.13, step 3 of 3: rebuild gamification-db's growing indexes with
-- "OrganizationId" first.
--
-- NOT executed by any automated process (build, migration, CI, or agent run), and deliberately NOT
-- part of EF migration 20260815213223_AddOrganizationId or of DatabaseBootstrapper. A transactional
-- index build takes an ACCESS EXCLUSIVE lock on the table, gamification-service runs
-- Database.Migrate() during startup, and a long build there stalls the readiness probe and races
-- the replicas. So it lives here, gets run by a human against a live database, and can be run with
-- the service up.
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d gamification -f 40.13_gamification_organization_indexes_concurrently.sql
--
-- Run it AFTER 40.13_gamification_organization_backfill.sql. Building an index over rows that all
-- share the placeholder organization is not wrong, but it wastes the maintenance window.
--
-- WHY THIS FILE COVERS ONLY TWO TABLES
--
--   40.10-40.12 moved every single index out of the migration. 40.13 does not, and the difference
--   is worth stating: four of gamification-db's constraints were load-bearing for CORRECTNESS in
--   the window between the deploy and this script, so the migration swaps them itself —
--   UNIQUE(WeekStartDate, Tier) on "Leagues" (otherwise the second organization to roll over gets
--   a unique violation and no league), UNIQUE(UserId) on "UserStreaks" and
--   UNIQUE(UserId, AchievementId) on "UserAchievements" (otherwise a person who belongs to two
--   customers cannot have a second row), and the primary key of "UserLearningProgress" (otherwise
--   one organization silently overwrites the other's counters). Those tables hold at most a row per
--   user, so a non-concurrent swap is a short lock.
--
--   What is left here is the two tables that actually grow without bound — "UserXpRecords" (one row
--   per XP grant, forever) and "LeagueMemberships" (one row per user per week) — and their indexes
--   are pure read paths. Nothing about correctness depends on when this file runs.
--
-- WHAT IS DELIBERATELY NOT REBUILT
--
--   UNIQUE("SourceEventId") WHERE "SourceEventId" IS NOT NULL on "UserXpRecords" stays exactly as
--   it is. It is a statement about the Kafka event stream — "one grant per event" — and the event
--   id is already unique platform-wide. Adding the organization to it would let a single event
--   grant XP once per tenant, which is the opposite of what it is for.
--
-- CONCURRENTLY, AND WHAT THAT COSTS
--
--   * CREATE INDEX CONCURRENTLY cannot run inside a transaction block. There is no BEGIN/COMMIT in
--     this file, on purpose — every statement is its own transaction. Do not wrap it.
--   * A concurrent build that fails (deadlock, cancelled session, conflicting long transaction)
--     leaves behind an INVALID index: never used by the planner, still maintained on every write.
--     The validity check below exists for exactly that, and it is not optional. The fix is
--     DROP INDEX CONCURRENTLY and re-run.
--   * The old index is dropped only AFTER its replacement is in place and valid, so no window
--     exists where neither serves queries.
--
-- Everything here is IF NOT EXISTS / IF EXISTS, so re-running is safe and a partially completed run
-- can simply be repeated.

\set ON_ERROR_STOP on

\echo '--- UserXpRecords: the per-user ledger read, organization first ---'

-- Every XP read is "this organization, this user, this time window" — the daily and weekly totals
-- on the progress screen and the weekly sync that fills a leaderboard. A strict superset of the old
-- IX_UserXpRecords_UserId.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_UserXpRecords_OrganizationId_UserId"
    ON "UserXpRecords" ("OrganizationId", "UserId");

\echo '--- LeagueMemberships: leaderboard reads, organization first ---'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_LeagueMemberships_OrganizationId_LeagueId"
    ON "LeagueMemberships" ("OrganizationId", "LeagueId");

-- Replaces UNIQUE("UserId", "LeagueId"). Note that the OLD one was already safe — a league id
-- belongs to exactly one organization, so the pair could never span two — which is precisely why
-- this swap could wait for a concurrent rebuild instead of going into the migration.
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "IX_LeagueMemberships_OrganizationId_UserId_LeagueId"
    ON "LeagueMemberships" ("OrganizationId", "UserId", "LeagueId");

\echo '--- keep the FK to Leagues indexed ---'

-- Not decoration. Dropping IX_LeagueMemberships_LeagueId below leaves the foreign key to "Leagues"
-- without a leading-column index; the new ("OrganizationId", "LeagueId") index does not serve it,
-- so deleting a league would sequential-scan the memberships table.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_LeagueMemberships_LeagueId_Fk"
    ON "LeagueMemberships" ("LeagueId");

\echo '--- validity check BEFORE dropping anything ---'

-- Nothing below this point may run if a build came out invalid: dropping the old index while its
-- replacement is unusable would take the table from "one good index" to "none".
DO $$
DECLARE
    invalid_index_names text;
BEGIN
    SELECT string_agg(pg_class.relname, ', ' ORDER BY pg_class.relname)
    INTO invalid_index_names
    FROM pg_index
    JOIN pg_class ON pg_class.oid = pg_index.indexrelid
    JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
    WHERE pg_namespace.nspname = current_schema()
      AND (NOT pg_index.indisvalid OR NOT pg_index.indisready)
      AND pg_class.relname LIKE 'IX_%';

    IF invalid_index_names IS NOT NULL THEN
        RAISE EXCEPTION
            'Invalid or not-ready index(es): %. A failed CREATE INDEX CONCURRENTLY leaves an index '
            'the planner never uses but every write still maintains. DROP INDEX CONCURRENTLY each '
            'of them and re-run this file. Nothing was dropped.',
            invalid_index_names;
    END IF;
END
$$;

\echo '--- dropping the superseded indexes ---'

DROP INDEX CONCURRENTLY IF EXISTS "IX_UserXpRecords_UserId";
DROP INDEX CONCURRENTLY IF EXISTS "IX_LeagueMemberships_UserId_LeagueId";
DROP INDEX CONCURRENTLY IF EXISTS "IX_LeagueMemberships_LeagueId";

\echo '--- final validity check ---'

DO $$
DECLARE
    invalid_index_names text;
    missing_index_names text;
BEGIN
    SELECT string_agg(pg_class.relname, ', ' ORDER BY pg_class.relname)
    INTO invalid_index_names
    FROM pg_index
    JOIN pg_class ON pg_class.oid = pg_index.indexrelid
    JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
    WHERE pg_namespace.nspname = current_schema()
      AND (NOT pg_index.indisvalid OR NOT pg_index.indisready);

    IF invalid_index_names IS NOT NULL THEN
        RAISE EXCEPTION 'Invalid or not-ready index(es) after the rebuild: %.', invalid_index_names;
    END IF;

    SELECT string_agg(expected.index_name, ', ' ORDER BY expected.index_name)
    INTO missing_index_names
    FROM (VALUES
        ('IX_UserXpRecords_OrganizationId_UserId'),
        ('IX_LeagueMemberships_OrganizationId_LeagueId'),
        ('IX_LeagueMemberships_OrganizationId_UserId_LeagueId'),
        ('IX_LeagueMemberships_LeagueId_Fk')
    ) AS expected(index_name)
    WHERE NOT EXISTS (
        SELECT 1 FROM pg_class
        JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
        WHERE pg_namespace.nspname = current_schema()
          AND pg_class.relkind = 'i'
          AND pg_class.relname = expected.index_name
    );

    IF missing_index_names IS NOT NULL THEN
        RAISE EXCEPTION 'Index(es) missing after the rebuild: %.', missing_index_names;
    END IF;
END
$$;

\echo 'gamification-db: the two growing tables now lead with "OrganizationId", the FK to Leagues stays indexed, and every index is valid.'
