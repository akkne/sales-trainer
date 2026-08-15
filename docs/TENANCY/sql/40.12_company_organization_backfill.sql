-- Sellevate — Phase 40.12, step 2 of 3: give every existing company-db row an organization.
--
-- NOT executed by any automated process (build, migration, CI, or agent run). A human runs it,
-- once, against a copy of production first and then against production, inside the maintenance
-- window described in docs/MICROSERVICES_PRODUCTION_MIGRATION.md ("Раскатка тенантов").
-- See docs/DONT_FORGET.md → "Раскатка organization_id в company-db (блок 40.12)".
--
-- ORDER MATTERS, AND GETTING IT WRONG IS VISIBLE TO USERS:
--
--   1. Deploy company-service on the new code with the service STOPPED, so its EF migration
--      20260815203733_AddOrganizationId runs. That migration adds the five columns and turns on
--      row-level security. It creates and drops no indexes at all — step 3 does that.
--   2. Run THIS file. Between step 1 and step 2 every pre-existing row carries the all-zeros
--      placeholder organization, and the RLS policy therefore hides it from everybody — fail-closed
--      working as designed (docs/TENANCY/TENANCY.md §1.5), but to a salesperson it looks exactly
--      like "my entire prospect list is gone". Do not leave that window open.
--   3. Run 40.12_company_organization_indexes_concurrently.sql. That one can be done later with the
--      service running: it is performance, not correctness.
--
-- Invocation (scripts/tenancy-company-organization-rollout.sh does this for you):
--   psql -v ON_ERROR_STOP=1 \
--        -v organization_id="'00000000-0000-4000-8000-000000000001'" \
--        -d company -f 40.12_company_organization_backfill.sql
--
-- Use the SAME :organization_id as the 40.9/40.10/40.11 scripts. company-db has no tenant registry
-- of its own to look it up in — that is organization-db's job — so the value is passed in, and the
-- assertions below check the shape of what was written rather than trusting it.
--
-- Idempotent: re-running changes nothing.
--
-- WHAT IT TOUCHES
--
--   Companies, CallLogEntries, PracticeCalls, CompanyContacts, CompanyPersonas — all five, all
--   placeholder -> the default organization.
--
-- Unlike learning-db and ai-db there is no "leave this one alone" list here: company-db holds no
-- global content library. Every row in it is one salesperson's own working data, which is exactly
-- why every table got the strict RLS flavour rather than the content one.
--
-- A NOTE ON THE OTHER HALF OF THE SCOPE
--
--   "UserId" is not touched and needs no backfill — it has always been populated. After this
--   script every row carries both halves of company-service's double scope: one organization AND
--   one user. The assertions below check the organization half; the user half is asserted too,
--   because a row with an empty user id would be visible to nobody once both filters apply, and
--   this is the last moment anyone looks at the table before RLS makes such a row invisible.

\set ON_ERROR_STOP on

BEGIN;

-- psql does not interpolate :variables inside dollar-quoted blocks, so the value is handed to the
-- DO block through a session GUC instead — the same trick 40.9 and 40.10 used.
SET LOCAL sellevate.organization_id = :organization_id;

-- The migration that ran in step 1 turned on FORCE ROW LEVEL SECURITY. Without BYPASSRLS the
-- placeholder rows this script exists to fix are invisible to it, the UPDATEs quietly touch zero
-- rows, and the assertions below then "pass" because they cannot see the rows either — a silent
-- no-op that looks like success. So: refuse to run unless the connected role can bypass RLS, and
-- turn row security off explicitly rather than relying on the role happening to be the owner.
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

CREATE TABLE IF NOT EXISTS tenancy_backfill_40_12 (
    id              integer PRIMARY KEY CHECK (id = 1),
    organization_id uuid        NOT NULL,
    applied_at      timestamptz NOT NULL
);

INSERT INTO tenancy_backfill_40_12 (id, organization_id, applied_at)
VALUES (1, :organization_id::uuid, now())
ON CONFLICT (id) DO NOTHING;

DO $$
DECLARE
    requested_organization_id uuid := current_setting('sellevate.organization_id')::uuid;
    recorded_organization_id  uuid;
    orphan_count              bigint;
    ownerless_count           bigint;
    table_name                text;
    tenant_tables             text[] := ARRAY[
        'Companies',
        'CallLogEntries',
        'PracticeCalls',
        'CompanyContacts',
        'CompanyPersonas'
    ];
BEGIN
    SELECT organization_id INTO recorded_organization_id FROM tenancy_backfill_40_12 WHERE id = 1;

    IF recorded_organization_id <> requested_organization_id THEN
        RAISE EXCEPTION
            'This database was already backfilled for organization %, refusing to re-point it at % — '
            'moving one customer''s CRM into another tenant is not something a script gets to decide.',
            recorded_organization_id, requested_organization_id;
    END IF;

    IF requested_organization_id = '00000000-0000-0000-0000-000000000000'::uuid THEN
        RAISE EXCEPTION 'The all-zeros guid is the placeholder this script exists to replace.';
    END IF;

    FOREACH table_name IN ARRAY tenant_tables
    LOOP
        EXECUTE format(
            'UPDATE %I SET "OrganizationId" = $1 WHERE "OrganizationId" = ''00000000-0000-0000-0000-000000000000''',
            table_name)
        USING requested_organization_id;
    END LOOP;

    -- Assert, do not assume: nothing may be left in the placeholder tenant, or it stays invisible.
    FOREACH table_name IN ARRAY tenant_tables
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

    -- The other half of the double scope. A row with no user is not a tenancy bug, but it is a row
    -- nobody will ever see again once both halves are enforced, and this is the last chance to
    -- notice it.
    FOREACH table_name IN ARRAY tenant_tables
    LOOP
        EXECUTE format(
            'SELECT count(*) FROM %I WHERE "UserId" = ''00000000-0000-0000-0000-000000000000''',
            table_name)
        INTO ownerless_count;

        IF ownerless_count > 0 THEN
            RAISE WARNING
                '% row(s) in % have an all-zeros "UserId". They now belong to organization % but to '
                'no user inside it, so no request will ever return them. Investigate before the '
                'maintenance window closes.',
                ownerless_count, table_name, requested_organization_id;
        END IF;
    END LOOP;

    -- Sub-resources must not end up in a different tenant from their parent company. The FK does
    -- not enforce this (it is on CompanyId alone), so it is asserted here instead.
    IF EXISTS (
        SELECT 1 FROM "CallLogEntries" child
        JOIN "Companies" parent ON parent."Id" = child."CompanyId"
        WHERE child."OrganizationId" <> parent."OrganizationId"
        UNION ALL
        SELECT 1 FROM "PracticeCalls" child
        JOIN "Companies" parent ON parent."Id" = child."CompanyId"
        WHERE child."OrganizationId" <> parent."OrganizationId"
        UNION ALL
        SELECT 1 FROM "CompanyContacts" child
        JOIN "Companies" parent ON parent."Id" = child."CompanyId"
        WHERE child."OrganizationId" <> parent."OrganizationId"
        UNION ALL
        SELECT 1 FROM "CompanyPersonas" child
        JOIN "Companies" parent ON parent."Id" = child."CompanyId"
        WHERE child."OrganizationId" <> parent."OrganizationId"
    ) THEN
        RAISE EXCEPTION
            'At least one sub-resource row belongs to a different organization than its parent '
            'company. That row is unreachable through the API and its parent''s cascade delete '
            'would cross a tenant boundary — aborting.';
    END IF;
END
$$;

COMMIT;

\echo 'company-db: every company, call log, practice call, contact and persona now belongs to the default organization.'
