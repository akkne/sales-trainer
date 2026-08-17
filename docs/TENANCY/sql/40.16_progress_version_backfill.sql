-- Sellevate — Phase 40.16, step 2 of 3: bind existing progress to a lesson version.
--
-- NOT executed by any automated process (build, migration, CI, or agent run). A human runs it,
-- once, against a copy of production first and then against production.
-- See docs/DONT_FORGET.md → "Раскатка 40.16 — привязка прогресса к версии".
--
-- ORDER MATTERS:
--
--   1. Deploy learning-service on the new code and let it START ONCE. Two things happen at startup
--      and both are prerequisites for this file:
--        a) EF migration 20260817195247_AddProgressLessonVersionBinding adds the two nullable
--           "LessonVersionId" columns (a catalogue-only change on Postgres 11+, so no rewrite and
--           no long lock, which is why it is allowed to run inside Database.Migrate());
--        b) LessonVersionBackfill mints a published "version 1" for every lesson that has never
--           been published — which is every lesson that existed before 40.15, because that phase
--           deliberately created no versions. THIS FILE HAS NOTHING TO BIND TO UNTIL THAT HAS RUN.
--           It is C# and not SQL for one reason: LessonVersion."ContentHash" is a SHA-256 over the
--           exact bytes LessonSnapshotSerializer emits, with object keys in ordinal order, and
--           Postgres orders jsonb keys by length and then bytes. A snapshot built here would carry
--           a hash the service never reproduces, and the next publish would mint a duplicate
--           version — defeating the one thing content_hash exists for.
--   2. Run THIS file.
--   3. Run 40.16_progress_version_indexes_concurrently.sql — performance, not correctness, and it
--      can wait for a convenient moment with the service running.
--
-- THERE IS NO WINDOW IN WHICH ANYTHING IS INVISIBLE, and that is the difference from 40.10-40.13.
--
--   Those backfills filled a column the row-level-security policy filters on, so between the deploy
--   and the script every existing row was hidden and users saw "my data was deleted". Nothing
--   filters on "LessonVersionId". Until this file runs, historical attempts simply report in the
--   "unversionedAttempts" bucket of GET /admin/lessons/{id}/accuracy instead of inside a version
--   segment. So this is not a maintenance pairing: step 1 and step 2 do not have to share a window.
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d learning -f 40.16_progress_version_backfill.sql
--
-- No :organization_id parameter, unlike 40.9-40.13. This script does not decide who owns anything —
-- every row it touches already has an owner, and the version it binds to is derived from the
-- lesson the row already points at. There is nothing for a human to get wrong here.
--
-- Idempotent: every UPDATE is guarded by "LessonVersionId" IS NULL, so re-running changes nothing.
-- Interrupting it mid-way is safe; run it again.
--
-- WHAT IT TOUCHES
--
--   "UserExerciseAttempts"."LessonVersionId"        NULL -> the lesson's earliest published version
--   "UserLessonProgressRecords"."LessonVersionId"   NULL -> the lesson's earliest published version
--
-- WHY "THE EARLIEST PUBLISHED VERSION" AND NOT "THE LATEST"
--
--   The roadmap calls it "version 1", and that is exactly right: these attempts were taken against
--   content as it stood before anybody could publish anything, and version 1 is the snapshot of
--   that content. Binding them to the latest version would claim they were answered against
--   whatever an administrator published last week — the retroactive re-interpretation this whole
--   phase exists to stop, performed by the fix itself.
--
-- WHAT IT CANNOT BIND, AND WHY THAT IS REPORTED RATHER THAN GUESSED
--
--   An attempt whose "ExerciseId" no longer matches any row in "Exercises" (the exercise was
--   deleted) has nothing to resolve a lesson through, so it stays NULL. It is counted and printed
--   at the end, not silently attached to a plausible lesson.

\set ON_ERROR_STOP on

-- The 40.10 migration turned on FORCE ROW LEVEL SECURITY for "UserExerciseAttempts" and
-- "UserLessonProgressRecords". Without BYPASSRLS this script would see zero rows, update zero rows,
-- and its final assertions would "pass" because they cannot see the rows either — a silent no-op
-- that looks like success. Refuse, loudly, and turn row security off explicitly rather than relying
-- on the connected role happening to be the table owner.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_roles
        WHERE rolname = current_user AND (rolsuper OR rolbypassrls)
    ) THEN
        RAISE EXCEPTION
            'Role % can neither bypass nor disable row-level security, so this backfill would '
            'silently update nothing. Connect as the migration/owner role instead.', current_user;
    END IF;
END
$$;

SET row_security = off;

-- Step 1 must have happened. Checking is cheap; discovering it afterwards is not.
DO $$
DECLARE
    lessons_without_version bigint;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'UserExerciseAttempts' AND column_name = 'LessonVersionId'
    ) THEN
        RAISE EXCEPTION
            'Column "UserExerciseAttempts"."LessonVersionId" does not exist. Deploy learning-service '
            'on the 40.16 code and let it start once so its EF migration runs, then re-run this file.';
    END IF;

    SELECT count(*) INTO lessons_without_version
    FROM "Lessons" lesson
    WHERE NOT EXISTS (
        SELECT 1 FROM "LessonVersions" version
        WHERE version."LessonId" = lesson."Id" AND version."Status" = 'published');

    IF lessons_without_version > 0 THEN
        RAISE EXCEPTION
            '% lesson(s) still have no published version, so their attempts have nothing to bind to. '
            'Either LessonVersionBackfill has not run yet or it logged errors (check the service log '
            'for "Lesson version backfill"), or these are organization-owned lessons: the startup '
            'backfill runs in system mode and deliberately sees only the global library, so an '
            'organization''s own lesson gets its first version from its own admin or its own '
            'learners. Publish those, then re-run this file.', lessons_without_version;
    END IF;
END
$$;

-- The lesson -> version-1 mapping, materialised once. Both UPDATEs below read it, and computing it
-- twice against "LessonVersions" would be the same work done twice.
DROP TABLE IF EXISTS pg_temp.tenancy_40_16_initial_version;

CREATE TEMP TABLE tenancy_40_16_initial_version AS
SELECT DISTINCT ON (version."LessonId")
       version."LessonId"        AS lesson_id,
       version."Id"              AS version_id,
       version."OrganizationId"  AS organization_id
FROM "LessonVersions" version
WHERE version."Status" = 'published'
ORDER BY version."LessonId", version."VersionNumber";

CREATE UNIQUE INDEX ON tenancy_40_16_initial_version (lesson_id);

-- Batched, and outside any explicit transaction block, because these two tables grow with every
-- answered exercise: one statement over millions of rows holds a snapshot open for its whole
-- duration, bloats the table with dead tuples, and cannot be interrupted without losing all of it.
-- COMMIT inside a DO block requires Postgres 11+ and requires that no BEGIN wraps this file — so do
-- not add one.
DO $$
DECLARE
    batch_size            constant integer := 20000;
    updated_in_batch      integer;
    total_attempts        bigint := 0;
    total_progress_rows   bigint := 0;
BEGIN
    LOOP
        WITH batch AS (
            SELECT attempt."Id" AS attempt_id, initial.version_id
            FROM "UserExerciseAttempts" attempt
            JOIN "Exercises" exercise ON exercise."Id" = attempt."ExerciseId"
            JOIN tenancy_40_16_initial_version initial ON initial.lesson_id = exercise."LessonId"
            WHERE attempt."LessonVersionId" IS NULL
            LIMIT batch_size
        )
        UPDATE "UserExerciseAttempts" attempt
        SET "LessonVersionId" = batch.version_id
        FROM batch
        WHERE attempt."Id" = batch.attempt_id;

        GET DIAGNOSTICS updated_in_batch = ROW_COUNT;
        total_attempts := total_attempts + updated_in_batch;
        COMMIT;

        EXIT WHEN updated_in_batch = 0;
    END LOOP;

    LOOP
        WITH batch AS (
            SELECT progress."Id" AS progress_id, initial.version_id
            FROM "UserLessonProgressRecords" progress
            JOIN tenancy_40_16_initial_version initial ON initial.lesson_id = progress."LessonId"
            WHERE progress."LessonVersionId" IS NULL
            LIMIT batch_size
        )
        UPDATE "UserLessonProgressRecords" progress
        SET "LessonVersionId" = batch.version_id
        FROM batch
        WHERE progress."Id" = batch.progress_id;

        GET DIAGNOSTICS updated_in_batch = ROW_COUNT;
        total_progress_rows := total_progress_rows + updated_in_batch;
        COMMIT;

        EXIT WHEN updated_in_batch = 0;
    END LOOP;

    RAISE NOTICE 'Bound % attempt(s) and % lesson-progress row(s) to their lesson''s first version.',
        total_attempts, total_progress_rows;
END
$$;

-- Assert, do not assume.
DO $$
DECLARE
    crossing_rows          bigint;
    orphan_attempts        bigint;
    unbound_attempts       bigint;
    unbound_progress_rows  bigint;
BEGIN
    -- A version is either global (NULL — the shared library) or one organization's own. An attempt
    -- may never point at another organization's snapshot: that would put one customer's content
    -- inside another customer's history.
    SELECT count(*) INTO crossing_rows
    FROM "UserExerciseAttempts" attempt
    JOIN "LessonVersions" version ON version."Id" = attempt."LessonVersionId"
    WHERE version."OrganizationId" IS NOT NULL
      AND version."OrganizationId" <> attempt."OrganizationId";

    IF crossing_rows > 0 THEN
        RAISE EXCEPTION
            '% attempt(s) now point at a lesson version owned by a different organization. '
            'That is a cross-tenant reference — aborting so it can be investigated.', crossing_rows;
    END IF;

    SELECT count(*) INTO crossing_rows
    FROM "UserLessonProgressRecords" progress
    JOIN "LessonVersions" version ON version."Id" = progress."LessonVersionId"
    WHERE version."OrganizationId" IS NOT NULL
      AND version."OrganizationId" <> progress."OrganizationId";

    IF crossing_rows > 0 THEN
        RAISE EXCEPTION
            '% lesson-progress row(s) now point at a lesson version owned by a different '
            'organization — aborting.', crossing_rows;
    END IF;

    -- Anything still unbound must be explainable, so count the one explanation that is acceptable.
    SELECT count(*) INTO orphan_attempts
    FROM "UserExerciseAttempts" attempt
    WHERE attempt."LessonVersionId" IS NULL
      AND NOT EXISTS (SELECT 1 FROM "Exercises" exercise WHERE exercise."Id" = attempt."ExerciseId");

    SELECT count(*) INTO unbound_attempts
    FROM "UserExerciseAttempts" WHERE "LessonVersionId" IS NULL;

    SELECT count(*) INTO unbound_progress_rows
    FROM "UserLessonProgressRecords" WHERE "LessonVersionId" IS NULL;

    IF unbound_attempts <> orphan_attempts THEN
        RAISE EXCEPTION
            '% attempt(s) are still unbound but only % of them point at a deleted exercise. The rest '
            'have a live exercise whose lesson has a published version, which means this script did '
            'not finish — re-run it.', unbound_attempts, orphan_attempts;
    END IF;

    IF orphan_attempts > 0 THEN
        RAISE NOTICE
            '% attempt(s) stay unversioned: their exercise row no longer exists, so there is no '
            'lesson to resolve a version through. They are reported in the "unversionedAttempts" '
            'bucket of GET /admin/lessons/{id}/accuracy and are deliberately not guessed at.',
            orphan_attempts;
    END IF;

    IF unbound_progress_rows > 0 THEN
        RAISE EXCEPTION
            '% lesson-progress row(s) are still unbound. Every one of them points at a lesson row '
            'by foreign key and every lesson has a published version, so this cannot happen unless '
            'the script did not finish — re-run it.', unbound_progress_rows;
    END IF;
END
$$;

DROP TABLE IF EXISTS pg_temp.tenancy_40_16_initial_version;

\echo 'learning-db: historical attempts and lesson progress are bound to their lesson''s first version. Editing a lesson from now on creates a new version and leaves these numbers alone.'
