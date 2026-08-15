-- Sellevate — Phase 40.9, step 2 of 2: attach every existing user to the default organization.
--
-- NOT executed by any automated process (build, migration, CI, or agent run). A human runs it,
-- once, against a copy of production first and then against production, inside the maintenance
-- window described in docs/MICROSERVICES_PRODUCTION_MIGRATION.md ("Раскатка тенантов").
-- See docs/DONT_FORGET.md → "Миграция живых данных (блок 40.9)".
--
-- Run AFTER 40.9_default_organization_backfill_organization_db.sql, with the SAME
-- :organization_id.
--
-- Invocation (scripts/tenancy-default-organization-backfill.sh does this for you):
--   psql -v ON_ERROR_STOP=1 \
--        -v organization_id="'00000000-0000-4000-8000-000000000001'" \
--        -v organization_name="'Sellevate'" \
--        -v organization_slug="'default'" \
--        -d identity -f 40.9_default_organization_backfill_identity_db.sql
--
-- Idempotent: re-running changes nothing.
--
-- WHAT IT TOUCHES, AND WHAT IT HONESTLY DOES NOT
--
--   Memberships                    — one row per user that has none (this is the whole point)
--   OrganizationReplicas           — so token issuance knows the organization exists (40.9)
--   OrganizationAuthConfigurations — so POST /auth/login/start resolves a method (40.8)
--   Users."Role"                   — the removed global Admin value 1 becomes User (40.6)
--
--   Invites is the only ITenantScoped table that exists anywhere today, and its "OrganizationId"
--   has been NOT NULL since the table was created in 40.7 — there is nothing to backfill, and the
--   assertion at the end proves it rather than assuming it.
--
--   OutboxMessages."OrganizationId" is left alone on purpose. It is nullable because a
--   platform-global event legitimately has no tenant (docs/TENANCY/TENANCY.md §1.7), the rows are
--   transient (relayed, then deleted), and stamping an organization onto an event that was
--   produced without one would be inventing history.
--
--   Every OTHER service database (learning, ai, company, gamification, social, notification) has
--   no organization_id column yet. Adding it is Stage C, roadmap 40.10+ — see the template at the
--   bottom of this file, which is where those steps get appended.

\set ON_ERROR_STOP on

BEGIN;

SET LOCAL sellevate.organization_id = :organization_id;

CREATE TABLE IF NOT EXISTS tenancy_backfill_40_9 (
    id              integer PRIMARY KEY CHECK (id = 1),
    organization_id uuid        NOT NULL,
    applied_at      timestamptz NOT NULL
);

-- The role demotion below is the one step that destroys information (which accounts used to hold
-- the removed global Admin role). It is recorded here so the rollback can put it back exactly.
CREATE TABLE IF NOT EXISTS tenancy_backfill_40_9_demoted_users (
    user_id       uuid PRIMARY KEY,
    previous_role integer NOT NULL
);

DO $$
DECLARE
    recorded_organization_id uuid;
    requested_organization_id uuid := current_setting('sellevate.organization_id')::uuid;
BEGIN
    SELECT organization_id INTO recorded_organization_id FROM tenancy_backfill_40_9 WHERE id = 1;

    IF recorded_organization_id IS NOT NULL
       AND recorded_organization_id <> requested_organization_id THEN
        RAISE EXCEPTION
            'This database was already backfilled for organization %, not %. Roll back before re-running with a different id.',
            recorded_organization_id, requested_organization_id;
    END IF;
END
$$;

INSERT INTO tenancy_backfill_40_9 (id, organization_id, applied_at)
VALUES (1, :organization_id::uuid, now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO "OrganizationReplicas" ("OrganizationId", "Name", "Slug", "Status", "UpdatedAt")
VALUES (:organization_id::uuid, :organization_name, :organization_slug, 0, now())
ON CONFLICT ("OrganizationId") DO NOTHING;

INSERT INTO "OrganizationAuthConfigurations" (
    "OrganizationId",
    "Method",
    "ProviderSettings",
    "AllowedEmailDomains",
    "IsJustInTimeProvisioningEnabled",
    "SessionLifetime",
    "IsMultiFactorAuthenticationRequired",
    "CreatedAt"
)
VALUES (
    :organization_id::uuid,
    'password',
    NULL,
    -- Deliberately empty: claiming a domain here would route every address at that domain to this
    -- organization, which is wrong the moment a second customer shares a mail provider. Existing
    -- users reach it through their membership instead.
    ARRAY[]::text[],
    false,
    NULL,
    false,
    now()
)
ON CONFLICT ("OrganizationId") DO NOTHING;

-- Remember the legacy global admins before demoting them.
INSERT INTO tenancy_backfill_40_9_demoted_users (user_id, previous_role)
SELECT "Id", "Role" FROM "Users" WHERE "Role" = 1
ON CONFLICT (user_id) DO NOTHING;

-- One membership per user that has none.
--
-- Role mapping: a user who held the removed global Admin role (1), or who is a platform SuperAdmin
-- (2), becomes OrgAdmin (1) of the default organization; everyone else becomes Manager (0). The
-- platform role on "Users" is a separate axis and is not touched by this mapping — a SuperAdmin
-- stays a SuperAdmin.
INSERT INTO "Memberships" (
    "UserId", "OrganizationId", "Role", "Status", "InvitedBy", "JoinedAt", "DeactivatedAt"
)
SELECT
    existing_user."Id",
    :organization_id::uuid,
    CASE WHEN existing_user."Role" IN (1, 2) THEN 1 ELSE 0 END,
    0,
    NULL,
    now(),
    NULL
FROM "Users" AS existing_user
WHERE NOT EXISTS (
    SELECT 1 FROM "Memberships" AS existing_membership
    WHERE existing_membership."UserId" = existing_user."Id"
);

-- The global Admin role was removed in 40.6 and its value was left unassigned so a surviving row
-- would fail loudly rather than silently mean something else. This is where those rows are fixed.
UPDATE "Users" SET "Role" = 0 WHERE "Role" = 1;

DO $$
DECLARE
    requested_organization_id uuid := current_setting('sellevate.organization_id')::uuid;
    users_without_membership bigint;
    invites_without_organization bigint;
    surviving_legacy_admins bigint;
BEGIN
    SELECT count(*) INTO users_without_membership
    FROM "Users" AS existing_user
    WHERE NOT EXISTS (
        SELECT 1 FROM "Memberships" AS existing_membership
        WHERE existing_membership."UserId" = existing_user."Id"
    );

    IF users_without_membership > 0 THEN
        RAISE EXCEPTION '% user(s) still have no membership — aborting.', users_without_membership;
    END IF;

    SELECT count(*) INTO invites_without_organization
    FROM "Invites" WHERE "OrganizationId" IS NULL;

    IF invites_without_organization > 0 THEN
        RAISE EXCEPTION
            '% invite(s) have no organization, which the 40.7 schema says is impossible — aborting.',
            invites_without_organization;
    END IF;

    SELECT count(*) INTO surviving_legacy_admins FROM "Users" WHERE "Role" = 1;

    IF surviving_legacy_admins > 0 THEN
        RAISE EXCEPTION '% user(s) still hold the removed global Admin role — aborting.', surviving_legacy_admins;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM "OrganizationReplicas" WHERE "OrganizationId" = requested_organization_id) THEN
        RAISE EXCEPTION 'The organization replica row is missing after the insert — aborting.';
    END IF;
END
$$;

COMMIT;

\echo 'identity-db: every user now belongs to the default organization.'

-- ---------------------------------------------------------------------------------------------
-- STAGE C EXTENSION POINT (roadmap 40.10+) — nothing below runs today.
--
-- Every remaining service database gets the same treatment once its service grows an
-- organization_id column. The per-service checklist is fixed by docs/ROADMAP.md ("Этап C"):
-- column → backfill → EF filters → indexes with organization_id first → revisit UNIQUE
-- constraints → RLS → audit background jobs → isolation tests.
--
-- The backfill half of that checklist belongs here, as one more file per database, following the
-- shape above: bookkeeping row, idempotent UPDATE, assertion that nothing is left null. Sketch:
--
--   -- 40.9_default_organization_backfill_learning_db.sql
--   BEGIN;
--   SET LOCAL sellevate.organization_id = :organization_id;
--   CREATE TABLE IF NOT EXISTS tenancy_backfill_40_9 (...);          -- same shape
--   INSERT INTO tenancy_backfill_40_9 ... ON CONFLICT (id) DO NOTHING;
--
--   UPDATE "UserExerciseAttempts" SET "OrganizationId" = :organization_id::uuid
--    WHERE "OrganizationId" IS NULL;
--   -- ... one statement per tenant-scoped table in that database ...
--
--   -- Content tables (skills, topics, lessons, exercises, techniques, reference materials) are
--   -- the exception: NULL there means "global, shared by every organization" and must STAY null
--   -- (docs/TENANCY/TENANCY.md §1.2). Backfilling them would fork the shared curriculum into one
--   -- customer's private copy on the first migration.
--
--   DO $$ ... assert no tenant-data row is left with a NULL organization ... $$;
--   COMMIT;
--
-- Do not fold those into this file: each database is migrated in its own maintenance step, and a
-- single file spanning seven databases cannot be rolled back one service at a time.
-- ---------------------------------------------------------------------------------------------
