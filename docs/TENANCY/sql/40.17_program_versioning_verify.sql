-- Sellevate — Phase 40.17: verify that programme versioning and enrollment landed correctly.
--
-- READ-ONLY. Every statement in this file is a SELECT or a DO block that only raises. It creates
-- nothing, drops nothing, updates nothing, and is safe to run against a production database with
-- the service up. NOT executed by any automated process (build, migration, CI, or agent run).
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d learning -f 40.17_program_versioning_verify.sql
--
-- Run it AFTER learning-service has started once on the 40.17 code, so that EF migration
-- 20260817203021_AddProgramVersioning has applied.
--
--
-- WHY THERE IS NO 40.17_..._indexes_concurrently.sql
--
-- The same call 40.15 made, for the same reason. Blocks 40.10-40.13 each shipped a concurrent-index
-- script because each rebuilt indexes on tables that were already large and already live: a
-- transactional build takes an ACCESS EXCLUSIVE lock, and those migrations run from
-- Database.Migrate() during startup, where a long build stalls the readiness probe.
--
-- All three tables here are created empty BY THIS VERY MIGRATION, so every index is built over zero
-- rows. Two of them are correctness constraints — one draft per organization, one pin per learner —
-- and deferring a correctness constraint to a script somebody has to remember to run is the worse
-- trade. So the migration creates the indexes, and this file only checks the result.
--
--
-- WHY THERE IS NO BACKFILL EITHER, AND WHY THAT MEANS NO MAINTENANCE WINDOW
--
-- 40.10-40.13 each had a window between deploy and backfill in which user data was invisible,
-- because RLS filtered on a column that was not filled yet. Nothing of the sort exists here: the
-- three tables start empty and nothing else filters on them. An organization with no published
-- programme version behaves exactly as it did before the migration — its people read the live
-- library, unpinned, as they always have.
--
-- The deliberate absence of a "programme version 1" minted from the live tree is a decision, not an
-- omission: see docs/DECISIONS.md (2026-08-17) and docs/DONT_FORGET.md.

\set ON_ERROR_STOP on

\echo '--- 1. schema: the 40.17 tables, indexes, constraints and triggers are all present ---'

DO $$
DECLARE
    missing_names text;
BEGIN
    SELECT string_agg(expected.object_name, ', ' ORDER BY expected.object_name)
    INTO missing_names
    FROM (VALUES
        ('ProgramVersions'),
        ('ProgramItems'),
        ('ProgramEnrollments')
    ) AS expected(object_name)
    WHERE NOT EXISTS (
        SELECT 1 FROM pg_class
        JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
        WHERE pg_namespace.nspname = current_schema()
          AND pg_class.relkind = 'r'
          AND pg_class.relname = expected.object_name
    );

    IF missing_names IS NOT NULL THEN
        RAISE EXCEPTION 'Table(s) missing: %. Did migration 20260817203021_AddProgramVersioning apply?',
            missing_names;
    END IF;
END
$$;

DO $$
DECLARE
    missing_names text;
BEGIN
    SELECT string_agg(expected.object_name, ', ' ORDER BY expected.object_name)
    INTO missing_names
    FROM (VALUES
        ('IX_ProgramVersions_OrganizationId_VersionNumber'),
        ('IX_ProgramVersions_OrganizationId_Draft'),
        ('IX_ProgramItems_ProgramVersionId_LessonId'),
        ('IX_ProgramItems_OrganizationId_ProgramVersionId_OrderIndex'),
        ('IX_ProgramItems_LessonVersionId'),
        ('IX_ProgramEnrollments_OrganizationId_UserId'),
        ('IX_ProgramEnrollments_ProgramVersionId')
    ) AS expected(object_name)
    WHERE NOT EXISTS (
        SELECT 1 FROM pg_class
        JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
        WHERE pg_namespace.nspname = current_schema()
          AND pg_class.relkind = 'i'
          AND pg_class.relname = expected.object_name
    );

    IF missing_names IS NOT NULL THEN
        RAISE EXCEPTION 'Index(es) missing: %. Did migration 20260817203021_AddProgramVersioning apply?',
            missing_names;
    END IF;
END
$$;

DO $$
DECLARE
    missing_names text;
BEGIN
    SELECT string_agg(expected.trigger_name, ', ' ORDER BY expected.trigger_name)
    INTO missing_names
    FROM (VALUES
        ('ProgramVersions', 'ProgramVersions_reject_frozen_change'),
        ('ProgramItems',    'ProgramItems_reject_frozen_change')
    ) AS expected(table_name, trigger_name)
    WHERE NOT EXISTS (
        SELECT 1 FROM pg_trigger
        JOIN pg_class ON pg_class.oid = pg_trigger.tgrelid
        WHERE pg_class.relname = expected.table_name
          AND pg_trigger.tgname = expected.trigger_name
          AND NOT pg_trigger.tgisinternal
    );

    IF missing_names IS NOT NULL THEN
        RAISE EXCEPTION
            'Freeze trigger(s) missing: %. Without them a published programme version can be '
            'rearranged in place, and a learner on lesson 8 of 21 finds a different programme — '
            'which is the single failure this block exists to prevent.',
            missing_names;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        JOIN pg_class ON pg_class.oid = pg_constraint.conrelid
        WHERE pg_constraint.conname IN (
            'CK_ProgramVersions_Status',
            'CK_ProgramVersions_PublishedAt',
            'CK_ProgramItems_OrderIndex')
        HAVING count(*) = 3
    ) THEN
        RAISE EXCEPTION 'One or more of the three 40.17 check constraints are missing.';
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

\echo '--- 2. row-level security is on, and the policy is STRICT equality (no global programmes) ---'

-- Expected: rowsecurity = t, forcerowsecurity = t on all three.
SELECT relname AS table_name,
       relrowsecurity      AS row_security_enabled,
       relforcerowsecurity AS row_security_forced
FROM pg_class
JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
WHERE pg_namespace.nspname = current_schema()
  AND relname IN ('ProgramVersions', 'ProgramItems', 'ProgramEnrollments')
ORDER BY relname;

-- Expected: qual mentions app.platform_mode; with_check does NOT.
-- Expected: NEITHER clause contains 'IS NULL' — that is the content flavour, and a NULL owner here
-- would mean "everybody's programme", which is not a thing. Lessons get "mine or global"; a
-- curriculum never does.
SELECT tablename, policyname, qual, with_check
FROM pg_policies
WHERE tablename IN ('ProgramVersions', 'ProgramItems', 'ProgramEnrollments')
ORDER BY tablename;

\echo '--- 3. data: every invariant the schema cannot state on its own ---'

-- At most one draft per organization. The partial unique index enforces it; this catches the case
-- where the index was dropped by hand. Expected: 0.
SELECT count(*) AS organizations_with_more_than_one_draft
FROM (
    SELECT "OrganizationId"
    FROM "ProgramVersions"
    WHERE "Status" = 'draft'
    GROUP BY "OrganizationId"
    HAVING count(*) > 1
) AS duplicates;

-- An item's organization must match its programme version's. The column is denormalized so the RLS
-- policy can compare it without a join; this is the check that the denormalization has not drifted.
-- Expected: 0.
SELECT count(*) AS items_whose_organization_disagrees_with_their_version
FROM "ProgramItems"
JOIN "ProgramVersions" ON "ProgramVersions"."Id" = "ProgramItems"."ProgramVersionId"
WHERE "ProgramItems"."OrganizationId" IS DISTINCT FROM "ProgramVersions"."OrganizationId";

-- Same for enrollments. Expected: 0. A learner pinned across an organization boundary would be
-- reading somebody else's curriculum.
SELECT count(*) AS enrollments_whose_organization_disagrees_with_their_version
FROM "ProgramEnrollments"
JOIN "ProgramVersions" ON "ProgramVersions"."Id" = "ProgramEnrollments"."ProgramVersionId"
WHERE "ProgramEnrollments"."OrganizationId" IS DISTINCT FROM "ProgramVersions"."OrganizationId";

-- Nobody may be pinned to a draft. Enrollment goes to published versions only; a pin to a mutable
-- row is a pin to nothing. Expected: 0.
SELECT count(*) AS enrollments_pinned_to_a_non_published_version
FROM "ProgramEnrollments"
JOIN "ProgramVersions" ON "ProgramVersions"."Id" = "ProgramEnrollments"."ProgramVersionId"
WHERE "ProgramVersions"."Status" <> 'published';

-- A pinned lesson version must exist. There is no foreign key on purpose — a content table under an
-- "IS NULL OR = current" policy and strict tenant data under plain equality are not joined by a
-- constraint validated with the writer's privileges (docs/DECISIONS.md) — so the check lives here.
-- Expected: 0.
--
-- NOTE: run this as a role that can see all of "LessonVersions". Under a NOBYPASSRLS role with no
-- app.organization_id set, the policy hides every organization-owned version and this query will
-- report false positives.
SELECT count(*) AS items_pinning_a_lesson_version_that_does_not_exist
FROM "ProgramItems"
WHERE NOT EXISTS (
    SELECT 1 FROM "LessonVersions"
    WHERE "LessonVersions"."Id" = "ProgramItems"."LessonVersionId"
);

-- The denormalized LessonId must agree with the pinned snapshot's own lesson. Same visibility
-- caveat as above. Expected: 0.
SELECT count(*) AS items_whose_lesson_disagrees_with_their_pinned_version
FROM "ProgramItems"
JOIN "LessonVersions" ON "LessonVersions"."Id" = "ProgramItems"."LessonVersionId"
WHERE "ProgramItems"."LessonId" IS DISTINCT FROM "LessonVersions"."LessonId";

-- A programme pins a lesson once. Twice would mean the same material with two different answer
-- keys inside one curriculum. Expected: 0.
SELECT count(*) AS versions_pinning_one_lesson_twice
FROM (
    SELECT "ProgramVersionId", "LessonId"
    FROM "ProgramItems"
    GROUP BY "ProgramVersionId", "LessonId"
    HAVING count(*) > 1
) AS duplicates;

\echo '--- 4. inventory ---'

SELECT
    (SELECT count(*) FROM "ProgramVersions")                                AS program_versions,
    (SELECT count(*) FROM "ProgramVersions" WHERE "Status" = 'draft')       AS draft_versions,
    (SELECT count(*) FROM "ProgramVersions" WHERE "Status" = 'published')   AS published_versions,
    (SELECT count(DISTINCT "OrganizationId") FROM "ProgramVersions")        AS organizations_with_a_programme,
    (SELECT count(*) FROM "ProgramItems")                                   AS program_items,
    (SELECT count(*) FROM "ProgramEnrollments")                             AS enrollments,
    (SELECT count(*) FROM "ProgramEnrollments" WHERE "SwitchedAt" IS NOT NULL) AS learners_who_have_switched;

\echo 'learning-db: 40.17 programme versioning verified. All seven counters above are expected to be'
\echo 'ZERO immediately after the migration — the migration creates no programme and enrolls nobody,'
\echo 'deliberately (docs/DECISIONS.md, 2026-08-17). The first programme version appears when an'
\echo 'organization administrator calls POST /admin/program/versions/draft and then /publish.'
