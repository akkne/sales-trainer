-- Sellevate — Phase 40.31: check the metric-to-content loop, and see what it would propose.
--
-- READ-ONLY. Every statement in this file is a SELECT. It creates nothing, drops nothing, updates
-- nothing, and is safe to run against a production database with the service up. NOT executed by
-- any automated process (build, migration, CI, or agent run).
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d learning -f 40.31_skill_gaps_verify.sql
--
--
-- WHAT 40.31 ADDED, IN ONE PARAGRAPH, SO THE QUERIES BELOW CAN BE READ
--
-- The dashboard's heat map (40.25, GET /admin/team/skill-map) is a report. 40.31 makes it a tool:
-- a stage of the sales funnel the team is failing at is offered as a button that starts the 40.27
-- content pipeline on it. The suggestion itself is NOT STORED — it is recomputed from the same
-- matrix on every read, so a gap that closes stops being offered without anything having to notice.
-- Only two things touch the schema:
--
--   * `TeamSkillGapDismissals` — one row per stage per organization, written when the ROP presses
--     «не сейчас». This is the one fact the matrix cannot derive.
--   * `ContentGenerationJobs."GapSourceRef"` — `skill-gap:<stage>@<yyyy-MM-dd>` on a run the
--     dashboard started. It is the provenance an assignment copies into `Assignments."SourceRef"`
--     with `SourceType = 'gap_detected'`, and it is also what stops the panel offering the same
--     stage twice.
--
-- The gap thresholds, all of them decided by the agent and recorded in docs/DECISIONS.md:
--   * at least 20 attempts on the stage inside the window (default 90 days),
--   * team accuracy at or below 60%,
--   * at least 2 managers with a reportable cell at or below 60% (a cell needs 5 attempts to
--     report anything at all — that floor is 40.25's).
--
-- Two things this script CANNOT tell you, stated so nobody reads its silence as a verdict:
--
--   1. Who still works here. The panel's «X из Y менеджеров» counts come from the live roster
--      identity-service returns over HTTP (`GET /internal/memberships/active`), and this database
--      does not hold it. Section 4 below therefore counts everybody with attempts, which is the
--      same fallback the service uses when identity is unreachable.
--   2. Whether a run's material was any good. Sufficiency is 40.28's, and its verdict is on the
--      run's `Insufficiency` document, not here.
--
-- Set the tenant before running anything below. Every table here carries strict row-level security,
-- and an unset organization is meant to return nothing rather than everything:
--
--   SET LOCAL app.organization_id = '00000000-0000-0000-0000-000000000000';  -- your org id


-- ─────────────────────────────────────────────────────────────────────────────
-- 1. The schema is what the migration says it is.
--
-- Expected: three CHECKs on TeamSkillGapDismissals (_Window, _Measurement, _StageKey), one on
-- ContentGenerationJobs (_GapSourceRef), and the unique index that makes "one live refusal per
-- stage" a database fact rather than a service convention.
-- ─────────────────────────────────────────────────────────────────────────────
SELECT
    rel.relname                AS table_name,
    con.conname                AS constraint_name,
    pg_get_constraintdef(con.oid) AS definition
FROM pg_constraint con
JOIN pg_class rel ON rel.oid = con.conrelid
WHERE rel.relname IN ('TeamSkillGapDismissals', 'ContentGenerationJobs')
  AND con.contype = 'c'
ORDER BY rel.relname, con.conname;

SELECT
    indexname,
    indexdef
FROM pg_indexes
WHERE tablename IN ('TeamSkillGapDismissals', 'ContentGenerationJobs')
ORDER BY tablename, indexname;


-- ─────────────────────────────────────────────────────────────────────────────
-- 2. Row-level security is on, forced, and is plain equality.
--
-- A dismissal is one organization's decision about its own panel. The policy must NOT be the
-- content flavour ("IS NULL OR = current") — a null owner here would silence one customer's
-- suggestion for every other. Expected: relrowsecurity = t, relforcerowsecurity = t, and a policy
-- whose qualifier compares OrganizationId to current_setting('app.organization_id').
-- ─────────────────────────────────────────────────────────────────────────────
SELECT
    rel.relname            AS table_name,
    rel.relrowsecurity     AS row_security_enabled,
    rel.relforcerowsecurity AS row_security_forced
FROM pg_class rel
JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
WHERE nsp.nspname = 'public'
  AND rel.relname = 'TeamSkillGapDismissals';

SELECT
    tablename,
    policyname,
    cmd,
    qual,
    with_check
FROM pg_policies
WHERE tablename = 'TeamSkillGapDismissals'
ORDER BY policyname;


-- ─────────────────────────────────────────────────────────────────────────────
-- 3. Every gap reference in the database parses back to a stage.
--
-- The CHECK only guarantees the namespace. This asserts the stronger property the panel actually
-- relies on: the string splits into a stage key that exists on a skill, and a date. A row here is
-- a run whose provenance nobody can read — the panel will neither suppress on it nor explain it.
--
-- Expected: zero rows.
-- ─────────────────────────────────────────────────────────────────────────────
SELECT
    j."Id"            AS job_id,
    j."Status",
    j."GapSourceRef",
    j."CreatedAt"
FROM "ContentGenerationJobs" j
WHERE j."GapSourceRef" IS NOT NULL
  AND (
        split_part(split_part(j."GapSourceRef", ':', 2), '@', 1) = ''
        OR split_part(j."GapSourceRef", '@', 2) !~ '^\d{4}-\d{2}-\d{2}$'
        OR split_part(split_part(j."GapSourceRef", ':', 2), '@', 1) NOT IN (
            SELECT DISTINCT s."Stage" FROM "Skills" s
        )
      )
ORDER BY j."CreatedAt" DESC;


-- ─────────────────────────────────────────────────────────────────────────────
-- 4. What the panel would propose right now.
--
-- This is the service's own detection, written out in SQL, over the default 90-day window and
-- WITHOUT the roster (see the caveat at the top: this database does not hold it). Skill
-- attribution goes through the live `Exercises` table exactly as `TeamSkillMapService` does —
-- attempts whose exercise was deleted lose attribution and are simply absent here.
--
-- `qualifies` is the three-condition verdict. A stage with `qualifies = true` is what the ROP would
-- see a button for, unless section 5 or 6 is suppressing it.
-- ─────────────────────────────────────────────────────────────────────────────
WITH attributed AS (
    SELECT
        att."UserId",
        s."Stage" AS stage_key,
        att."IsCorrect"
    FROM "UserExerciseAttempts" att
    JOIN "Exercises" e ON e."Id" = att."ExerciseId"
    JOIN "Lessons"   l ON l."Id" = e."LessonId"
    JOIN "Topics"    t ON t."Id" = l."TopicId"
    JOIN "Skills"    s ON s."Id" = t."SkillId"
    WHERE att."AttemptedAt" >= now() - interval '90 days'
),
per_member AS (
    SELECT
        stage_key,
        "UserId",
        count(*)                                             AS attempt_count,
        round(100.0 * count(*) FILTER (WHERE "IsCorrect") / count(*)) AS accuracy_percent
    FROM attributed
    GROUP BY stage_key, "UserId"
),
per_stage AS (
    SELECT
        stage_key,
        sum(attempt_count)                                   AS attempt_count,
        round(100.0 * sum(attempt_count * accuracy_percent) / nullif(sum(attempt_count), 0) / 100.0 * 100) AS approximate_accuracy_percent,
        count(*) FILTER (WHERE attempt_count >= 5)           AS measured_managers,
        count(*) FILTER (WHERE attempt_count >= 5 AND accuracy_percent <= 60) AS struggling_managers
    FROM per_member
    GROUP BY stage_key
)
SELECT
    p.stage_key,
    coalesce(st."Label", p.stage_key)                        AS stage_label,
    p.attempt_count,
    p.approximate_accuracy_percent,
    p.measured_managers,
    p.struggling_managers,
    (p.attempt_count >= 20
     AND p.approximate_accuracy_percent <= 60
     AND p.struggling_managers >= 2)                         AS qualifies
FROM per_stage p
LEFT JOIN "SkillStages" st ON st."Key" = p.stage_key
ORDER BY p.approximate_accuracy_percent, p.stage_key;


-- ─────────────────────────────────────────────────────────────────────────────
-- 5. Live refusals, and whether each one is still holding.
--
-- A dismissal expires after 90 days — the heat map's own default window, so a refusal lasts exactly
-- as long as the measurement that provoked it could still be the same measurement. It is also
-- broken early when the number falls 10 points below what it was when the refusal was recorded:
-- «мы это знаем» was said about one number and is not an answer to a much worse one.
-- ─────────────────────────────────────────────────────────────────────────────
SELECT
    d."StageKey",
    d."AccuracyPercentAtDismissal",
    d."AttemptCountAtDismissal",
    d."DismissedBy",
    d."DismissedAt",
    d."ExpiresAt",
    (d."ExpiresAt" > now())                                  AS still_live,
    d."AccuracyPercentAtDismissal" - 10                      AS reopens_below_percent,
    d."Note"
FROM "TeamSkillGapDismissals" d
ORDER BY d."ExpiresAt" DESC;


-- ─────────────────────────────────────────────────────────────────────────────
-- 6. Runs the dashboard started, and what became of them.
--
-- `run_state` is the reason the panel is (or is not) suppressing that stage:
--   * `open`               — structuring / awaiting_review / generating / insufficient. The stage is
--                            not offered, and pressing the button returns THIS run rather than
--                            starting a second one.
--   * `recently_addressed` — completed inside the last 30 days.
--   * `spent`              — completed longer ago, or failed. Not suppressing anything.
--
-- A run in `completed` with a `ProducedLessonVersionId` is what `POST /admin/assignments` needs in
-- order to create the `gap_detected` assignment: it copies `GapSourceRef` into `SourceRef` and pins
-- the frozen version as the assignment's content.
-- ─────────────────────────────────────────────────────────────────────────────
SELECT
    j."Id"                                                   AS job_id,
    split_part(split_part(j."GapSourceRef", ':', 2), '@', 1) AS stage_key,
    split_part(j."GapSourceRef", '@', 2)                     AS observed_on,
    j."Status",
    CASE
        WHEN j."Status" IN ('structuring', 'awaiting_review', 'generating', 'insufficient') THEN 'open'
        WHEN j."Status" = 'completed' AND j."CreatedAt" >= now() - interval '30 days' THEN 'recently_addressed'
        ELSE 'spent'
    END                                                      AS run_state,
    j."ProducedLessonId",
    j."ProducedLessonVersionId",
    j."ProducedExerciseCount",
    j."CreatedAt",
    j."GeneratedAt"
FROM "ContentGenerationJobs" j
WHERE j."GapSourceRef" IS NOT NULL
ORDER BY j."CreatedAt" DESC;


-- ─────────────────────────────────────────────────────────────────────────────
-- 7. The loop, closed: assignments that exist because a number was bad.
--
-- This is the query the block exists to make answerable. Each row is an assignment whose source is
-- a measurement rather than a document, joined back to the run that produced its content and the
-- lesson that run generated. The observed numbers are not in `SourceRef` — they are in `Goal`,
-- written by the service at creation time, which is why a year-old row still reads.
--
-- `CK_Assignments_ManualHasNoSourceRef` guarantees the inverse direction (a manual assignment can
-- never carry one of these), so this listing is exhaustive.
-- ─────────────────────────────────────────────────────────────────────────────
SELECT
    a."Id"                                                   AS assignment_id,
    a."Title",
    a."Status",
    a."SourceRef",
    split_part(split_part(a."SourceRef", ':', 2), '@', 1)    AS stage_key,
    split_part(a."SourceRef", '@', 2)                        AS observed_on,
    a."Goal",
    j."Id"                                                   AS job_id,
    j."ProducedLessonId",
    a."CreatedAt",
    a."ActivatedAt"
FROM "Assignments" a
LEFT JOIN "ContentGenerationJobs" j ON j."GapSourceRef" = a."SourceRef"
WHERE a."SourceType" = 'gap_detected'
ORDER BY a."CreatedAt" DESC;
