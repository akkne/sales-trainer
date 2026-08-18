-- Sellevate — Phase 40.12, step 3 of 3: rebuild company-db's indexes with "OrganizationId" first.
--
-- NOT executed by any automated process (build, migration, CI, or agent run), and deliberately NOT
-- part of EF migration 20260815203733_AddOrganizationId or of DatabaseBootstrapper. A transactional
-- index build takes an ACCESS EXCLUSIVE lock on the table, company-service runs Database.Migrate()
-- during startup, and a long build there stalls the readiness probe and races the replicas. So it
-- lives here, gets run by a human against a live database, and can be run with the service up.
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d company -f 40.12_company_organization_indexes_concurrently.sql
--
-- Run it AFTER 40.12_company_organization_backfill.sql. Building an index over rows that all share
-- the placeholder organization is not wrong, but it wastes the maintenance window.
--
-- WHY THE MIGRATION CREATES NO INDEXES AT ALL
--
--   The old indexes on the four child tables were ("CompanyId", <time>) — they doubled as the
--   index the foreign key to "Companies" needs for its cascade delete. Their replacements lead
--   with "OrganizationId", which does not serve that FK. So this file creates BOTH the new
--   organization-first indexes AND a plain ("CompanyId") index per child table before dropping
--   anything. Had the EF migration done the drop, deleting a company between deploy and this
--   script would have sequential-scanned four tables.
--
-- CONCURRENTLY, AND WHAT THAT COSTS
--
--   * CREATE INDEX CONCURRENTLY cannot run inside a transaction block. There is no BEGIN/COMMIT in
--     this file, on purpose — every statement is its own transaction. Do not wrap it.
--   * A concurrent build that fails (deadlock, cancelled session, conflicting long transaction)
--     leaves behind an INVALID index: never used by the planner, still maintained on every write.
--     The validity check below exists for exactly that, and it is not optional. The fix is
--     DROP INDEX CONCURRENTLY and re-run.
--   * The old index is dropped only AFTER its replacement is in place and valid, so no window
--     exists where neither serves queries.
--
-- Everything here is IF NOT EXISTS / IF EXISTS, so re-running is safe and a partially completed run
-- can simply be repeated.

\set ON_ERROR_STOP on

\echo '--- Companies: the double scope, and the per-organization follow-up poll ---'

-- (organization, user) is the access path of every request-driven read in company-service, and a
-- strict superset of the old IX_Companies_UserId.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Companies_OrganizationId_UserId"
    ON "Companies" ("OrganizationId", "UserId");

-- Sparse, and organization-first from 40.12 on: FollowUpReminderBackgroundService now runs the
-- due-follow-up query once per organization with that organization set, so the organization is the
-- first column every one of those queries filters on.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Companies_OrganizationId_NextActionAt"
    ON "Companies" ("OrganizationId", "NextActionAt")
    WHERE "NextActionAt" IS NOT NULL;

\echo '--- child tables: organization-first reads ---'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CallLogEntries_OrganizationId_CompanyId_OccurredAt"
    ON "CallLogEntries" ("OrganizationId", "CompanyId", "OccurredAt" DESC);
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CallLogEntries_OrganizationId_UserId"
    ON "CallLogEntries" ("OrganizationId", "UserId");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_PracticeCalls_OrganizationId_CompanyId_CreatedAt"
    ON "PracticeCalls" ("OrganizationId", "CompanyId", "CreatedAt" DESC);
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_PracticeCalls_OrganizationId_UserId"
    ON "PracticeCalls" ("OrganizationId", "UserId");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CompanyContacts_OrganizationId_CompanyId_CreatedAt"
    ON "CompanyContacts" ("OrganizationId", "CompanyId", "CreatedAt" DESC);
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CompanyContacts_OrganizationId_UserId"
    ON "CompanyContacts" ("OrganizationId", "UserId");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CompanyPersonas_OrganizationId_CompanyId_CreatedAt"
    ON "CompanyPersonas" ("OrganizationId", "CompanyId", "CreatedAt" DESC);
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CompanyPersonas_OrganizationId_UserId"
    ON "CompanyPersonas" ("OrganizationId", "UserId");

\echo '--- child tables: keep the FK to Companies indexed ---'

-- Not decoration. Dropping the ("CompanyId", <time>) indexes below leaves the FK to "Companies"
-- without a leading-column index, and deleting one company would then scan all four child tables.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CallLogEntries_CompanyId"
    ON "CallLogEntries" ("CompanyId");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_PracticeCalls_CompanyId"
    ON "PracticeCalls" ("CompanyId");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CompanyContacts_CompanyId"
    ON "CompanyContacts" ("CompanyId");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_CompanyPersonas_CompanyId"
    ON "CompanyPersonas" ("CompanyId");

\echo '--- validity check BEFORE dropping anything ---'

-- Nothing below this point may run if a build came out invalid: dropping the old index while its
-- replacement is unusable would take the table from "one good index" to "none".
DO $$
DECLARE
    invalid_index_names text;
BEGIN
    SELECT string_agg(pg_class.relname, ', ' ORDER BY pg_class.relname)
    INTO invalid_index_names
    FROM pg_index
    JOIN pg_class ON pg_class.oid = pg_index.indexrelid
    JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
    WHERE pg_namespace.nspname = current_schema()
      AND (NOT pg_index.indisvalid OR NOT pg_index.indisready)
      AND pg_class.relname LIKE 'IX_%';

    IF invalid_index_names IS NOT NULL THEN
        RAISE EXCEPTION
            'Invalid or not-ready index(es): %. A failed CREATE INDEX CONCURRENTLY leaves an index '
            'the planner never uses but every write still maintains. DROP INDEX CONCURRENTLY each '
            'of them and re-run this file. Nothing was dropped.',
            invalid_index_names;
    END IF;
END
$$;

\echo '--- dropping the superseded indexes ---'

DROP INDEX CONCURRENTLY IF EXISTS "IX_Companies_UserId";
DROP INDEX CONCURRENTLY IF EXISTS "IX_Companies_NextActionAt";
DROP INDEX CONCURRENTLY IF EXISTS "IX_CallLogEntries_CompanyId_OccurredAt";
DROP INDEX CONCURRENTLY IF EXISTS "IX_PracticeCalls_CompanyId_CreatedAt";
DROP INDEX CONCURRENTLY IF EXISTS "IX_CompanyContacts_CompanyId_CreatedAt";
DROP INDEX CONCURRENTLY IF EXISTS "IX_CompanyPersonas_CompanyId_CreatedAt";

\echo '--- final validity check ---'

DO $$
DECLARE
    invalid_index_names text;
    missing_index_names text;
BEGIN
    SELECT string_agg(pg_class.relname, ', ' ORDER BY pg_class.relname)
    INTO invalid_index_names
    FROM pg_index
    JOIN pg_class ON pg_class.oid = pg_index.indexrelid
    JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
    WHERE pg_namespace.nspname = current_schema()
      AND (NOT pg_index.indisvalid OR NOT pg_index.indisready);

    IF invalid_index_names IS NOT NULL THEN
        RAISE EXCEPTION 'Invalid or not-ready index(es) after the rebuild: %.', invalid_index_names;
    END IF;

    SELECT string_agg(expected.index_name, ', ' ORDER BY expected.index_name)
    INTO missing_index_names
    FROM (VALUES
        ('IX_Companies_OrganizationId_UserId'),
        ('IX_Companies_OrganizationId_NextActionAt'),
        ('IX_CallLogEntries_OrganizationId_CompanyId_OccurredAt'),
        ('IX_CallLogEntries_OrganizationId_UserId'),
        ('IX_CallLogEntries_CompanyId'),
        ('IX_PracticeCalls_OrganizationId_CompanyId_CreatedAt'),
        ('IX_PracticeCalls_OrganizationId_UserId'),
        ('IX_PracticeCalls_CompanyId'),
        ('IX_CompanyContacts_OrganizationId_CompanyId_CreatedAt'),
        ('IX_CompanyContacts_OrganizationId_UserId'),
        ('IX_CompanyContacts_CompanyId'),
        ('IX_CompanyPersonas_OrganizationId_CompanyId_CreatedAt'),
        ('IX_CompanyPersonas_OrganizationId_UserId'),
        ('IX_CompanyPersonas_CompanyId')
    ) AS expected(index_name)
    WHERE NOT EXISTS (
        SELECT 1 FROM pg_class
        JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
        WHERE pg_namespace.nspname = current_schema()
          AND pg_class.relkind = 'i'
          AND pg_class.relname = expected.index_name
    );

    IF missing_index_names IS NOT NULL THEN
        RAISE EXCEPTION 'Index(es) missing after the rebuild: %.', missing_index_names;
    END IF;
END
$$;

\echo 'company-db: every tenant index now leads with "OrganizationId", every FK stays indexed, and every index is valid.'
