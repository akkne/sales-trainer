-- Sellevate — Phase 40.22: verify that threshold evaluation landed correctly.
--
-- READ-ONLY. Every statement in this file is a SELECT or a DO block that only raises. It creates
-- nothing, drops nothing, updates nothing, and is safe to run against a production database with
-- the service up. NOT executed by any automated process (build, migration, CI, or agent run).
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d learning -f 40.22_completion_threshold_verify.sql
--
-- Run it AFTER learning-service has started once on the 40.22 code, so that EF migration
-- 20260817225702_AddAssignmentThresholdEvaluation has applied.
--
--
-- WHAT THIS BLOCK ADDED, AND WHAT IT IS FOR
--
-- 40.21 shipped `completion_rule` as a required jsonb object with a `kind` and nothing more. 40.22
-- defines the two kinds the roadmap names — dialog_score and exercise_accuracy — and evaluates them.
-- The whole product argument is in docs/TENANCY/ASSIGNMENTS.md §1.1: if completion means "opened
-- everything", a team clicks through in four minutes, the dashboard reads 100%, and the number is a
-- lie the РОП eventually catches.
--
-- The schema addition is one table, `UserDialogScores`: one row per graded practice conversation.
-- It exists because "3 диалога с оценкой ≥70" is a question about a SET of conversations, not a
-- number that can be incremented — and because deriving AttemptCount and BestScore from rows makes
-- an at-least-once Kafka redelivery a no-op. A counter would inflate on its own once the Redis
-- dedupe window expires, and "tried 4 times and did not reach the bar" is the line a РОП acts on.
--
--
-- WHY THERE IS NO 40.22_..._indexes_concurrently.sql AND NO BACKFILL
--
-- Third block running, same reasoning as 40.15/40.17/40.18/40.21. `UserDialogScores` is created
-- empty by the migration, so both its indexes are built over zero rows and the ACCESS EXCLUSIVE
-- lock a transactional build takes costs nothing. One of the two is the uniqueness that makes
-- reprocessing an event a no-op, and deferring a correctness constraint to a script somebody has to
-- remember to run is the worse trade.
--
-- A backfill is not merely unnecessary, it is impossible: before this block `dialog.evaluated`
-- carried no grade at all (its `rawScore` field is the pre-multiplier XP reward, not a score), so
-- the history does not exist anywhere to copy from. Conversations graded before the deploy are
-- invisible to every assignment, permanently. That is recorded in docs/DONT_FORGET.md rather than
-- papered over.
--
--
-- THE ONE THING THIS SCRIPT CANNOT SHOW YOU YET
--
-- `AssignmentProgressRecords` still has no row CREATOR. 40.22 wrote the updater — the code that
-- moves a row between not_started / in_progress / completed / failed_threshold — but the row's
-- existence means "this person was asked", which is a fact about issue time and belongs to 40.23's
-- audience fan-out. Until that ships, section 4 below counts zero rows in every state, and that is
-- the correct answer rather than a fault.

\set ON_ERROR_STOP on

\echo '--- 1. schema: the 40.22 table, indexes and constraints are present ---'

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_class
        JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
        WHERE pg_namespace.nspname = current_schema()
          AND pg_class.relkind = 'r'
          AND pg_class.relname = 'UserDialogScores'
    ) THEN
        RAISE EXCEPTION
            'Table "UserDialogScores" is missing. Did migration '
            '20260817225702_AddAssignmentThresholdEvaluation apply?';
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
        ('IX_UserDialogScores_OrganizationId_UserId_SessionId'),
        ('IX_UserDialogScores_OrganizationId_UserId_DialogModeKey_Evalua~')
    ) AS expected(object_name)
    WHERE NOT EXISTS (
        SELECT 1 FROM pg_class
        JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
        WHERE pg_namespace.nspname = current_schema()
          AND pg_class.relkind = 'i'
          AND pg_class.relname = expected.object_name
    );

    IF missing_names IS NOT NULL THEN
        RAISE EXCEPTION 'Index(es) missing: %. Did migration '
            '20260817225702_AddAssignmentThresholdEvaluation apply?',
            missing_names;
    END IF;
END
$$;

-- The unique index is the idempotency guarantee, not a performance choice. Without it a redelivered
-- dialog.evaluated writes a second row for the same conversation, the attempt count goes up while
-- nobody practised, and a person who tried twice reads as though they tried four times.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_index
        JOIN pg_class ON pg_class.oid = pg_index.indexrelid
        WHERE pg_class.relname = 'IX_UserDialogScores_OrganizationId_UserId_SessionId'
          AND pg_index.indisunique
    ) THEN
        RAISE EXCEPTION
            'IX_UserDialogScores_OrganizationId_UserId_SessionId exists but is NOT unique. That '
            'index is what makes reprocessing an event a no-op; without it AttemptCount drifts '
            'upward on its own.';
    END IF;
END
$$;

DO $$
DECLARE
    missing_names text;
BEGIN
    SELECT string_agg(expected.constraint_name, ', ' ORDER BY expected.constraint_name)
    INTO missing_names
    FROM (VALUES
        ('CK_UserDialogScores_Score'),
        ('CK_UserDialogScores_SessionId'),
        ('CK_UserDialogScores_DialogModeKey')
    ) AS expected(constraint_name)
    WHERE NOT EXISTS (
        SELECT 1 FROM pg_constraint
        JOIN pg_class ON pg_class.oid = pg_constraint.conrelid
        WHERE pg_class.relname = 'UserDialogScores'
          AND pg_constraint.contype = 'c'
          AND pg_constraint.conname = expected.constraint_name
    );

    IF missing_names IS NOT NULL THEN
        RAISE EXCEPTION 'Check constraint(s) missing: %.', missing_names;
    END IF;
END
$$;

\echo '--- 2. row-level security is on, and the policy is STRICT equality ---'

-- Expected: rowsecurity = t, forcerowsecurity = t.
SELECT relname AS table_name,
       relrowsecurity      AS row_security_enabled,
       relforcerowsecurity AS row_security_forced
FROM pg_class
JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
WHERE pg_namespace.nspname = current_schema()
  AND relname = 'UserDialogScores';

-- Expected: NEITHER clause contains 'IS NULL'. A practice conversation happens inside exactly one
-- organization; the content flavour ("IS NULL OR = current") would make one customer's graded calls
-- readable by every other customer, and those calls are the rawest customer data the platform holds.
SELECT tablename, policyname, qual, with_check
FROM pg_policies
WHERE tablename = 'UserDialogScores';

DO $$
DECLARE
    content_flavoured_policies text;
BEGIN
    SELECT string_agg(policyname, ', ' ORDER BY policyname)
    INTO content_flavoured_policies
    FROM pg_policies
    WHERE tablename = 'UserDialogScores'
      AND (coalesce(qual, '') LIKE '%IS NULL%' OR coalesce(with_check, '') LIKE '%IS NULL%');

    IF content_flavoured_policies IS NOT NULL THEN
        RAISE EXCEPTION
            'Policy(ies) % on UserDialogScores use the CONTENT flavour (IS NULL OR = current).',
            content_flavoured_policies;
    END IF;
END
$$;

\echo '--- 3. data: every completion rule stored is one this service can actually evaluate ---'

-- THE most important query in this file. A rule whose kind is not in the 40.22 vocabulary can never
-- be met, and on the РОП's dashboard "nobody can complete this" is indistinguishable from "nobody
-- tried". The API refuses such a rule at create and update time, so a row here means either a rule
-- written before 40.22 shipped or one written by hand with psql.
-- Expected: 0.
SELECT count(*) AS assignments_with_an_unknown_completion_rule_kind
FROM "Assignments"
WHERE "CompletionRule" ->> 'kind' NOT IN ('dialog_score', 'exercise_accuracy');

-- Expected: 0 rows. Each one is an assignment nobody can finish; the second column says why.
SELECT "Id",
       "Status",
       "CompletionRule" ->> 'kind' AS rule_kind,
       "CompletionRule"            AS rule
FROM "Assignments"
WHERE "CompletionRule" ->> 'kind' NOT IN ('dialog_score', 'exercise_accuracy')
ORDER BY "CreatedAt" DESC
LIMIT 50;

-- A bar of zero is the compliance-theatre failure wearing a discriminator: "score at least 0" is a
-- threshold every click clears. The API refuses it; this is the check that nothing else wrote one.
-- Expected: 0.
SELECT count(*) AS assignments_whose_threshold_is_not_a_threshold
FROM "Assignments"
WHERE ("CompletionRule" ->> 'kind' = 'dialog_score'
       AND (coalesce(("CompletionRule" ->> 'minimumScore')::int, 0) < 1
            OR coalesce(("CompletionRule" ->> 'minimumScore')::int, 0) > 100
            OR coalesce(("CompletionRule" ->> 'requiredCount')::int, 0) < 1))
   OR ("CompletionRule" ->> 'kind' = 'exercise_accuracy'
       AND (coalesce(("CompletionRule" ->> 'minimumAccuracyPercent')::int, 0) < 1
            OR coalesce(("CompletionRule" ->> 'minimumAccuracyPercent')::int, 0) > 100));

-- A rule is measured over one kind of content: dialog_score over dialog_scenario items,
-- exercise_accuracy over lesson_version items. An ACTIVE assignment that carries neither is frozen
-- in that state by the 40.21 trigger — its rule can no longer be edited — so it is unfinishable
-- forever. The service refuses this combination at activation; a row here predates that check.
-- Expected: 0.
SELECT count(*) AS active_assignments_whose_rule_measures_content_they_do_not_have
FROM "Assignments"
WHERE "Status" = 'active'
  AND (
        ("CompletionRule" ->> 'kind' = 'dialog_score' AND NOT EXISTS (
            SELECT 1 FROM jsonb_array_elements("Content" -> 'items') AS item
            WHERE item ->> 'kind' = 'dialog_scenario'))
     OR ("CompletionRule" ->> 'kind' = 'exercise_accuracy' AND NOT EXISTS (
            SELECT 1 FROM jsonb_array_elements("Content" -> 'items') AS item
            WHERE item ->> 'kind' = 'lesson_version'))
      );

-- Every dialog score is on the 0-100 scale a completion rule compares against. ai-service normalizes
-- its own 0-10 grade before publishing; this is the check that nothing wrote the raw grade instead,
-- which would silently put every learner ten times under the bar.
-- Expected: 0.
SELECT count(*) AS dialog_scores_outside_the_0_100_scale
FROM "UserDialogScores"
WHERE "Score" < 0 OR "Score" > 100;

-- Two rows for the same conversation would be two attempts for one try. The unique index makes this
-- impossible; the query is here because "impossible" is a claim about an index that has to exist.
-- Expected: 0.
SELECT count(*) AS duplicated_conversations
FROM (
    SELECT "OrganizationId", "UserId", "SessionId"
    FROM "UserDialogScores"
    GROUP BY "OrganizationId", "UserId", "SessionId"
    HAVING count(*) > 1
) AS duplicates;

-- A progress row that claims a state its numbers do not support. `completed` with no score means
-- something moved the row without measuring it; `failed_threshold` with no attempts means somebody
-- was written off without trying. Both are the dashboard telling the РОП a story about a person.
-- Expected: 0.
SELECT count(*) AS progress_rows_whose_state_contradicts_their_numbers
FROM "AssignmentProgressRecords"
WHERE ("Status" = 'completed' AND ("BestScore" IS NULL OR "AttemptCount" = 0))
   OR ("Status" = 'failed_threshold' AND "AttemptCount" = 0)
   OR ("Status" = 'not_started' AND "AttemptCount" > 0);

\echo '--- 4. inventory: what the thresholds are doing right now ---'

-- Which rules organizations actually chose. Useful the first week: a fleet that is 100%
-- exercise_accuracy means nobody has wired a practice conversation into an assignment yet.
SELECT "CompletionRule" ->> 'kind' AS rule_kind,
       count(*)                    AS assignments,
       count(*) FILTER (WHERE "Status" = 'active') AS active
FROM "Assignments"
GROUP BY 1
ORDER BY 2 DESC;

-- The funnel, across every assignment. Until 40.23's fan-out ships, every one of these is zero —
-- there is nothing to update, because nothing creates a progress row. That is the expected reading
-- immediately after this migration, and it is honest rather than broken.
SELECT "Status", count(*) AS progress_rows
FROM "AssignmentProgressRecords"
GROUP BY "Status"
ORDER BY "Status";

-- The row 40.22 exists to make visible: started, tried repeatedly, still under the bar. Once 40.23
-- ships, this is the query behind the most valuable line on the РОП's screen.
SELECT "AssignmentId",
       "UserId",
       "AttemptCount",
       "BestScore",
       "FirstOpenedAt"
FROM "AssignmentProgressRecords"
WHERE "Status" = 'failed_threshold'
ORDER BY "AttemptCount" DESC
LIMIT 50;

-- How much graded conversation evidence has arrived at all. Zero here with a non-zero count of
-- completed dialog sessions in ai-service means the dialog.evaluated consumer is not running, or is
-- dead-lettering: check that envelopes carry an organization (the 40.14 finding).
SELECT count(*)                  AS graded_conversations,
       count(DISTINCT "UserId")  AS people,
       min("EvaluatedAt")        AS earliest,
       max("EvaluatedAt")        AS latest
FROM "UserDialogScores";

\echo '--- verify complete ---'
