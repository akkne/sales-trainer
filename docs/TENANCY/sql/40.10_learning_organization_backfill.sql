-- Sellevate — Phase 40.10, step 2 of 3: give every existing learning-db progress row an owner.
--
-- NOT executed by any automated process (build, migration, CI, or agent run). A human runs it,
-- once, against a copy of production first and then against production, inside the maintenance
-- window described in docs/MICROSERVICES_PRODUCTION_MIGRATION.md ("Раскатка тенантов").
-- See docs/DONT_FORGET.md → "Раскатка organization_id в learning-db (блок 40.10)".
--
-- ORDER MATTERS, AND GETTING IT WRONG IS VISIBLE TO USERS:
--
--   1. Deploy learning-service on the new code with the service STOPPED, so its
--      EF migration 20260815152225_AddOrganizationId runs. That migration adds the columns and
--      turns on row-level security.
--   2. Run THIS file. Between step 1 and step 2 every pre-existing progress row carries the
--      all-zeros placeholder organization, and the RLS policy therefore hides it from everybody —
--      that is fail-closed working as designed (docs/TENANCY/TENANCY.md §1.5), but to a logged-in
--      user it looks exactly like "my progress was deleted". Do not leave that window open.
--   3. Run 40.10_learning_organization_indexes_concurrently.sql, which can be done later with the
--      service running — it is performance, not correctness.
--
-- Invocation (scripts/tenancy-learning-organization-backfill.sh does this for you):
--   psql -v ON_ERROR_STOP=1 \
--        -v organization_id="'00000000-0000-4000-8000-000000000001'" \
--        -d learning -f 40.10_learning_organization_backfill.sql
--
-- Use the SAME :organization_id as the 40.9 scripts. learning-db has no tenant registry of its
-- own to look it up in — that is organization-db's job — so the value is passed in, and the
-- assertions below check the shape of what was written rather than trusting it.
--
-- Idempotent: re-running changes nothing.
--
-- WHAT IT TOUCHES, AND WHAT IT DELIBERATELY DOES NOT
--
--   UserSkillProgressRecords     — tenant data, placeholder -> the default organization
--   UserLessonProgressRecords    — same
--   UserExerciseAttempts         — same
--   UserTechniqueProgress        — same
--
--   Skills, Topics, Lessons, Exercises, Techniques, ReferenceMaterials are NOT touched. Their
--   "OrganizationId" is NULL and must STAY null: NULL there means "global library, shared by every
--   organization" (docs/TENANCY/CONTENT_MODEL.md). Backfilling them would fork the shared
--   curriculum into one customer's private copy on the first migration, and every other tenant
--   would lose the entire skill tree.
--
--   ExerciseTypePrompts, SkillStages, DailyQuotes and UserReplicas have no organization column at
--   all and are outside RLS by design (roadmap 40.10).
--
--   OutboxMessages."OrganizationId" is left alone, for the same reason as in 40.9: the rows are
--   transient and stamping a tenant onto an event produced without one invents history.

\set ON_ERROR_STOP on

BEGIN;

-- psql does not interpolate :variables inside dollar-quoted blocks, so the value is handed to the
-- DO block through a session GUC instead — the same trick 40.9 used.
SET LOCAL sellevate.organization_id = :organization_id;

-- The migration that ran in step 1 turned on FORCE ROW LEVEL SECURITY. Without BYPASSRLS the
-- placeholder rows this script exists to fix are invisible to it, the UPDATEs quietly touch zero
-- rows, and the assertions below then "pass" because they cannot see the rows either — a silent
-- no-op that looks like success. So: refuse to run unless the connected role can bypass RLS, and
-- turn row security off explicitly rather than relying on the role happening to be the owner.
-- Same requirement as docs/TENANCY/sql/create_sellevate_app_role.sql spells out for migrations.
SET LOCAL row_security = off;

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

CREATE TABLE IF NOT EXISTS tenancy_backfill_40_10 (
    id              integer PRIMARY KEY CHECK (id = 1),
    organization_id uuid        NOT NULL,
    applied_at      timestamptz NOT NULL
);

INSERT INTO tenancy_backfill_40_10 (id, organization_id, applied_at)
VALUES (1, :organization_id::uuid, now())
ON CONFLICT (id) DO NOTHING;

DO $$
DECLARE
    requested_organization_id uuid := current_setting('sellevate.organization_id')::uuid;
    recorded_organization_id  uuid;
    orphan_count              bigint;
    content_rows_with_owner   bigint;
    table_name                text;
BEGIN
    SELECT organization_id INTO recorded_organization_id FROM tenancy_backfill_40_10 WHERE id = 1;

    IF recorded_organization_id <> requested_organization_id THEN
        RAISE EXCEPTION
            'This database was already backfilled for organization %, refusing to re-point it at % — '
            'moving progress between tenants is not something a migration script gets to decide.',
            recorded_organization_id, requested_organization_id;
    END IF;

    IF requested_organization_id = '00000000-0000-0000-0000-000000000000'::uuid THEN
        RAISE EXCEPTION 'The all-zeros guid is the placeholder this script exists to replace.';
    END IF;

    FOREACH table_name IN ARRAY ARRAY[
        'UserSkillProgressRecords',
        'UserLessonProgressRecords',
        'UserExerciseAttempts',
        'UserTechniqueProgress'
    ]
    LOOP
        EXECUTE format(
            'UPDATE %I SET "OrganizationId" = $1 WHERE "OrganizationId" = ''00000000-0000-0000-0000-000000000000''',
            table_name)
        USING requested_organization_id;
    END LOOP;

    -- Assert, do not assume: nothing may be left in the placeholder tenant, or it stays invisible.
    FOREACH table_name IN ARRAY ARRAY[
        'UserSkillProgressRecords',
        'UserLessonProgressRecords',
        'UserExerciseAttempts',
        'UserTechniqueProgress'
    ]
    LOOP
        EXECUTE format(
            'SELECT count(*) FROM %I WHERE "OrganizationId" = ''00000000-0000-0000-0000-000000000000''',
            table_name)
        INTO orphan_count;

        IF orphan_count > 0 THEN
            RAISE EXCEPTION '% row(s) in % still carry the placeholder organization — aborting.',
                orphan_count, table_name;
        END IF;
    END LOOP;

    -- And the mirror-image assertion: the global content library must still be global.
    SELECT
        (SELECT count(*) FROM "Skills"             WHERE "OrganizationId" IS NOT NULL)
      + (SELECT count(*) FROM "Topics"             WHERE "OrganizationId" IS NOT NULL)
      + (SELECT count(*) FROM "Lessons"            WHERE "OrganizationId" IS NOT NULL)
      + (SELECT count(*) FROM "Exercises"          WHERE "OrganizationId" IS NOT NULL)
      + (SELECT count(*) FROM "Techniques"         WHERE "OrganizationId" IS NOT NULL)
      + (SELECT count(*) FROM "ReferenceMaterials" WHERE "OrganizationId" IS NOT NULL)
    INTO content_rows_with_owner;

    IF content_rows_with_owner > 0 THEN
        RAISE WARNING
            '% content row(s) belong to a specific organization. That is legitimate from 40.18 on '
            '(copy-on-write overrides), but it is impossible in 40.10 — check that nothing '
            'backfilled the content tables by mistake.',
            content_rows_with_owner;
    END IF;
END
$$;

COMMIT;

\echo 'learning-db: every progress row now belongs to the default organization; content stays global.'
