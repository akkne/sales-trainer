-- Sellevate — Phase 40.16, step 3 of 3: index the two new version columns.
--
-- NOT executed by any automated process (build, migration, CI, or agent run), and deliberately NOT
-- part of EF migration 20260817195247_AddProgressLessonVersionBinding. A transactional index build
-- takes an ACCESS EXCLUSIVE lock on the table, learning-service runs Database.Migrate() during
-- startup, and these two tables grow with every answered exercise — a build there stalls the
-- readiness probe. Same judgement 40.10 made about every index on these same two tables; 40.15 was
-- allowed to put its indexes in the migration only because its tables were empty or a few hundred
-- rows.
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d learning -f 40.16_progress_version_indexes_concurrently.sql
--
-- Run it AFTER 40.16_progress_version_backfill.sql, and it may be run with the service up. Building
-- these before the backfill is not wrong, it just indexes a column that is still NULL everywhere and
-- then has to keep up with the backfill's writes.
--
-- WHAT THEY ARE FOR
--
--   Both serve GET /admin/lessons/{id}/accuracy, which asks "every attempt of MY organization,
--   grouped by the snapshot it was scored against". Organization first, because the row-level
--   security policy and the EF query filter both put it in front of every predicate
--   (docs/TENANCY/TENANCY.md §3).
--
-- NAMES MATTER HERE
--
--   These are the exact names EF Core generates for the HasIndex declarations in
--   UserExerciseAttemptEntityConfiguration and UserLessonProgressEntityConfiguration — including
--   the "~" that marks EF's truncation at Postgres's 63-byte identifier limit. Rename them and the
--   next "dotnet ef migrations add" will decide the indexes are missing and emit a CreateIndex that
--   locks the table at startup, which is the thing this file exists to avoid.
--
-- NOTHING IS DROPPED
--
--   Unlike 40.10-40.13 these are new indexes on new columns, not replacements. There is no old
--   index to retire and therefore no window where a query path is unserved — but the validity check
--   at the end still matters, because a failed concurrent build leaves an INVALID index that the
--   planner never uses and every write still maintains: pure overhead, silently.
--
-- CONCURRENTLY, AND WHAT THAT COSTS
--
--   * CREATE INDEX CONCURRENTLY cannot run inside a transaction block. There is no BEGIN/COMMIT in
--     this file, on purpose — every statement is its own transaction. Do not wrap it.
--   * A concurrent build that fails (deadlock, cancelled session, conflicting long transaction)
--     leaves an INVALID index. Fix with DROP INDEX CONCURRENTLY "<name>" and re-run this file.
--
-- Everything here is IF NOT EXISTS, so re-running is safe and a partially completed run can simply
-- be repeated.

\set ON_ERROR_STOP on

\echo '--- UserExerciseAttempts: attempts grouped by the version they were scored against ---'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_UserExerciseAttempts_OrganizationId_LessonVersionId_Exercis~"
    ON "UserExerciseAttempts" ("OrganizationId", "LessonVersionId", "ExerciseId");

\echo '--- UserLessonProgressRecords: "how many of my team finished this version" ---'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_UserLessonProgressRecords_OrganizationId_LessonVersionId"
    ON "UserLessonProgressRecords" ("OrganizationId", "LessonVersionId");

\echo '--- validity check ---'

DO $$
DECLARE
    invalid_index_names text;
BEGIN
    SELECT string_agg(class.relname, ', ')
    INTO invalid_index_names
    FROM pg_index index_entry
    JOIN pg_class class ON class.oid = index_entry.indexrelid
    WHERE NOT index_entry.indisvalid
      AND class.relname IN (
          'IX_UserExerciseAttempts_OrganizationId_LessonVersionId_Exercis~',
          'IX_UserLessonProgressRecords_OrganizationId_LessonVersionId'
      );

    IF invalid_index_names IS NOT NULL THEN
        RAISE EXCEPTION
            'These indexes came out INVALID: %. The planner will never use them and every write '
            'still maintains them. Run DROP INDEX CONCURRENTLY on each, then re-run this file.',
            invalid_index_names;
    END IF;
END
$$;

\echo 'learning-db: the accuracy-by-version read path is indexed.'
