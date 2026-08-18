-- Sellevate — Phase 40.24: verify that automatic repeats landed correctly.
--
-- READ-ONLY. Every statement in this file is a SELECT or a DO block that only raises. It creates
-- nothing, drops nothing, updates nothing, and is safe to run against a production database with
-- the service up. NOT executed by any automated process (build, migration, CI, or agent run).
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d learning -f 40.24_assignment_repeats_verify.sql
--
-- Run it AFTER learning-service has started once on the 40.24 code, so that EF migration
-- 20260818001925_AddAssignmentRepeats has applied.
--
--
-- WHAT THIS BLOCK ADDED, AND WHAT IT IS FOR
--
-- The effect of an internal training decays in two to three weeks. A one-shot assignment therefore
-- reproduces exactly the failure the product exists to fix, which is why `repeat_schedule` was in
-- the design from 40.21 — stored, and deliberately uninterpreted, until now.
--
-- 40.24 gives it a vocabulary — {"kind":"fixed_offsets","offsetDays":[7,21]} — and a background
-- sweep that acts on it. A wave is a NEW `Assignments` row carrying `RepeatOfAssignmentId` (the
-- assignment a human created) and `RepeatWaveIndex` (which offset it is, 1-based). It is created
-- already `active`, issued to the ORIGIN's recipients intersected with the live roster, with the
-- theory dropped from its content and, for a dialog_score rule, half as many conversations required
-- — never a lower bar.
--
-- The one property to keep in mind while reading everything below: a wave has been issued exactly
-- when its row exists. There is no "wave 1 sent" flag anywhere, deliberately — the origin may be
-- `closed`, and a closed assignment cannot be updated at all (the 40.21 trigger freezes it whole),
-- so a stamp on the origin would have made closing an assignment silently cancel its repeats.
--
--
-- WHY THERE IS NO 40.24_..._indexes_concurrently.sql AND NO BACKFILL
--
-- Fifth block running, same reasoning as 40.15/40.17/40.18/40.21/40.22/40.23. `Assignments` is
-- empty in every deployed database — nothing could create an assignment before 40.21, and the РОП's
-- admin panel is still 40.20 — so both columns and the unique index land over zero rows, the ACCESS
-- EXCLUSIVE lock costs nothing, and no existing row changes meaning.
--
-- The index is created by the migration rather than deferred to a CONCURRENTLY script on purpose:
-- it is a CORRECTNESS constraint (one row per origin per wave), and it is the only thing standing
-- between two sweep ticks racing inside one window and two identical repeats landing on the same
-- team on the same morning. A correctness constraint that waits for a script somebody has to
-- remember to run is a correctness constraint that is not there.
--
--
-- WHAT THIS SCRIPT CANNOT SHOW YOU
--
-- Whether a repeat that was never issued SHOULD have been. The sweep skips a wave permanently once
-- it is more than `Assignments__RepeatCatchUpDays` (default 3) days late, and skipping is
-- recomputed rather than recorded — there is no row to find. Section 6 gives you the origins whose
-- waves are missing and how late they are; the log line to grep for is "too long ago to issue now".

\set ON_ERROR_STOP on

\echo '--- 1. schema: the two 40.24 columns are present ---'

DO $$
DECLARE
    missing_columns text;
BEGIN
    SELECT string_agg(expected.column_name, ', ' ORDER BY expected.column_name)
    INTO missing_columns
    FROM (VALUES ('RepeatOfAssignmentId'), ('RepeatWaveIndex')) AS expected(column_name)
    WHERE NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'Assignments'
          AND columns.column_name = expected.column_name
    );

    IF missing_columns IS NOT NULL THEN
        RAISE EXCEPTION
            'Column(s) "Assignments".% are missing. Did migration 20260818001925_AddAssignmentRepeats '
            'apply?', missing_columns;
    END IF;
END
$$;

\echo '--- 2. the unique index that is the sweep''s whole idempotency story ---'

-- Partial and NOT tenant-leading, both deliberate. It is the only index covering the new
-- self-referencing foreign key, and an origin id is globally unique already, so leading with
-- OrganizationId would weaken the uniqueness rather than scope it. Isolation is decided by the RLS
-- policy, never by an index.
-- Expected: exactly one row, indisunique = t, indisvalid = t.
SELECT index_class.relname AS index_name,
       pg_index.indisunique AS is_unique,
       pg_index.indisvalid  AS is_valid,
       pg_get_expr(pg_index.indpred, pg_index.indrelid) AS partial_where
FROM pg_index
JOIN pg_class AS index_class ON index_class.oid = pg_index.indexrelid
JOIN pg_class AS table_class ON table_class.oid = pg_index.indrelid
WHERE table_class.relname = 'Assignments'
  AND index_class.relname = 'IX_Assignments_RepeatOfAssignmentId_RepeatWaveIndex';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_index
        JOIN pg_class AS index_class ON index_class.oid = pg_index.indexrelid
        WHERE index_class.relname = 'IX_Assignments_RepeatOfAssignmentId_RepeatWaveIndex'
          AND pg_index.indisunique
          AND pg_index.indisvalid
    ) THEN
        RAISE EXCEPTION
            'The (RepeatOfAssignmentId, RepeatWaveIndex) index is missing, not unique, or invalid. '
            'Without it two sweep ticks racing inside one window issue the same repeat twice, and '
            'nothing else in the system would notice.';
    END IF;
END
$$;

\echo '--- 3. the three check constraints that keep a repeat from repeating ---'

-- CK_Assignments_RepeatNoCascade is the load-bearing one: a repeat that carried a schedule of its
-- own would repeat itself, and two waves would each spawn two more. Every individual step of that
-- cascade looks exactly like the feature working.
-- Expected: three rows.
SELECT conname AS constraint_name, pg_get_constraintdef(oid) AS definition
FROM pg_constraint
WHERE conrelid = '"Assignments"'::regclass
  AND conname LIKE 'CK_Assignments_Repeat%'
ORDER BY conname;

DO $$
DECLARE
    missing_constraints text;
BEGIN
    SELECT string_agg(expected.name, ', ' ORDER BY expected.name)
    INTO missing_constraints
    FROM (VALUES
        ('CK_Assignments_RepeatWave'),
        ('CK_Assignments_RepeatNoCascade'),
        ('CK_Assignments_RepeatNotSelf')
    ) AS expected(name)
    WHERE NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = '"Assignments"'::regclass
          AND conname = expected.name
    );

    IF missing_constraints IS NOT NULL THEN
        RAISE EXCEPTION 'Check constraint(s) % are missing.', missing_constraints;
    END IF;
END
$$;

\echo '--- 4. the freeze trigger now freezes the series columns too ---'

-- Which series an assignment belongs to and which wave it is are identity, and every recorded score
-- is read through them. RepeatSchedule stays OUT of the frozen set on purpose: editing it on an
-- active assignment is how a РОП cancels waves that have not gone out yet, which is the only
-- cancel path this block has (see docs/DONT_FORGET.md).
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

    IF function_body NOT LIKE '%RepeatOfAssignmentId%' OR function_body NOT LIKE '%RepeatWaveIndex%' THEN
        RAISE EXCEPTION
            'The freeze trigger does not mention the series columns. Did the 40.24 CREATE OR REPLACE '
            'run? An issued repeat could then be re-pointed at another origin after the fact.';
    END IF;

    IF function_body LIKE '%NEW."RepeatSchedule" IS DISTINCT FROM%' THEN
        RAISE EXCEPTION
            'The freeze trigger froze RepeatSchedule. Editing it on an active assignment is the only '
            'way a РОП can cancel waves that have not gone out yet.';
    END IF;
END
$$;

\echo '--- 5. every repeat is well-formed ---'

-- A repeat whose origin is itself a repeat. Nothing writes this — the sweep only ever picks origins
-- that carry a schedule, and a repeat is forbidden from carrying one — so a row here means the
-- series is more than one level deep and 40.25's grouping will read it wrongly.
-- Expected: 0 rows.
SELECT repeat."Id", repeat."Title", repeat."RepeatOfAssignmentId"
FROM "Assignments" AS repeat
JOIN "Assignments" AS origin ON origin."Id" = repeat."RepeatOfAssignmentId"
WHERE origin."RepeatOfAssignmentId" IS NOT NULL;

-- A repeat in a different organization from its origin. Impossible through the service (the sweep
-- runs one organization at a time with that organization set on the context, and the tenant save
-- interceptor stamps the column), and impossible through the RLS policy — checked because this is
-- the first foreign key in the feature that points at the same table.
-- Expected: 0.
SELECT count(*) AS repeats_in_a_different_organization_from_their_origin
FROM "Assignments" AS repeat
JOIN "Assignments" AS origin ON origin."Id" = repeat."RepeatOfAssignmentId"
WHERE repeat."OrganizationId" <> origin."OrganizationId";

-- A repeat that was never issued to anybody. The sweep refuses to create one with an empty cohort,
-- so a row here is an assignment nobody can complete and the screen cannot say so.
-- Expected: 0 rows.
SELECT repeat."Id", repeat."Title"
FROM "Assignments" AS repeat
WHERE repeat."RepeatOfAssignmentId" IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM "AssignmentProgressRecords" AS progress
      WHERE progress."AssignmentId" = repeat."Id"
  );

\echo '--- 6. the series, wave by wave: did it stick? ---'

-- This is the question the whole block exists to make answerable. Read it down a series: the same
-- cohort, the same bar, two or three weeks apart. A completion rate that falls off between wave 0
-- and wave 1 is the decay the roadmap is talking about, measured rather than asserted.
SELECT coalesce(assignment."RepeatOfAssignmentId", assignment."Id")        AS series_id,
       coalesce(assignment."RepeatWaveIndex", 0)                          AS wave,
       assignment."Title",
       assignment."Status",
       assignment."ActivatedAt",
       assignment."Deadline",
       count(progress.*)                                                  AS assigned,
       count(*) FILTER (WHERE progress."Status" <> 'not_started')         AS started,
       count(*) FILTER (WHERE progress."Status" = 'completed')            AS completed,
       count(*) FILTER (WHERE progress."Status" = 'failed_threshold')     AS under_threshold
FROM "Assignments" AS assignment
LEFT JOIN "AssignmentProgressRecords" AS progress ON progress."AssignmentId" = assignment."Id"
WHERE assignment."RepeatSchedule" IS NOT NULL
   OR assignment."RepeatOfAssignmentId" IS NOT NULL
GROUP BY series_id, wave, assignment."Title", assignment."Status",
         assignment."ActivatedAt", assignment."Deadline"
ORDER BY series_id, wave
LIMIT 100;

-- Origins with a wave that is overdue and missing. A few minutes of lateness is normal (the sweep
-- runs hourly by default). Anything past the catch-up window has been skipped permanently and will
-- never be issued — and the trap worth naming is that the sweep goes silent, without erroring, the
-- day the service moves to the NOBYPASSRLS role `sellevate_app`, because its cross-tenant
-- enumeration returns nothing. See docs/DONT_FORGET.md.
SELECT origin."Id",
       origin."Title",
       origin."Status",
       origin."ActivatedAt",
       origin."RepeatSchedule",
       offset_day.value                                                        AS offset_days,
       origin."ActivatedAt" + (offset_day.value || ' days')::interval          AS wave_due_at,
       now() - (origin."ActivatedAt" + (offset_day.value || ' days')::interval) AS overdue_by,
       offset_day.ordinality                                                   AS wave
FROM "Assignments" AS origin
CROSS JOIN LATERAL jsonb_array_elements_text(
    coalesce(origin."RepeatSchedule" -> 'offsetDays', '[7, 21]'::jsonb)
) WITH ORDINALITY AS offset_day(value, ordinality)
WHERE origin."RepeatSchedule" IS NOT NULL
  AND origin."RepeatOfAssignmentId" IS NULL
  AND origin."ActivatedAt" IS NOT NULL
  AND origin."Status" <> 'draft'
  AND origin."ActivatedAt" + (offset_day.value || ' days')::interval < now()
  AND NOT EXISTS (
      SELECT 1 FROM "Assignments" AS wave
      WHERE wave."RepeatOfAssignmentId" = origin."Id"
        AND wave."RepeatWaveIndex" = offset_day.ordinality
  )
ORDER BY overdue_by DESC
LIMIT 50;

\echo '--- 7. every wave told the people it asked ---'

-- A repeat stages one assignment.issued outbox row per recipient in the same transaction as their
-- progress row, exactly as a human-pressed issue does. A count far BELOW the number of progress
-- rows means somebody was asked and never told.
SELECT (SELECT count(*)
        FROM "AssignmentProgressRecords" AS progress
        JOIN "Assignments" AS assignment ON assignment."Id" = progress."AssignmentId"
        WHERE assignment."RepeatOfAssignmentId" IS NOT NULL)          AS people_asked_by_a_repeat,
       (SELECT count(*)
        FROM "OutboxMessages"
        WHERE "Topic" = 'assignment.issued'
          AND "DispatchedAt" IS NULL)                                 AS issue_notices_still_pending;
