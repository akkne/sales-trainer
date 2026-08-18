-- Sellevate — Phase 40.23: verify that the audience fan-out and the deadline notice landed correctly.
--
-- READ-ONLY. Every statement in this file is a SELECT or a DO block that only raises. It creates
-- nothing, drops nothing, updates nothing, and is safe to run against a production database with
-- the service up. NOT executed by any automated process (build, migration, CI, or agent run).
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d learning -f 40.23_assignment_fanout_verify.sql
--
-- Run it AFTER learning-service has started once on the 40.23 code, so that EF migration
-- 20260817234012_AddAssignmentDeadlineNotice has applied.
--
--
-- WHAT THIS BLOCK ADDED, AND WHAT IT IS FOR
--
-- 40.21 built `AssignmentProgressRecords`. 40.22 built what UPDATES a row. Neither built what
-- CREATES one — and a row's existence is the record that a person was asked to do something, which
-- is a fact about the moment a human pressed "issue". This block is that moment: it resolves the
-- audience RULE (whole_team / users / group) into named people by asking identity-service who
-- currently works here, writes one not_started row per recipient, and stages one assignment.issued
-- outbox event per recipient in the same transaction.
--
-- The consequence for everything before it: from this block on, the funnel counts on the РОП's
-- screen and the threshold evaluation 40.22 wrote finally run over a non-empty set. Before it they
-- were correct and unobservable.
--
-- The schema addition is one nullable column, `Assignments."DeadlineNoticeSentAt"`: when the "your
-- deadline is close" notice went out for the deadline the assignment CURRENTLY has. Moving the
-- deadline clears it, which is what makes an extension announce itself.
--
--
-- WHY THERE IS NO 40.23_..._indexes_concurrently.sql AND NO BACKFILL
--
-- Fourth block running, same reasoning as 40.15/40.17/40.18/40.21/40.22. `Assignments` is empty in
-- every deployed database: nothing could create an assignment before 40.21, and 40.21 shipped
-- without an admin screen. So the column is added over zero rows, the ACCESS EXCLUSIVE lock costs
-- nothing, and no existing row changes meaning.
--
-- No index either, and that IS a decision. The deadline sweep's enumeration is the one query in
-- this service that filters without leading on OrganizationId — "which organizations have an
-- unannounced deadline coming", asked across all of them — so an index for it would have to be a
-- partial index on (Deadline), the exact shape the convention since 40.10 exists to prevent. Over a
-- table that grows at the rate a human writes assignments, the scan is cheaper than the exception.
--
--
-- WHAT THIS SCRIPT CANNOT SHOW YOU
--
-- Whether the roster identity-service returned was COMPLETE. This is the one thing in the block
-- that a query against learning-db cannot answer, because learning-db does not hold memberships on
-- purpose (docs/DECISIONS.md, 2026-08-18). Section 5 gives you the counts per assignment; the check
-- is a human comparing one of them against the organization's headcount in the admin panel.

\set ON_ERROR_STOP on

\echo '--- 1. schema: the 40.23 column is present ---'

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'Assignments'
          AND column_name = 'DeadlineNoticeSentAt'
    ) THEN
        RAISE EXCEPTION
            'Column "Assignments"."DeadlineNoticeSentAt" is missing. Did migration '
            '20260817234012_AddAssignmentDeadlineNotice apply?';
    END IF;
END
$$;

\echo '--- 2. the freeze trigger still lets the sweep write, and still freezes what it must ---'

-- The 40.21 trigger refuses changes to source_type, source_ref, content, completion_rule,
-- organization_id and activated_at once an assignment is active. DeadlineNoticeSentAt is
-- deliberately NOT in that set — the sweep writes it on active rows every time it announces a
-- deadline. Expected: the trigger exists and names the six frozen columns and not the new one.
SELECT tgname AS trigger_name, tgenabled AS enabled
FROM pg_trigger
JOIN pg_class ON pg_class.oid = pg_trigger.tgrelid
WHERE pg_class.relname = 'Assignments'
  AND NOT tgisinternal;

DO $$
DECLARE
    function_body text;
BEGIN
    SELECT prosrc INTO function_body
    FROM pg_proc
    WHERE proname = 'assignment_reject_frozen_change';

    IF function_body IS NULL THEN
        RAISE EXCEPTION 'Trigger function assignment_reject_frozen_change is missing (40.21).';
    END IF;

    IF function_body LIKE '%DeadlineNoticeSentAt%' THEN
        RAISE EXCEPTION
            'The freeze trigger mentions DeadlineNoticeSentAt. If it froze that column the deadline '
            'sweep could never mark an active assignment as announced, and it would re-announce the '
            'same deadline on every tick — forever.';
    END IF;
END
$$;

\echo '--- 3. row-level security on the progress table is still STRICT equality ---'

-- Unchanged by this block, checked because this block is the first one that puts rows in the table.
-- Expected: rowsecurity = t, forcerowsecurity = t, and NEITHER clause containing 'IS NULL'.
SELECT relname AS table_name,
       relrowsecurity      AS row_security_enabled,
       relforcerowsecurity AS row_security_forced
FROM pg_class
JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
WHERE pg_namespace.nspname = current_schema()
  AND relname IN ('Assignments', 'AssignmentProgressRecords')
ORDER BY relname;

DO $$
DECLARE
    content_flavoured_policies text;
BEGIN
    SELECT string_agg(tablename || '.' || policyname, ', ' ORDER BY tablename, policyname)
    INTO content_flavoured_policies
    FROM pg_policies
    WHERE tablename IN ('Assignments', 'AssignmentProgressRecords')
      AND (coalesce(qual, '') LIKE '%IS NULL%' OR coalesce(with_check, '') LIKE '%IS NULL%');

    IF content_flavoured_policies IS NOT NULL THEN
        RAISE EXCEPTION
            'Policy(ies) % use the CONTENT flavour (IS NULL OR = current). There is no such thing as '
            'a global assignment, and a NULL owner here would mean everybody''s homework.',
            content_flavoured_policies;
    END IF;
END
$$;

\echo '--- 4. every progress row belongs to the same organization as its assignment ---'

-- The denormalized copy on AssignmentProgressRecords is what the RLS policy compares (a policy can
-- only see columns of the row it filters). The fan-out never assigns it by hand — the tenant save
-- interceptor stamps it — so a mismatch here means somebody wrote rows outside the service.
-- Expected: 0.
SELECT count(*) AS progress_rows_whose_organization_disagrees_with_their_assignment
FROM "AssignmentProgressRecords" AS progress
JOIN "Assignments" AS assignment ON assignment."Id" = progress."AssignmentId"
WHERE progress."OrganizationId" <> assignment."OrganizationId";

\echo '--- 5. the funnel, per assignment: this is what 40.21 and 40.22 could not show ---'

-- Before this block every one of these counts was zero for every assignment, honestly. If they are
-- STILL all zero after somebody has issued an assignment, the fan-out did not run: check
-- learning-service's log for "the organization roster could not be read" and confirm
-- IdentityService__BaseUrl points at a reachable identity-service.
SELECT assignment."Id",
       assignment."Title",
       assignment."Status",
       assignment."Deadline",
       assignment."DeadlineNoticeSentAt",
       count(progress.*)                                                                AS assigned,
       count(*) FILTER (WHERE progress."Status" <> 'not_started')                       AS started,
       count(*) FILTER (WHERE progress."Status" = 'completed')                          AS completed,
       count(*) FILTER (WHERE progress."Status" = 'failed_threshold')                   AS under_threshold
FROM "Assignments" AS assignment
LEFT JOIN "AssignmentProgressRecords" AS progress ON progress."AssignmentId" = assignment."Id"
GROUP BY assignment."Id", assignment."Title", assignment."Status",
         assignment."Deadline", assignment."DeadlineNoticeSentAt"
ORDER BY assignment."CreatedAt" DESC
LIMIT 50;

-- An ACTIVE assignment with no recipients at all. The service refuses to issue to an empty audience,
-- so a row here was either issued before 40.23 (impossible in a fresh installation) or written by
-- hand. Whatever it is, nobody can complete it and the screen cannot say so.
-- Expected: 0 rows.
SELECT assignment."Id", assignment."Title", assignment."Audience"
FROM "Assignments" AS assignment
WHERE assignment."Status" = 'active'
  AND NOT EXISTS (
      SELECT 1 FROM "AssignmentProgressRecords" AS progress
      WHERE progress."AssignmentId" = assignment."Id"
  );

\echo '--- 6. one row per person per assignment ---'

-- The fan-out skips whoever already has a row, and the unique index (OrganizationId, AssignmentId,
-- UserId) from 40.21 is the guarantee behind it. A duplicate would double-count somebody in every
-- funnel number above.
-- Expected: 0 rows.
SELECT "AssignmentId", "UserId", count(*) AS row_count
FROM "AssignmentProgressRecords"
GROUP BY "AssignmentId", "UserId"
HAVING count(*) > 1;

\echo '--- 7. the deadline notice: announced exactly once per deadline ---'

-- An active assignment whose deadline is inside the lead window and has NOT been announced. A few
-- are normal (the sweep runs every 30 minutes by default); a pile of them that never clears means
-- the sweep is not running, or — the trap worth naming — it is running under a NOBYPASSRLS role and
-- its cross-tenant enumeration silently returns nothing. See docs/DONT_FORGET.md.
SELECT "Id", "Title", "Deadline", "DeadlineNoticeSentAt"
FROM "Assignments"
WHERE "Status" = 'active'
  AND "Deadline" IS NOT NULL
  AND "Deadline" > now()
  AND "Deadline" <= now() + interval '24 hours'
  AND "DeadlineNoticeSentAt" IS NULL
ORDER BY "Deadline"
LIMIT 50;

-- A notice stamped for an assignment with no deadline at all. Nothing writes this combination —
-- the sweep only touches rows whose deadline is set, and clearing a deadline is an ordinary edit
-- that also clears the stamp.
-- Expected: 0.
SELECT count(*) AS announced_assignments_with_no_deadline
FROM "Assignments"
WHERE "Deadline" IS NULL
  AND "DeadlineNoticeSentAt" IS NOT NULL;

\echo '--- 8. the three notices actually left the building ---'

-- Every assignment notification goes out through the transactional outbox in the same transaction
-- as the row it describes, so a staged-but-unsent pile means the relay is stuck rather than that
-- the fan-out failed.
-- Expected: small and shrinking.
SELECT "Topic", count(*) AS pending
FROM "OutboxMessages"
WHERE "DispatchedAt" IS NULL
  AND "Topic" IN ('assignment.issued', 'assignment.deadline.approaching', 'assignment.reminder')
GROUP BY "Topic"
ORDER BY "Topic";

-- Sanity: an assignment.issued event per progress row, give or take the rows added by an audience
-- edit after an outbox row was already pruned. A count far BELOW the number of progress rows means
-- somebody was asked and never told.
SELECT (SELECT count(*) FROM "AssignmentProgressRecords")                                  AS people_asked,
       (SELECT count(*) FROM "OutboxMessages" WHERE "Topic" = 'assignment.issued')         AS issue_notices_staged;
