-- Sellevate — Phase 40.10, step 3 of 3: rebuild learning-db's indexes with "OrganizationId" first.
--
-- NOT executed by any automated process (build, migration, CI, or agent run), and deliberately NOT
-- part of EF migration 20260815152225_AddOrganizationId or of DatabaseBootstrapper. The roadmap
-- calls this out explicitly: a transactional index build takes an ACCESS EXCLUSIVE lock on the
-- table, learning-service runs Database.Migrate() during startup, and a long build there stalls
-- the readiness probe and races the replicas. So it lives here, gets run by a human against a live
-- database, and can be run with the service up.
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d learning -f 40.10_learning_organization_indexes_concurrently.sql
--
-- Run it AFTER 40.10_learning_organization_backfill.sql. Building an index over rows that all
-- share the placeholder organization is not wrong, but it wastes the maintenance window.
--
-- CONCURRENTLY, AND WHAT THAT COSTS
--
--   * CREATE INDEX CONCURRENTLY cannot run inside a transaction block. There is no BEGIN/COMMIT in
--     this file, on purpose — every statement is its own transaction. Do not wrap it.
--   * A concurrent build that fails (deadlock, cancelled session, conflicting long transaction)
--     leaves behind an INVALID index. An invalid index is never used by the planner but is still
--     maintained on every INSERT/UPDATE — pure write overhead, invisible in query plans. The
--     validity check at the bottom of this file exists for exactly that, and it is not optional.
--     The fix for an invalid index is DROP INDEX CONCURRENTLY and re-run.
--   * The old index is dropped only AFTER its replacement is in place and valid, so no window
--     exists where neither serves queries.
--
-- Everything here is IF NOT EXISTS / IF EXISTS, so re-running is safe and a partially completed
-- run can simply be repeated.

\set ON_ERROR_STOP on

\echo '--- progress tables: new (OrganizationId, ...) indexes ---'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_UserSkillProgressRecords_OrganizationId_UserId_SkillId"
    ON "UserSkillProgressRecords" ("OrganizationId", "UserId", "SkillId");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_UserLessonProgressRecords_OrganizationId_UserId_LessonId"
    ON "UserLessonProgressRecords" ("OrganizationId", "UserId", "LessonId");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_UserExerciseAttempts_OrganizationId_UserId_ExerciseId"
    ON "UserExerciseAttempts" ("OrganizationId", "UserId", "ExerciseId");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_UserTechniqueProgress_OrganizationId_UserId"
    ON "UserTechniqueProgress" ("OrganizationId", "UserId");

-- The uniqueness this one enforces changes meaning: one row per (organization, user, technique)
-- instead of one per (user, technique). It is strictly weaker, so it cannot fail on data that
-- satisfied the old constraint.
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "IX_UserTechniqueProgress_OrganizationId_UserId_TechniqueId"
    ON "UserTechniqueProgress" ("OrganizationId", "UserId", "TechniqueId");

\echo '--- content tables: per-organization uniqueness ---'

-- Two indexes per slug, not one. In a composite unique index Postgres treats NULLs as distinct, so
-- ("OrganizationId", "IconicName") does NOT stop two global skills sharing a slug. The partial
-- index over the global rows is what preserves the old guarantee for the shared library, while the
-- composite one lets a second customer have its own "objections".
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "IX_Skills_OrganizationId_IconicName"
    ON "Skills" ("OrganizationId", "IconicName");
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "IX_Skills_IconicName_Global"
    ON "Skills" ("IconicName") WHERE "OrganizationId" IS NULL;
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Skills_OrganizationId_Stage"
    ON "Skills" ("OrganizationId", "Stage");

CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "IX_Topics_OrganizationId_IconicName"
    ON "Topics" ("OrganizationId", "IconicName");
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "IX_Topics_IconicName_Global"
    ON "Topics" ("IconicName") WHERE "OrganizationId" IS NULL;
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Topics_OrganizationId_SkillId_OrderInSkill"
    ON "Topics" ("OrganizationId", "SkillId", "OrderInSkill");
-- Dropping IX_Topics_SkillId_OrderInSkill leaves the FK to Skills without a leading-column index,
-- which makes deleting a skill scan Topics. EF asks for this one in the model for that reason.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Topics_SkillId"
    ON "Topics" ("SkillId");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Lessons_OrganizationId_TopicId_OrderInTopic"
    ON "Lessons" ("OrganizationId", "TopicId", "OrderInTopic");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Lessons_TopicId"
    ON "Lessons" ("TopicId");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Exercises_OrganizationId_LessonId_OrderInLesson"
    ON "Exercises" ("OrganizationId", "LessonId", "OrderInLesson");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Exercises_LessonId"
    ON "Exercises" ("LessonId");

CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "IX_Techniques_OrganizationId_Slug"
    ON "Techniques" ("OrganizationId", "Slug");
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "IX_Techniques_Slug_Global"
    ON "Techniques" ("Slug") WHERE "OrganizationId" IS NULL;
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Techniques_OrganizationId_PrimarySkillId"
    ON "Techniques" ("OrganizationId", "PrimarySkillId");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Techniques_OrganizationId_SortOrder"
    ON "Techniques" ("OrganizationId", "SortOrder");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_ReferenceMaterials_OrganizationId_SkillId_SortOrder"
    ON "ReferenceMaterials" ("OrganizationId", "SkillId", "SortOrder");

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

DROP INDEX CONCURRENTLY IF EXISTS "IX_UserTechniqueProgress_UserId";
DROP INDEX CONCURRENTLY IF EXISTS "IX_UserTechniqueProgress_UserId_TechniqueId";
DROP INDEX CONCURRENTLY IF EXISTS "IX_Skills_IconicName";
DROP INDEX CONCURRENTLY IF EXISTS "IX_Skills_Stage";
DROP INDEX CONCURRENTLY IF EXISTS "IX_Topics_IconicName";
DROP INDEX CONCURRENTLY IF EXISTS "IX_Topics_SkillId_OrderInSkill";
DROP INDEX CONCURRENTLY IF EXISTS "IX_Lessons_TopicId_OrderInTopic";
DROP INDEX CONCURRENTLY IF EXISTS "IX_Exercises_LessonId_OrderInLesson";
DROP INDEX CONCURRENTLY IF EXISTS "IX_Techniques_Slug";
DROP INDEX CONCURRENTLY IF EXISTS "IX_Techniques_PrimarySkillId";
DROP INDEX CONCURRENTLY IF EXISTS "IX_Techniques_SortOrder";

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
        ('IX_UserSkillProgressRecords_OrganizationId_UserId_SkillId'),
        ('IX_UserLessonProgressRecords_OrganizationId_UserId_LessonId'),
        ('IX_UserExerciseAttempts_OrganizationId_UserId_ExerciseId'),
        ('IX_UserTechniqueProgress_OrganizationId_UserId'),
        ('IX_UserTechniqueProgress_OrganizationId_UserId_TechniqueId'),
        ('IX_Skills_OrganizationId_IconicName'),
        ('IX_Skills_IconicName_Global'),
        ('IX_Skills_OrganizationId_Stage'),
        ('IX_Topics_OrganizationId_IconicName'),
        ('IX_Topics_IconicName_Global'),
        ('IX_Topics_OrganizationId_SkillId_OrderInSkill'),
        ('IX_Topics_SkillId'),
        ('IX_Lessons_OrganizationId_TopicId_OrderInTopic'),
        ('IX_Lessons_TopicId'),
        ('IX_Exercises_OrganizationId_LessonId_OrderInLesson'),
        ('IX_Exercises_LessonId'),
        ('IX_Techniques_OrganizationId_Slug'),
        ('IX_Techniques_Slug_Global'),
        ('IX_Techniques_OrganizationId_PrimarySkillId'),
        ('IX_Techniques_OrganizationId_SortOrder'),
        ('IX_ReferenceMaterials_OrganizationId_SkillId_SortOrder')
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

\echo 'learning-db: every tenant index now leads with "OrganizationId" and every index is valid.'
