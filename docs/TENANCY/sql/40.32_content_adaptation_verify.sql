-- Sellevate — Phase 40.32: check batch tone adaptation and AI content review.
--
-- READ-ONLY. Every statement in this file is a SELECT. It creates nothing, drops nothing, updates
-- nothing, and is safe to run against a production database with the service up. NOT executed by
-- any automated process (build, migration, CI, or agent run).
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d learning -f 40.32_content_adaptation_verify.sql
--
--
-- WHAT 40.32 ADDED, IN ONE PARAGRAPH, SO THE QUERIES BELOW CAN BE READ
--
-- «Перепиши все упражнения этапа "закрытие" под наш продукт и тон» is a batch: one row in
-- `ContentAdaptationJobs` naming a mode and a `Skill.Stage`, and one row in
-- `ContentAdaptationItems` per exercise in that stage. A background worker
-- (`ContentAdaptationSweepService`, the ninth in docs/TENANCY/BACKGROUND_JOBS.md §2.1) answers the
-- items a few at a time, one LLM call per exercise, and writes a PROPOSAL onto the item. Nothing is
-- applied. An organization administrator then walks the queue and accepts or rejects each item by
-- id; accepting is the only thing in the block that writes an `Exercise`, and it happens inside
-- their HTTP request.
--
-- The same two tables serve the block's second half. `Mode = 'quality_review'` sends each exercise
-- to a reviewer instead of a rewriter, and the item comes back carrying `Findings` — a closed
-- vocabulary of seven codes (docs/SKILLS_AND_EXERCISES.md) — with nothing to apply. Accepting a
-- review item is refused by the service and by `CK_ContentAdaptationItems_Proposal`.
--
-- Three invariants the queries below actually check:
--
--   1. An accepted item carries the proposal it applied and the exercise it was applied to.
--      Nothing outside `accepted` carries an application. (CK_ContentAdaptationItems_Proposal.)
--   2. At most one live batch per (organization, mode, stage). (UX_ContentAdaptationJobs_Live.)
--   3. A batch's `Status` agrees with its items — it is a projection recomputed from them, and a
--      disagreement means a writer skipped `ContentAdaptationStatusCalculator`.
--
-- Two things this script CANNOT tell you, stated so nobody reads its silence as a verdict:
--
--   1. Whether a rewrite was any good. `ChangedFieldCount` says how many leaves moved and
--      `ChangeSummary` says what the model claims it did; neither is a judgement. That is what the
--      per-item accept exists for, and it is a person's.
--   2. Whether the review's findings are true. No prompt in this block has ever been run against a
--      real provider (docs/DONT_FORGET.md) — its calibration is unknown in both directions.


\echo '=== 1. Schema: tables, RLS, constraints ==='

SELECT c.relname                                        AS table_name,
       c.relrowsecurity                                 AS rls_enabled,
       c.relforcerowsecurity                            AS rls_forced,
       (SELECT count(*) FROM pg_policy p WHERE p.polrelid = c.oid) AS policy_count
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND c.relname IN ('ContentAdaptationJobs', 'ContentAdaptationItems')
ORDER BY c.relname;

-- Expect: both rows present, rls_enabled = t, rls_forced = t, policy_count >= 1.
-- A missing FORCE is the failure mode that matters: without it the table owner (which is the
-- migration role, and in a single-role deployment also the application role) bypasses the policy
-- entirely and one customer's proposals become readable by every other.

SELECT conrelid::regclass AS table_name,
       conname            AS constraint_name,
       contype            AS kind,
       pg_get_constraintdef(oid) AS definition
FROM pg_constraint
WHERE conrelid::regclass::text IN ('"ContentAdaptationJobs"', '"ContentAdaptationItems"',
                                   'ContentAdaptationJobs', 'ContentAdaptationItems')
ORDER BY conrelid::regclass::text, conname;

-- Expect, among others:
--   CK_ContentAdaptationJobs_Mode / _Status / _StageKey / _ItemCount / _Completed
--   CK_ContentAdaptationItems_Status / _Counters / _Proposal / _Resolution
--   UQ_ContentAdaptationJobs_Id_OrganizationId  (the composite key the item's FK points at)
--   FK_ContentAdaptationItems_Job_Organization  (an item belongs to its batch's organization)


\echo '=== 2. Batches: what exists, and where it is ==='

SELECT j."OrganizationId",
       j."Mode",
       j."StageKey",
       j."Status",
       j."ItemCount",
       count(i.*) FILTER (WHERE i."Status" = 'pending')   AS pending,
       count(i.*) FILTER (WHERE i."Status" = 'proposed')  AS awaiting_a_person,
       count(i.*) FILTER (WHERE i."Status" = 'unchanged') AS unchanged,
       count(i.*) FILTER (WHERE i."Status" = 'accepted')  AS accepted,
       count(i.*) FILTER (WHERE i."Status" = 'rejected')  AS rejected,
       count(i.*) FILTER (WHERE i."Status" = 'failed')    AS failed,
       j."CreatedAt",
       j."CompletedAt"
FROM "ContentAdaptationJobs" j
LEFT JOIN "ContentAdaptationItems" i ON i."JobId" = j."Id"
GROUP BY j."Id"
ORDER BY j."CreatedAt" DESC;

-- The `accepted` column is the only one that says anything about the product working. A batch whose
-- items are all `rejected` is a batch the model wasted, and a run of those against one organization
-- is the signal their profile is empty — the rewriter has nothing to rewrite towards.


\echo '=== 3. Invariant: an accepted item applied something, somewhere ==='

SELECT "Id", "JobId", "Status", "ProposedContent" IS NOT NULL AS has_proposal,
       "AppliedExerciseId", "AppliedAt", "ResolvedBy"
FROM "ContentAdaptationItems"
WHERE ("Status" = 'accepted' AND ("ProposedContent" IS NULL
                                  OR "AppliedAt" IS NULL
                                  OR "AppliedExerciseId" IS NULL))
   OR ("AppliedAt" IS NOT NULL AND "Status" <> 'accepted')
   OR (("Status" IN ('accepted', 'rejected')) <> ("ResolvedAt" IS NOT NULL));

-- Expect ZERO rows. Any row here means CK_ContentAdaptationItems_Proposal or _Resolution is missing
-- from this database — the constraints make these states unrepresentable, so a row means the
-- migration did not fully apply.


\echo '=== 4. Invariant: a proposal was applied to an exercise this organization owns ==='

SELECT i."Id"        AS item_id,
       i."OrganizationId",
       i."ExerciseId"        AS proposed_against,
       i."AppliedExerciseId" AS written_to,
       e."OrganizationId"    AS written_row_owner,
       l."ParentLessonId"    AS lesson_is_a_fork_of
FROM "ContentAdaptationItems" i
JOIN "Exercises" e ON e."Id" = i."AppliedExerciseId"
JOIN "Lessons"   l ON l."Id" = e."LessonId"
WHERE i."Status" = 'accepted'
  AND (e."OrganizationId" IS NULL OR e."OrganizationId" <> i."OrganizationId");

-- Expect ZERO rows, and this is the sharpest check in the file. A row whose `written_row_owner` is
-- NULL means an accepted rewrite was written into the GLOBAL LIBRARY — one customer's tone edit
-- applied to every other customer's curriculum. The accept path forks the lesson (40.18
-- copy-on-write) precisely to make that impossible; RLS cannot catch it, because the content policy
-- admits `OrganizationId IS NULL` in its WITH CHECK clause and the database cannot tell "global"
-- from "somebody else's" (docs/TENANCY/CONTENT_MODEL.md, ContentAuthoringGuard).

SELECT i."OrganizationId",
       count(*) FILTER (WHERE l."ParentLessonId" IS NOT NULL) AS applied_into_a_fork,
       count(*) FILTER (WHERE l."ParentLessonId" IS NULL)     AS applied_into_own_lesson
FROM "ContentAdaptationItems" i
JOIN "Exercises" e ON e."Id" = i."AppliedExerciseId"
JOIN "Lessons"   l ON l."Id" = e."LessonId"
WHERE i."Status" = 'accepted'
GROUP BY i."OrganizationId";

-- Informational. `applied_into_a_fork` counts the copy-on-write moments this block caused: each one
-- is a global lesson this organization now owns a copy of, and therefore one more row in the 40.18
-- staleness queue when the base moves.


\echo '=== 5. Invariant: the batch status agrees with its items ==='

WITH derived AS (
    SELECT j."Id",
           j."Status" AS stored,
           CASE
               WHEN count(i.*) = 0 THEN 'completed'
               WHEN count(i.*) FILTER (WHERE i."Status" = 'pending') > 0 THEN 'preparing'
               WHEN count(i.*) FILTER (WHERE i."Status" = 'proposed') > 0 THEN 'awaiting_review'
               WHEN count(i.*) FILTER (WHERE i."Status" <> 'failed') = 0 THEN 'failed'
               ELSE 'completed'
           END AS should_be
    FROM "ContentAdaptationJobs" j
    LEFT JOIN "ContentAdaptationItems" i ON i."JobId" = j."Id"
    GROUP BY j."Id"
)
SELECT * FROM derived WHERE stored <> should_be;

-- Expect ZERO rows while no worker tick is mid-flight. The status is a projection recomputed by
-- ContentAdaptationStatusCalculator inside every writing transaction; a lasting disagreement means
-- some writer changed an item without recomputing, which would show up as a batch stuck at
-- «готовим предложения…» that no worker will ever pick up again.


\echo '=== 6. Invariant: at most one live batch per stage per mode ==='

SELECT "OrganizationId", "Mode", "StageKey", count(*) AS live_batches
FROM "ContentAdaptationJobs"
WHERE "Status" IN ('preparing', 'awaiting_review')
GROUP BY 1, 2, 3
HAVING count(*) > 1;

-- Expect ZERO rows. UX_ContentAdaptationJobs_Live makes this unrepresentable; a row means the
-- partial unique index is missing, and its absence is a money bug rather than a tidiness bug — two
-- clicks a second apart would each buy a stage's worth of LLM calls.


\echo '=== 7. Stuck work: leases, attempts, and what a person is waiting on ==='

SELECT "Id", "Mode", "StageKey", "Status", "ClaimedAt",
       now() - "ClaimedAt" AS held_for,
       "FailureReason"
FROM "ContentAdaptationJobs"
WHERE "ClaimedAt" IS NOT NULL
ORDER BY "ClaimedAt";

-- A lease older than ContentAdaptation:ClaimLeaseMinutes (default 10) is not a bug — it is what the
-- lease is for, and the next tick will take the batch back. A lease that is ALWAYS held, across many
-- runs of this query, means either the worker is crashing before releasing it or the service is
-- running under a NOBYPASSRLS role and the enumeration returns nothing (docs/DONT_FORGET.md).

SELECT i."JobId", i."Id" AS item_id, i."ExerciseType", i."Attempts", i."FailureReason"
FROM "ContentAdaptationItems" i
WHERE i."Status" = 'failed'
ORDER BY i."JobId", i."OrderInLesson";

-- Per-item failure budget, not per-batch: one exercise the model chokes on cannot exhaust the
-- batch. `POST /admin/content/adaptations/{id}/retry` re-queues exactly these rows and nothing else.


\echo '=== 8. Review mode: which defects the library actually has ==='

SELECT i."OrganizationId",
       finding ->> 'code' AS finding_code,
       count(*)           AS occurrences
FROM "ContentAdaptationItems" i
JOIN "ContentAdaptationJobs" j ON j."Id" = i."JobId"
CROSS JOIN LATERAL jsonb_array_elements(i."Findings") AS finding
WHERE j."Mode" = 'quality_review'
  AND i."Findings" IS NOT NULL
GROUP BY 1, 2
ORDER BY 1, 3 DESC;

-- The whole reason the vocabulary is closed instead of prose: this query is answerable. A customer
-- whose library is full of `unmeasurable_criteria` needs a sentence in onboarding about what a
-- criterion is; one with any `banned_claim_rewarded` needs a phone call, because an exercise that
-- rewards a forbidden promise teaches a salesperson to make it.
