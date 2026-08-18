-- Sellevate — Phase 40.26: see what the deadline sweep is about to tell the ROP, and why.
--
-- READ-ONLY. Every statement in this file is a SELECT. It creates nothing, drops nothing, updates
-- nothing, and is safe to run against a production database with the service up. NOT executed by
-- any automated process (build, migration, CI, or agent run).
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d learning -f 40.26_deadline_digest_verify.sql
--
--
-- WHY THIS FILE EXISTS AT ALL, GIVEN THAT 40.26 HAS NO MIGRATION
--
-- 40.26 added no table, no column and no index. Its whole schema footprint is zero: the digest to
-- the ROP is published by the sweep 40.23 already built, in the same transaction, about the same
-- date, and stamped by the same `Assignments."DeadlineNoticeSentAt"`. There is consequently nothing
-- to *verify* in the sense the 40.21-40.25 scripts mean it — no constraint to assert, no policy to
-- check, no backfill to reconcile.
--
-- What there is instead is a visibility problem. The ROP has no screen (roadmap 40.20 waits on the
-- owner's design), so the only way for a human to see whether this feature is doing anything is to
-- ask the database the same questions the sweep asks. That is what the four sections below are:
-- the sweep's own predicate, written out, so an operator can answer "who would get a digest
-- tonight, about whom, and did last night's go out".
--
-- The sweep's shape, in one paragraph, so the queries below can be read:
--
--   * It looks for `active` assignments whose `Deadline` is in the future and within the lead
--     window (`Assignments__DeadlineNoticeLeadHours`, 24 by default), and whose
--     `DeadlineNoticeSentAt` is still NULL.
--   * For each such assignment it warns every unfinished recipient who still holds an active
--     membership, and sends every administrator of the organization a digest naming the recipients
--     whose status is still `not_started`.
--   * If nobody is `not_started`, no digest is sent — "everybody has at least started" is not news,
--     and a notice that says so teaches its reader to skip the channel.
--   * Then it stamps `DeadlineNoticeSentAt`, whether or not anything was sent.
--
-- Two things this script CANNOT tell you, stated so nobody reads its silence as a verdict:
--
--   1. Who the administrators are. Memberships and roles live in identity-db, a different database
--      owned by a different service; learning-service asks over HTTP
--      (`GET /internal/memberships/active`, which 40.26 widened with `administratorUserIds`).
--      Section 4 gives the identity-db query to run separately.
--   2. Whether a notification was actually delivered. The outbox row and the Redis inbox are the
--      evidence for that, not these tables.
--
-- Set the tenant before running anything below. Every table here carries strict row-level security,
-- and an unset organization is meant to return nothing rather than everything:
--
--   SET LOCAL app.organization_id = '00000000-0000-0000-0000-000000000000';  -- your org id


-- ─────────────────────────────────────────────────────────────────────────────
-- 1. What the next tick would announce: assignments inside the lead window.
--
-- The `hours_left` column is the honest reading of "a day before the deadline": the sweep runs
-- every 30 minutes (`Assignments__SweepIntervalMinutes`), so a row here will be picked up within
-- half an hour of appearing.
-- ─────────────────────────────────────────────────────────────────────────────
SELECT
    a."Id"                                                        AS assignment_id,
    a."Title"                                                     AS title,
    a."Deadline"                                                  AS deadline,
    round(EXTRACT(EPOCH FROM (a."Deadline" - now())) / 3600.0, 1) AS hours_left,
    a."RepeatOfAssignmentId"                                      AS repeat_of,
    a."RepeatWaveIndex"                                           AS wave
FROM "Assignments" a
WHERE a."Status" = 'active'
  AND a."Deadline" IS NOT NULL
  AND a."Deadline" > now()
  AND a."Deadline" <= now() + interval '24 hours'   -- keep in step with DeadlineNoticeLeadHours
  AND a."DeadlineNoticeSentAt" IS NULL
ORDER BY a."Deadline";


-- ─────────────────────────────────────────────────────────────────────────────
-- 2. The digest itself, per assignment: who has not started, and how many.
--
-- This is the list the notification body carries — up to five names, with the true total beside
-- them. `not_started_count = 0` is the case in which NO digest is sent at all; it appears here
-- deliberately, because "the sweep stayed silent" and "the sweep never ran" look identical from a
-- notification inbox and completely different from this query.
--
-- Note the join to "UserReplicas": that table is platform-global by design (a user is a
-- cross-organization identity), which is why it is joined on `UserId` alone. A recipient with no
-- replica row contributes to the count and not to the names, which is also what the sweep does.
-- ─────────────────────────────────────────────────────────────────────────────
SELECT
    a."Id"                                                         AS assignment_id,
    a."Title"                                                      AS title,
    a."Deadline"                                                   AS deadline,
    count(*) FILTER (WHERE p."Status" = 'not_started')             AS not_started_count,
    count(*) FILTER (WHERE p."Status" = 'in_progress')             AS in_progress_count,
    count(*) FILTER (WHERE p."Status" = 'failed_threshold')        AS failed_threshold_count,
    count(*) FILTER (WHERE p."Status" = 'completed')               AS completed_count,
    array_agg(u."DisplayName" ORDER BY u."DisplayName")
        FILTER (WHERE p."Status" = 'not_started' AND u."DisplayName" IS NOT NULL)
                                                                   AS not_started_names
FROM "Assignments" a
JOIN "AssignmentProgressRecords" p ON p."AssignmentId" = a."Id"
LEFT JOIN "UserReplicas" u ON u."UserId" = p."UserId"
WHERE a."Status" = 'active'
  AND a."Deadline" IS NOT NULL
  AND a."Deadline" > now()
  AND a."Deadline" <= now() + interval '24 hours'
GROUP BY a."Id", a."Title", a."Deadline"
ORDER BY a."Deadline";


-- ─────────────────────────────────────────────────────────────────────────────
-- 3. Did the last announcement happen, and is anything stuck?
--
-- `DeadlineNoticeSentAt` is stamped even when nobody needed warning, so a row that is inside the
-- window with a NULL stamp and an old `UpdatedAt` is the shape of a sweep that is NOT running —
-- the failure mode worth looking for, because it is silent. The most likely cause is the one
-- docs/DONT_FORGET.md records for six jobs: learning-service running under a NOBYPASSRLS role, in
-- which the sweep's system-mode enumeration comes back empty and nothing errors.
--
-- The second query is the same question asked backwards: deadlines that have already passed with
-- nothing announced. Those are not a bug on their own (an assignment created with a deadline
-- already inside the window, or in the past, was never announceable), but a growing count means
-- the sweep is not keeping up.
-- ─────────────────────────────────────────────────────────────────────────────
SELECT
    a."Id"                     AS assignment_id,
    a."Title"                  AS title,
    a."Deadline"               AS deadline,
    a."DeadlineNoticeSentAt"   AS announced_at,
    a."UpdatedAt"              AS updated_at
FROM "Assignments" a
WHERE a."Status" = 'active'
  AND a."Deadline" IS NOT NULL
  AND a."Deadline" > now()
  AND a."Deadline" <= now() + interval '24 hours'
ORDER BY a."DeadlineNoticeSentAt" NULLS FIRST, a."Deadline";

SELECT
    count(*) FILTER (WHERE a."DeadlineNoticeSentAt" IS NULL) AS passed_without_notice,
    count(*)                                                AS passed_total
FROM "Assignments" a
WHERE a."Deadline" IS NOT NULL
  AND a."Deadline" <= now();


-- ─────────────────────────────────────────────────────────────────────────────
-- 4. Who would receive it — RUN THIS AGAINST identity-db, NOT learning-db.
--
-- 40.26's one new capability: the organization's administrators are now enumerable, which is what
-- lets a notification be addressed to "the ROP" at all. Both tenancy administrator roles qualify
-- (1 = TenancyAdmin, 2 = TenancySuperAdmin); they differ only in who may add and remove people,
-- which has nothing to do with who should be told the team is missing a deadline.
--
-- An EMPTY result here is the quiet failure worth knowing about: the sweep will send the manager
-- notices and no digest at all, because there is nobody to address it to. That is an organization
-- whose every membership is a plain manager, and it is a real configuration, not a bug — but it
-- means this whole feature is invisible for that customer until somebody is made an administrator.
--
--   \c identity
--   SELECT m."UserId", m."Role"
--   FROM "Memberships" m
--   WHERE m."OrganizationId" = '00000000-0000-0000-0000-000000000000'  -- your org id
--     AND m."Status" = 0            -- Active
--     AND m."Role" IN (1, 2)        -- TenancyAdmin, TenancySuperAdmin
--   ORDER BY m."Role" DESC, m."UserId";
