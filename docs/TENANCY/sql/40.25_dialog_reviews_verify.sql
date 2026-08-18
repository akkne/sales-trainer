-- Sellevate — Phase 40.25: verify the ROP dashboard's new table, and extract the labelled dataset.
--
-- READ-ONLY. Every statement in this file is a SELECT or a DO block that only raises. It creates
-- nothing, drops nothing, updates nothing, and is safe to run against a production database with
-- the service up. NOT executed by any automated process (build, migration, CI, or agent run).
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d learning -f 40.25_dialog_reviews_verify.sql
--
-- Run it AFTER learning-service has started once on the 40.25 code, so that EF migration
-- 20260818005249_AddDialogReviewNotes has applied.
--
--
-- WHAT THIS BLOCK ADDED, AND WHAT IT IS FOR
--
-- 40.25 is the ROP's screen. Most of it is reads over rows that already existed — the funnel is a
-- count of `AssignmentProgressRecords`, the heat map is an aggregation over `UserExerciseAttempts`
-- — and one new table: `DialogReviewNotes`.
--
-- That table carries both directions of docs/TENANCY/ASSIGNMENTS.md §4.1 under a `Kind` column:
--
--   coaching_note  — the ROP selected a fragment of somebody's practice call, quoted it and
--                    commented. Author is the ROP, subject is the manager, and the manager closes
--                    it by reading it (`acknowledged`).
--   score_dispute  — the manager says the AI graded them wrongly. Author and subject are the same
--                    person, and the ROP closes it with a verdict (`upheld` / `rejected`).
--
-- One table rather than two because the six fields that matter — session, quoted fragment, comment,
-- author, subject, resolution — are shared, and two tables would give the tenant column, the freeze
-- rules and the frozen-quote copy two places to be got right.
--
-- The second purpose of the table is the one the roadmap names explicitly: a resolved dispute is a
-- human-labelled disagreement with a specific machine grade, which is exactly the training signal
-- the evaluation prompts need. Section 6 below is that extraction.
--
--
-- WHY THERE IS NO 40.25_..._indexes_concurrently.sql AND NO BACKFILL
--
-- Sixth block running, same reasoning as 40.21–40.24. `DialogReviewNotes` is created empty by the
-- migration, so all four indexes (three ordinary, one partial unique) are built over zero rows and
-- the ACCESS EXCLUSIVE lock costs nothing. Nothing exists to backfill either: no coaching note or
-- dispute has ever been recorded anywhere, in any form, before this migration.
--
-- The partial unique index is created by the migration rather than deferred, for the reason 40.24
-- gave: it is a correctness constraint, not a performance one, and deferring a correctness
-- constraint to a script somebody has to remember to run is how a "unique" column ends up not being
-- unique.
--
-- The other two blocks of 40.25 add no schema at all. `GET /admin/assignments/{id}/dashboard` and
-- `GET /admin/team/skill-map` read existing tables; the indexes they lean on
-- (`IX_AssignmentProgressRecords_AssignmentId_Status` from 40.21 and
-- `IX_UserExerciseAttempts_OrganizationId_UserId_ExerciseId` from 40.10) were already there. That is
-- a deliberate statement rather than an omission — see docs/DECISIONS.md (2026-08-18).


\echo ''
\echo '=== 1. The table exists, with row-level security forced ==================================='

SELECT
    c.relname                                   AS table_name,
    c.relrowsecurity                            AS rls_enabled,
    c.relforcerowsecurity                       AS rls_forced
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND c.relname = 'DialogReviewNotes';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname = 'DialogReviewNotes'
          AND c.relrowsecurity
          AND c.relforcerowsecurity
    ) THEN
        RAISE EXCEPTION
            'DialogReviewNotes is missing FORCE ROW LEVEL SECURITY. A coaching note is one '
            'organization''s manager being coached in front of every other organization.';
    END IF;
END $$;


\echo ''
\echo '=== 2. The policy is plain equality, never the content "IS NULL OR" branch ==============='

SELECT
    pol.polname                                 AS policy_name,
    pg_get_expr(pol.polqual, pol.polrelid)      AS using_clause,
    pg_get_expr(pol.polwithcheck, pol.polrelid) AS with_check_clause
FROM pg_policy pol
JOIN pg_class c ON c.oid = pol.polrelid
WHERE c.relname = 'DialogReviewNotes';

DO $$
DECLARE
    using_clause text;
BEGIN
    SELECT pg_get_expr(pol.polqual, pol.polrelid)
      INTO using_clause
      FROM pg_policy pol
      JOIN pg_class c ON c.oid = pol.polrelid
     WHERE c.relname = 'DialogReviewNotes'
     LIMIT 1;

    IF using_clause IS NULL THEN
        RAISE EXCEPTION 'DialogReviewNotes has no row-level-security policy at all.';
    END IF;

    IF using_clause ILIKE '%IS NULL%' THEN
        RAISE EXCEPTION
            'DialogReviewNotes carries a content-style policy with a NULL branch. There is no '
            'global coaching note and there is no global dispute; a NULL owner here would publish '
            'one customer''s argument about a grade to every other customer.';
    END IF;
END $$;


\echo ''
\echo '=== 3. Check constraints: the per-kind status vocabulary is the load-bearing one =========='

SELECT
    con.conname                                 AS constraint_name,
    pg_get_constraintdef(con.oid)               AS definition
FROM pg_constraint con
JOIN pg_class c ON c.oid = con.conrelid
WHERE c.relname = 'DialogReviewNotes'
  AND con.contype = 'c'
ORDER BY con.conname;

DO $$
DECLARE
    expected text[] := ARRAY[
        'CK_DialogReviewNotes_Author',
        'CK_DialogReviewNotes_CoachingNoteQuote',
        'CK_DialogReviewNotes_Comment',
        'CK_DialogReviewNotes_Kind',
        'CK_DialogReviewNotes_Quote',
        'CK_DialogReviewNotes_Scores',
        'CK_DialogReviewNotes_SessionId',
        'CK_DialogReviewNotes_Status'
    ];
    missing text;
BEGIN
    FOREACH missing IN ARRAY expected LOOP
        IF NOT EXISTS (
            SELECT 1
            FROM pg_constraint con
            JOIN pg_class c ON c.oid = con.conrelid
            WHERE c.relname = 'DialogReviewNotes' AND con.conname = missing
        ) THEN
            RAISE EXCEPTION 'Check constraint % is missing from DialogReviewNotes.', missing;
        END IF;
    END LOOP;
END $$;


\echo ''
\echo '=== 4. Indexes, including the partial unique that limits one open dispute per call ======='

SELECT
    i.relname                                   AS index_name,
    pg_get_indexdef(i.oid)                      AS definition,
    idx.indisvalid                              AS is_valid
FROM pg_index idx
JOIN pg_class c ON c.oid = idx.indrelid
JOIN pg_class i ON i.oid = idx.indexrelid
WHERE c.relname = 'DialogReviewNotes'
ORDER BY i.relname;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_class i
        JOIN pg_index idx ON idx.indexrelid = i.oid
        JOIN pg_class c ON c.oid = idx.indrelid
        WHERE c.relname = 'DialogReviewNotes'
          AND i.relname = 'UX_DialogReviewNotes_OpenDisputePerSession'
          AND idx.indisunique
          AND idx.indisvalid
    ) THEN
        RAISE EXCEPTION
            'UX_DialogReviewNotes_OpenDisputePerSession is missing or invalid. Without it one '
            'complaint can be filed a hundred times, and a queue that can be flooded is a queue '
            'the ROP stops opening.';
    END IF;
END $$;


\echo ''
\echo '=== 5. What is actually in the table, by kind and status ================================='

SELECT
    "Kind",
    "Status",
    count(*)                                    AS rows,
    count(*) FILTER (WHERE "QuotedText" IS NOT NULL) AS with_quote,
    min("CreatedAt")                            AS oldest,
    max("CreatedAt")                            AS newest
FROM "DialogReviewNotes"
GROUP BY "Kind", "Status"
ORDER BY "Kind", "Status";

-- Every open row is somebody waiting. Disputes waiting longest first: this is the number that says
-- whether the mechanism is being used or has quietly become a place complaints go to die.
SELECT
    "Id",
    "SessionId",
    "DialogModeKey",
    "SubjectUserId",
    "DisputedScore",
    "CreatedAt",
    now() - "CreatedAt"                         AS waiting_for
FROM "DialogReviewNotes"
WHERE "Kind" = 'score_dispute'
  AND "Status" = 'open'
ORDER BY "CreatedAt"
LIMIT 50;


\echo ''
\echo '=== 6. THE DATASET: every human-labelled disagreement with a machine grade ==============='

-- This is the roadmap's second reason for the dispute mechanism — «плюс это даёт размеченные данные
-- для настройки промптов оценки». One row per ruled-on dispute:
--
--   dialog_mode_key  — which grading prompt produced the grade. This is the grouping key: "which
--                      scenarios do managers argue with" is the question that points at a prompt.
--   machine_score    — what the AI said, frozen at the moment the dispute was filed.
--   human_score      — what the ROP said it should have been, on an upheld dispute. NULL on a
--                      rejection and on an upheld dispute where the ROP did not name a number.
--   verdict          — upheld / rejected. A rejection is just as much labelled data as an upheld
--                      one: it says the grade was defensible, which is what a regression set needs.
--   manager_argument — the manager's own words for why it was wrong.
--   rop_reasoning    — the ROP's words. Always present on a rejection (the service requires it).
--
-- The conversation itself is NOT here: transcripts live in ai-service's Mongo (`dialog_sessions`),
-- and joining them is a deliberate second step outside this database. `session_id` is the join key;
-- see docs/AI_SERVICE.md. Whoever assembles the training set has to make the retention and consent
-- decision that touching transcripts implies, and it must not happen as a side effect of running a
-- verification script.
SELECT
    n."Id"                                      AS note_id,
    n."OrganizationId"                          AS organization_id,
    n."SessionId"                               AS session_id,
    n."DialogModeKey"                           AS dialog_mode_key,
    n."DisputedScore"                           AS machine_score,
    n."AdjustedScore"                           AS human_score,
    n."Status"                                  AS verdict,
    n."Comment"                                 AS manager_argument,
    n."Resolution"                              AS rop_reasoning,
    n."QuotedFromMessageIndex"                  AS quote_from_index,
    n."QuotedToMessageIndex"                    AS quote_to_index,
    n."QuotedText"                              AS quoted_fragment,
    n."CreatedAt"                               AS filed_at,
    n."ResolvedAt"                              AS ruled_at
FROM "DialogReviewNotes" n
WHERE n."Kind" = 'score_dispute'
  AND n."Status" IN ('upheld', 'rejected')
ORDER BY n."DialogModeKey", n."ResolvedAt";

-- The summary a prompt engineer actually starts from: which scenario's grading is argued with most,
-- and how often the argument turns out to be right.
SELECT
    "DialogModeKey"                             AS dialog_mode_key,
    count(*)                                    AS disputes_ruled_on,
    count(*) FILTER (WHERE "Status" = 'upheld')   AS upheld,
    count(*) FILTER (WHERE "Status" = 'rejected') AS rejected,
    round(
        100.0 * count(*) FILTER (WHERE "Status" = 'upheld') / nullif(count(*), 0),
        1
    )                                           AS upheld_percent,
    round(avg("DisputedScore") FILTER (WHERE "Status" = 'upheld'), 1)  AS avg_machine_score_when_wrong,
    round(avg("AdjustedScore") FILTER (WHERE "Status" = 'upheld'), 1)  AS avg_human_score_when_wrong
FROM "DialogReviewNotes"
WHERE "Kind" = 'score_dispute'
  AND "Status" IN ('upheld', 'rejected')
GROUP BY "DialogModeKey"
ORDER BY disputes_ruled_on DESC;


\echo ''
\echo '=== 7. Cross-check: no review note points at a score row of another organization ========='

-- Nothing writes SessionId from a request — every insert copies it from the UserDialogScores row
-- for that session, which is itself under row-level security. This asserts that the property held.
-- Rows here mean a writer appeared that did not go through DialogReviewService.
SELECT
    n."Id"                                      AS note_id,
    n."OrganizationId"                          AS note_organization_id,
    n."SessionId"                               AS session_id,
    n."SubjectUserId"                           AS note_subject_user_id
FROM "DialogReviewNotes" n
WHERE NOT EXISTS (
    SELECT 1
    FROM "UserDialogScores" s
    WHERE s."SessionId" = n."SessionId"
      AND s."OrganizationId" = n."OrganizationId"
      AND s."UserId" = n."SubjectUserId"
);

\echo ''
\echo '=== done ================================================================================='
