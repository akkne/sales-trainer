-- Sellevate — Phase 40.9 rollback, step 1 of 2: undo the identity-db backfill.
--
-- DESTRUCTIVE. It contains DELETE and DROP TABLE. Per the SAFETY RULES in CLAUDE.md the statements
-- are written here and printed by the driver script before anything runs; no automated process
-- executes this file. Run the identity-db rollback BEFORE the organization-db one, mirroring the
-- forward order.
--
-- WHAT MAKES THIS SAFE TO RUN
--
-- The forward script recorded which organization it created and when
-- (tenancy_backfill_40_9), and which accounts it demoted
-- (tenancy_backfill_40_9_demoted_users). This file deletes only rows that match that record:
--
--   * memberships in the default organization that were created by the backfill run itself
--     (JoinedAt <= applied_at). If anyone has joined the organization since — an accepted invite,
--     a bootstrap admin — the rollback REFUSES rather than deleting a real membership.
--   * the auth configuration and the registry projection for that one organization.
--   * the platform role of the accounts the forward run demoted, restored exactly.
--
-- It deliberately does NOT touch "Users": the backfill created no users, so neither does the
-- rollback delete any. Offboarding is deactivation, never deletion (docs/TENANCY/TENANCY.md §4.3),
-- and that principle does not stop applying because a migration went wrong.
--
-- Invocation (scripts/tenancy-default-organization-backfill.sh --rollback does this for you):
--   psql -v ON_ERROR_STOP=1 \
--        -v organization_id="'00000000-0000-4000-8000-000000000001'" \
--        -d identity -f 40.9_default_organization_rollback_identity_db.sql

\set ON_ERROR_STOP on

BEGIN;

SET LOCAL sellevate.organization_id = :organization_id;

DO $$
DECLARE
    requested_organization_id uuid := current_setting('sellevate.organization_id')::uuid;
    recorded_organization_id  uuid;
    recorded_applied_at       timestamptz;
    memberships_joined_later  bigint;
BEGIN
    IF to_regclass('public.tenancy_backfill_40_9') IS NULL THEN
        RAISE EXCEPTION 'No Phase 40.9 backfill record in this database — nothing to roll back.';
    END IF;

    SELECT organization_id, applied_at
      INTO recorded_organization_id, recorded_applied_at
      FROM tenancy_backfill_40_9 WHERE id = 1;

    IF recorded_organization_id IS NULL THEN
        RAISE EXCEPTION 'No Phase 40.9 backfill record in this database — nothing to roll back.';
    END IF;

    IF recorded_organization_id <> requested_organization_id THEN
        RAISE EXCEPTION
            'This database was backfilled for organization %, not %. Refusing to roll back the wrong tenant.',
            recorded_organization_id, requested_organization_id;
    END IF;

    SELECT count(*) INTO memberships_joined_later
      FROM "Memberships"
     WHERE "OrganizationId" = recorded_organization_id
       AND "JoinedAt" > recorded_applied_at;

    IF memberships_joined_later > 0 THEN
        RAISE EXCEPTION
            '% membership(s) joined this organization after the backfill at %. Rolling back would '
            'delete real, post-migration data. Resolve them by hand first.',
            memberships_joined_later, recorded_applied_at;
    END IF;
END
$$;

DELETE FROM "Memberships"
 WHERE "OrganizationId" = :organization_id::uuid
   AND "JoinedAt" <= (SELECT applied_at FROM tenancy_backfill_40_9 WHERE id = 1);

UPDATE "Users" AS demoted_user
   SET "Role" = restore_source.previous_role
  FROM tenancy_backfill_40_9_demoted_users AS restore_source
 WHERE demoted_user."Id" = restore_source.user_id;

DELETE FROM "OrganizationAuthConfigurations" WHERE "OrganizationId" = :organization_id::uuid;

DELETE FROM "OrganizationReplicas" WHERE "OrganizationId" = :organization_id::uuid;

DROP TABLE tenancy_backfill_40_9_demoted_users;
DROP TABLE tenancy_backfill_40_9;

DO $$
DECLARE
    requested_organization_id uuid := current_setting('sellevate.organization_id')::uuid;
    surviving_memberships bigint;
BEGIN
    SELECT count(*) INTO surviving_memberships
      FROM "Memberships" WHERE "OrganizationId" = requested_organization_id;

    IF surviving_memberships > 0 THEN
        RAISE EXCEPTION '% membership(s) in the default organization survived the rollback — aborting.',
            surviving_memberships;
    END IF;
END
$$;

COMMIT;

\echo 'identity-db: Phase 40.9 backfill rolled back.'
