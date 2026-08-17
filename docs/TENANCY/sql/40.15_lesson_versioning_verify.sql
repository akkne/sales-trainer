-- Sellevate — Phase 40.15: verify that immutable lesson versioning landed correctly.
--
-- READ-ONLY. Every statement in this file is a SELECT or a DO block that only raises. It creates
-- nothing, drops nothing, updates nothing, and is safe to run against a production database with
-- the service up. NOT executed by any automated process (build, migration, CI, or agent run).
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d learning -f 40.15_lesson_versioning_verify.sql
--
-- Run it AFTER learning-service has started once on the 40.15 code, so that EF migration
-- 20260817193243_AddLessonVersioning has applied.
--
--
-- WHY THERE IS NO 40.15_..._indexes_concurrently.sql
--
-- Blocks 40.10-40.13 each shipped one, because each rebuilt indexes on tables that were already
-- large and already live: a transactional build takes an ACCESS EXCLUSIVE lock, and those
-- migrations run from Database.Migrate() during service startup, where a long build stalls the
-- readiness probe and races the replicas.
--
-- 40.15 has nothing of that shape. "LessonVersions" is created empty by the migration itself, so
-- its indexes are built over zero rows. "Lessons" is a content table of a few hundred rows, where
-- the build is milliseconds — and its new index enforces slug uniqueness, which is correctness, not
-- performance. Deferring a correctness constraint to a script somebody has to remember is the worse
-- trade, and it is the same call 40.13 made for the four small gamification tables.
--
-- So the migration creates the indexes, and this file only checks the result. If a future
-- installation ever grows a "Lessons" table large enough for that to be the wrong call, the fix is
-- to move those three CREATE INDEX calls here with CONCURRENTLY — not to relax the constraint.

\set ON_ERROR_STOP on

\echo '--- 1. schema: the 40.15 columns, indexes, constraints and trigger are all present ---'

DO $$
DECLARE
    missing_names text;
BEGIN
    SELECT string_agg(expected.object_name, ', ' ORDER BY expected.object_name)
    INTO missing_names
    FROM (VALUES
        ('IX_Lessons_OrganizationId_Slug'),
        ('IX_Lessons_Slug_Global'),
        ('IX_Lessons_ParentLessonId'),
        ('IX_LessonVersions_LessonId_Draft'),
        ('IX_LessonVersions_LessonId_VersionNumber'),
        ('IX_LessonVersions_OrganizationId_LessonId_VersionNumber'),
        ('IX_LessonVersions_BaseVersionId')
    ) AS expected(object_name)
    WHERE NOT EXISTS (
        SELECT 1 FROM pg_class
        JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
        WHERE pg_namespace.nspname = current_schema()
          AND pg_class.relkind = 'i'
          AND pg_class.relname = expected.object_name
    );

    IF missing_names IS NOT NULL THEN
        RAISE EXCEPTION 'Index(es) missing: %. Did migration 20260817193243_AddLessonVersioning apply?',
            missing_names;
    END IF;
END
$$;

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
      AND (NOT pg_index.indisvalid OR NOT pg_index.indisready);

    IF invalid_index_names IS NOT NULL THEN
        RAISE EXCEPTION
            'Invalid or not-ready index(es): %. An invalid index is never used by the planner but is '
            'still maintained on every write — pure overhead, invisible in query plans. '
            'DROP INDEX CONCURRENTLY each of them and rebuild.',
            invalid_index_names;
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_trigger
        JOIN pg_class ON pg_class.oid = pg_trigger.tgrelid
        WHERE pg_class.relname = 'LessonVersions'
          AND pg_trigger.tgname = 'LessonVersions_reject_frozen_change'
          AND NOT pg_trigger.tgisinternal
    ) THEN
        RAISE EXCEPTION
            'The freeze trigger is missing. Without it a published lesson version can be edited in '
            'place, and every historical attempt scored against it silently re-interprets.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        JOIN pg_class ON pg_class.oid = pg_constraint.conrelid
        WHERE pg_class.relname = 'LessonVersions'
          AND pg_constraint.conname IN ('CK_LessonVersions_Status', 'CK_LessonVersions_PublishedAt')
        HAVING count(*) = 2
    ) THEN
        RAISE EXCEPTION 'One or both LessonVersions check constraints are missing.';
    END IF;
END
$$;

\echo '--- 2. row-level security is on, and the content policy is "mine or global" ---'

-- Expected: rowsecurity = t, forcerowsecurity = t.
SELECT relname AS table_name,
       relrowsecurity  AS row_security_enabled,
       relforcerowsecurity AS row_security_forced
FROM pg_class
JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
WHERE pg_namespace.nspname = current_schema()
  AND relname = 'LessonVersions';

-- Expected: qual mentions app.platform_mode AND "OrganizationId" IS NULL;
--           with_check mentions "OrganizationId" IS NULL but NOT app.platform_mode.
-- Platform staff read across organizations; they never gain authorship by doing so.
SELECT policyname, qual, with_check
FROM pg_policies
WHERE tablename = 'LessonVersions';

\echo '--- 3. data: every invariant the schema cannot state on its own ---'

-- Every lesson has a slug. Expected: 0.
SELECT count(*) AS lessons_without_a_slug
FROM "Lessons"
WHERE "Slug" IS NULL OR btrim("Slug") = '';

-- Slug uniqueness, including the global rows. Both expected: 0. The second query is the one that
-- matters: a composite unique index over (OrganizationId, Slug) does NOT constrain rows where
-- OrganizationId IS NULL, because Postgres treats those NULLs as distinct.
SELECT count(*) AS duplicate_slugs_within_an_organization
FROM (
    SELECT "OrganizationId", "Slug"
    FROM "Lessons"
    WHERE "OrganizationId" IS NOT NULL
    GROUP BY "OrganizationId", "Slug"
    HAVING count(*) > 1
) AS duplicates;

SELECT count(*) AS duplicate_slugs_among_global_lessons
FROM (
    SELECT "Slug"
    FROM "Lessons"
    WHERE "OrganizationId" IS NULL
    GROUP BY "Slug"
    HAVING count(*) > 1
) AS duplicates;

-- At most one draft per lesson. Expected: 0.
SELECT count(*) AS lessons_with_more_than_one_draft
FROM (
    SELECT "LessonId"
    FROM "LessonVersions"
    WHERE "Status" = 'draft'
    GROUP BY "LessonId"
    HAVING count(*) > 1
) AS duplicates;

-- A version's organization must match its lesson's. The column is denormalized so that the RLS
-- policy can compare it without a join; this is the check that the denormalization has not drifted.
-- Expected: 0.
SELECT count(*) AS versions_whose_organization_disagrees_with_their_lesson
FROM "LessonVersions"
JOIN "Lessons" ON "Lessons"."Id" = "LessonVersions"."LessonId"
WHERE "LessonVersions"."OrganizationId" IS DISTINCT FROM "Lessons"."OrganizationId";

-- Two published versions of one lesson with the same content hash means content_hash stopped doing
-- its job and publishing minted a version for an edit that was not one. Expected: 0.
SELECT count(*) AS lessons_with_duplicate_published_content
FROM (
    SELECT "LessonId", "ContentHash"
    FROM "LessonVersions"
    WHERE "Status" = 'published'
    GROUP BY "LessonId", "ContentHash"
    HAVING count(*) > 1
) AS duplicates;

\echo '--- 4. inventory ---'

SELECT
    (SELECT count(*) FROM "Lessons")                                         AS lessons,
    (SELECT count(*) FROM "Lessons" WHERE "OrganizationId" IS NULL)          AS global_lessons,
    (SELECT count(*) FROM "Lessons" WHERE "ParentLessonId" IS NOT NULL)      AS override_lessons,
    (SELECT count(*) FROM "Lessons" WHERE "IsArchived")                      AS archived_lessons,
    (SELECT count(*) FROM "LessonVersions")                                  AS lesson_versions,
    (SELECT count(*) FROM "LessonVersions" WHERE "Status" = 'draft')         AS draft_versions,
    (SELECT count(*) FROM "LessonVersions" WHERE "Status" = 'published')     AS published_versions,
    (SELECT count(*) FROM "Lessons" l WHERE NOT EXISTS (
        SELECT 1 FROM "LessonVersions" v WHERE v."LessonId" = l."Id"))       AS lessons_never_versioned;

\echo 'learning-db: 40.15 lesson versioning verified. "lessons_never_versioned" is expected to equal'
\echo 'the full lesson count immediately after the migration — versions are created when an admin'
\echo 'first opens a draft or publishes, never by the migration. Attaching historical attempts to a'
\echo 'version 1 is block 40.16 and is not done here.'
