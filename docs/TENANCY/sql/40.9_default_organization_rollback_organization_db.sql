-- Sellevate — Phase 40.9 rollback, step 2 of 2: remove the default organization from
-- organization-db.
--
-- DESTRUCTIVE. It contains DELETE and DROP TABLE. Run it AFTER the identity-db rollback, so the
-- registry row outlives the memberships that point at it rather than the other way round.
--
-- It refuses if the organization has grown a profile row (docs/CONTENT_MODEL.md) or if any other
-- organization exists that was created after the backfill: either means the platform has been
-- used since the migration, and deleting the registry row would strand real data in the other
-- service databases that reference it by bare uuid.
--
-- Invocation (scripts/tenancy-default-organization-backfill.sh --rollback does this for you):
--   psql -v ON_ERROR_STOP=1 \
--        -v organization_id="'00000000-0000-4000-8000-000000000001'" \
--        -d organization -f 40.9_default_organization_rollback_organization_db.sql

\set ON_ERROR_STOP on

BEGIN;

SET LOCAL sellevate.organization_id = :organization_id;

DO $$
DECLARE
    requested_organization_id uuid := current_setting('sellevate.organization_id')::uuid;
    recorded_organization_id  uuid;
    recorded_applied_at       timestamptz;
    organizations_created_later bigint;
    profile_rows bigint;
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

    SELECT count(*) INTO organizations_created_later
      FROM "Organizations"
     WHERE "Id" <> recorded_organization_id
       AND "CreatedAt" > recorded_applied_at;

    IF organizations_created_later > 0 THEN
        RAISE EXCEPTION
            '% organization(s) were created after the backfill. The platform has been used since; '
            'roll back by hand.', organizations_created_later;
    END IF;

    SELECT count(*) INTO profile_rows
      FROM "OrganizationProfiles" WHERE "OrganizationId" = recorded_organization_id;

    IF profile_rows > 0 THEN
        RAISE EXCEPTION
            'The default organization has a profile row — someone has configured it since the '
            'migration. Roll back by hand.';
    END IF;
END
$$;

DELETE FROM "Organizations" WHERE "Id" = :organization_id::uuid;

DROP TABLE tenancy_backfill_40_9;

DO $$
DECLARE
    requested_organization_id uuid := current_setting('sellevate.organization_id')::uuid;
BEGIN
    IF EXISTS (SELECT 1 FROM "Organizations" WHERE "Id" = requested_organization_id) THEN
        RAISE EXCEPTION 'The default organization row survived the rollback — aborting.';
    END IF;
END
$$;

COMMIT;

\echo 'organization-db: Phase 40.9 backfill rolled back.'
