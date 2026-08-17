-- Sellevate — Phase 40.21: verify that the Assignment entity landed correctly.
--
-- READ-ONLY. Every statement in this file is a SELECT or a DO block that only raises. It creates
-- nothing, drops nothing, updates nothing, and is safe to run against a production database with
-- the service up. NOT executed by any automated process (build, migration, CI, or agent run).
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d learning -f 40.21_assignments_verify.sql
--
-- Run it AFTER learning-service has started once on the 40.21 code, so that EF migration
-- 20260817223606_AddAssignments has applied.
--
--
-- WHY THERE IS NO 40.21_..._indexes_concurrently.sql
--
-- The same call 40.15, 40.17 and 40.18 made, for the same reason. Blocks 40.10-40.13 each shipped a
-- concurrent-index script because each rebuilt indexes on tables that were already large and already
-- live: a transactional build takes an ACCESS EXCLUSIVE lock, and those migrations run from
-- Database.Migrate() during startup, where a long build stalls the readiness probe.
--
-- Both tables here are created empty BY THIS VERY MIGRATION, so every index is built over zero rows.
-- One of them is a correctness constraint — one progress row per person per assignment — and
-- deferring a correctness constraint to a script somebody has to remember to run is the worse trade.
-- So the migration creates the indexes, and this file only checks the result.
--
--
-- WHY THERE IS NO BACKFILL EITHER, AND WHY THAT MEANS NO MAINTENANCE WINDOW
--
-- 40.10-40.13 each had a window between deploy and backfill in which user data was invisible, because
-- RLS filtered on a column that was not filled yet. Nothing of the sort exists here: both tables start
-- empty, nothing else filters on them, and no existing row anywhere gains a meaning it did not have.
-- An organization with no assignments behaves exactly as it did before the migration.
--
-- The tables also stay empty for longer than the usual "until somebody uses the feature":
-- AssignmentProgressRecords has no writer at all until 40.23 resolves an audience into people. That
-- is a deliberate scope boundary, not an omission — see docs/DECISIONS.md (2026-08-18) and
-- docs/DONT_FORGET.md.

\set ON_ERROR_STOP on

\echo '--- 1. schema: the 40.21 tables, indexes, constraints and trigger are all present ---'

DO $$
DECLARE
    missing_names text;
BEGIN
    SELECT string_agg(expected.object_name, ', ' ORDER BY expected.object_name)
    INTO missing_names
    FROM (VALUES
        ('Assignments'),
        ('AssignmentProgressRecords')
    ) AS expected(object_name)
    WHERE NOT EXISTS (
        SELECT 1 FROM pg_class
        JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
        WHERE pg_namespace.nspname = current_schema()
          AND pg_class.relkind = 'r'
          AND pg_class.relname = expected.object_name
    );

    IF missing_names IS NOT NULL THEN
        RAISE EXCEPTION 'Table(s) missing: %. Did migration 20260817223606_AddAssignments apply?',
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
        ('IX_Assignments_OrganizationId_Status_Deadline'),
        ('IX_Assignments_OrganizationId_CreatedAt'),
        ('IX_AssignmentProgressRecords_OrganizationId_AssignmentId_UserId'),
        ('IX_AssignmentProgressRecords_OrganizationId_UserId_Status'),
        ('IX_AssignmentProgressRecords_AssignmentId_Status')
    ) AS expected(object_name)
    WHERE NOT EXISTS (
        SELECT 1 FROM pg_class
        JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
        WHERE pg_namespace.nspname = current_schema()
          AND pg_class.relkind = 'i'
          AND pg_class.relname = expected.object_name
    );

    IF missing_names IS NOT NULL THEN
        RAISE EXCEPTION 'Index(es) missing: %. Did migration 20260817223606_AddAssignments apply?',
            missing_names;
    END IF;
END
$$;

-- IX_AssignmentProgressRecords_AssignmentId_Status is the one index here that deliberately does NOT
-- lead with OrganizationId. It covers the foreign key's ON DELETE RESTRICT check as well as 40.25's
-- per-assignment funnel; without a leading-AssignmentId index Postgres scans the whole progress table
-- on every attempt to delete an assignment — the trap 40.12 documented for company-service.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_AssignmentProgressRecords_Assignments_AssignmentId'
          AND confdeltype = 'r'
    ) THEN
        RAISE EXCEPTION
            'The progress -> assignment foreign key is missing or is not ON DELETE RESTRICT. '
            'Cascade here would let deleting an assignment erase the record that people were asked '
            'to do something.';
    END IF;
END
$$;

DO $$
DECLARE
    missing_names text;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_trigger
        JOIN pg_class ON pg_class.oid = pg_trigger.tgrelid
        WHERE pg_class.relname = 'Assignments'
          AND pg_trigger.tgname = 'Assignments_reject_frozen_change'
          AND NOT pg_trigger.tgisinternal
    ) THEN
        RAISE EXCEPTION
            'Freeze trigger Assignments_reject_frozen_change is missing. Without it an issued '
            'assignment can have its content or its completion rule rewritten in place, and every '
            'score already recorded against it silently starts describing something else.';
    END IF;

    SELECT string_agg(expected.constraint_name, ', ' ORDER BY expected.constraint_name)
    INTO missing_names
    FROM (VALUES
        ('CK_Assignments_Status'),
        ('CK_Assignments_SourceType'),
        ('CK_Assignments_ManualHasNoSourceRef'),
        ('CK_Assignments_Schedule'),
        ('CK_Assignments_ActivatedAt'),
        ('CK_Assignments_ClosedAt'),
        ('CK_Assignments_Content'),
        ('CK_Assignments_Audience'),
        ('CK_Assignments_CompletionRule'),
        ('CK_Assignments_RepeatSchedule'),
        ('CK_AssignmentProgressRecords_Status'),
        ('CK_AssignmentProgressRecords_BestScore'),
        ('CK_AssignmentProgressRecords_AttemptCount'),
        ('CK_AssignmentProgressRecords_CompletedAt'),
        ('CK_AssignmentProgressRecords_FirstOpenedAt')
    ) AS expected(constraint_name)
    WHERE NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = expected.constraint_name
    );

    IF missing_names IS NOT NULL THEN
        RAISE EXCEPTION 'Check constraint(s) missing: %.', missing_names;
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

\echo '--- 2. row-level security is on, and the policy is STRICT equality (no global assignments) ---'

-- Expected: rowsecurity = t, forcerowsecurity = t on both.
SELECT relname AS table_name,
       relrowsecurity      AS row_security_enabled,
       relforcerowsecurity AS row_security_forced
FROM pg_class
JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
WHERE pg_namespace.nspname = current_schema()
  AND relname IN ('Assignments', 'AssignmentProgressRecords')
ORDER BY relname;

-- Expected: qual mentions app.platform_mode; with_check does NOT.
-- Expected: NEITHER clause contains 'IS NULL' — that is the content flavour, and a NULL owner here
-- would mean "everybody's homework", which is not a thing. Lessons get "mine or global"; an
-- assignment never does. "Copy what the neighbouring table does" is exactly how that mistake would
-- arrive later, so it is asserted rather than assumed.
SELECT tablename, policyname, qual, with_check
FROM pg_policies
WHERE tablename IN ('Assignments', 'AssignmentProgressRecords')
ORDER BY tablename;

DO $$
DECLARE
    content_flavoured_policies text;
BEGIN
    SELECT string_agg(tablename || '.' || policyname, ', ' ORDER BY tablename)
    INTO content_flavoured_policies
    FROM pg_policies
    WHERE tablename IN ('Assignments', 'AssignmentProgressRecords')
      AND (coalesce(qual, '') LIKE '%IS NULL%' OR coalesce(with_check, '') LIKE '%IS NULL%');

    IF content_flavoured_policies IS NOT NULL THEN
        RAISE EXCEPTION
            'Policy(ies) % use the CONTENT flavour (IS NULL OR = current). An assignment with a NULL '
            'owner would be visible to every customer at once.',
            content_flavoured_policies;
    END IF;
END
$$;

\echo '--- 3. data: every invariant the schema cannot state on its own ---'

-- A progress row's organization must match its assignment's. The column is denormalized so the RLS
-- policy can compare it without a join; this is the check that the denormalization has not drifted.
-- Expected: 0.
SELECT count(*) AS progress_rows_whose_organization_disagrees_with_their_assignment
FROM "AssignmentProgressRecords"
JOIN "Assignments" ON "Assignments"."Id" = "AssignmentProgressRecords"."AssignmentId"
WHERE "AssignmentProgressRecords"."OrganizationId" IS DISTINCT FROM "Assignments"."OrganizationId";

-- Nobody may have progress on a draft: a draft has not been issued, so there is nobody to have it.
-- Expected: 0.
SELECT count(*) AS progress_rows_against_a_draft
FROM "AssignmentProgressRecords"
JOIN "Assignments" ON "Assignments"."Id" = "AssignmentProgressRecords"."AssignmentId"
WHERE "Assignments"."Status" = 'draft';

-- An active assignment with no content asks people to do nothing. The service refuses to issue one;
-- this catches a row written around it. Expected: 0.
SELECT count(*) AS issued_assignments_with_no_content
FROM "Assignments"
WHERE "Status" <> 'draft'
  AND coalesce(jsonb_array_length("Content" -> 'items'), 0) = 0;

-- Every content item must name a known kind and carry a reference. The vocabulary is
-- AssignmentContentItemKinds; a row outside it is a row 40.23 cannot render. Expected: 0.
SELECT count(*) AS content_items_with_an_unknown_shape
FROM "Assignments",
     LATERAL jsonb_array_elements(coalesce("Content" -> 'items', '[]'::jsonb)) AS item
WHERE item ->> 'kind' IS NULL
   OR item ->> 'kind' NOT IN ('lesson_version', 'dialog_scenario', 'reference_material')
   OR coalesce(item ->> 'reference', '') = '';

-- A lesson_version item must point at a lesson version that exists. There is no foreign key on
-- purpose — a content table under an "IS NULL OR = current" policy and strict tenant data under plain
-- equality are not joined by a constraint validated with the writer's privileges
-- (docs/DECISIONS.md) — so the check lives here. Expected: 0.
--
-- NOTE: run this as a role that can see all of "LessonVersions". Under a NOBYPASSRLS role with no
-- app.organization_id set, the policy hides every organization-owned version and this query will
-- report false positives.
SELECT count(*) AS content_items_pinning_a_lesson_version_that_does_not_exist
FROM "Assignments",
     LATERAL jsonb_array_elements(coalesce("Content" -> 'items', '[]'::jsonb)) AS item
WHERE item ->> 'kind' = 'lesson_version'
  AND NOT EXISTS (
      SELECT 1 FROM "LessonVersions"
      WHERE "LessonVersions"."Id"::text = item ->> 'reference'
  );

-- An assignment whose source_ref names a lesson rather than a lesson version repeats exactly the
-- defect 40.16 removed from progress: a reference that silently re-points at whatever the lesson has
-- become. Expected: 0.
SELECT count(*) AS assignments_sourced_from_a_mutable_lesson
FROM "Assignments"
WHERE "SourceRef" LIKE 'lesson:%';

-- The audience rule must name a known kind, and a named-users audience must actually name somebody.
-- Expected: 0 for both.
SELECT count(*) AS assignments_with_an_unknown_audience_kind
FROM "Assignments"
WHERE "Audience" ->> 'kind' NOT IN ('whole_team', 'users', 'group');

SELECT count(*) AS assignments_addressed_to_an_empty_user_list
FROM "Assignments"
WHERE "Audience" ->> 'kind' = 'users'
  AND coalesce(jsonb_array_length("Audience" -> 'userIds'), 0) = 0;

-- The single most important row in this file. A completion rule that is absent, empty or not an
-- object means completion has degenerated into "opened everything" — managers click through in four
-- minutes, the dashboard reads 100%, and the number is a lie the РОП eventually catches
-- (docs/TENANCY/ASSIGNMENTS.md §1.1). The check constraint makes it impossible; this is the check
-- that the constraint is doing its job. Expected: 0.
SELECT count(*) AS assignments_without_a_real_completion_rule
FROM "Assignments"
WHERE jsonb_typeof("CompletionRule") <> 'object'
   OR NOT jsonb_exists("CompletionRule", 'kind');

-- A terminal progress state has to carry the timestamps it implies, or 40.25's funnel cannot be
-- reconciled. Expected: 0.
SELECT count(*) AS progress_rows_claiming_a_state_they_cannot_support
FROM "AssignmentProgressRecords"
WHERE ("Status" = 'completed' AND "CompletedAt" IS NULL)
   OR ("Status" <> 'not_started' AND "FirstOpenedAt" IS NULL)
   OR ("Status" = 'failed_threshold' AND "AttemptCount" = 0);

\echo '--- 4. inventory ---'

SELECT
    (SELECT count(*) FROM "Assignments")                                       AS assignments,
    (SELECT count(*) FROM "Assignments" WHERE "Status" = 'draft')              AS draft_assignments,
    (SELECT count(*) FROM "Assignments" WHERE "Status" = 'active')             AS active_assignments,
    (SELECT count(*) FROM "Assignments" WHERE "Status" = 'closed')             AS closed_assignments,
    (SELECT count(*) FROM "Assignments" WHERE "RepeatSchedule" IS NOT NULL)    AS assignments_with_a_repeat_schedule,
    (SELECT count(DISTINCT "OrganizationId") FROM "Assignments")               AS organizations_with_an_assignment,
    (SELECT count(*) FROM "AssignmentProgressRecords")                         AS progress_rows,
    (SELECT count(*) FROM "AssignmentProgressRecords"
      WHERE "Status" = 'failed_threshold')                                     AS people_under_the_threshold;

\echo 'learning-db: 40.21 assignments verified. All eight counters above are expected to be ZERO'
\echo 'immediately after the migration — it creates no assignment and issues nothing, deliberately.'
\echo 'The first assignment appears when an organization administrator calls POST /admin/assignments.'
\echo 'progress_rows stays zero even after that until 40.23 resolves an audience into people; if it'
\echo 'is non-zero before 40.23 has shipped, something wrote that table outside the service.'
