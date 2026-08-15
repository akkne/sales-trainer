-- Sellevate — Phase 40.11: rebuild ai-db's dialog-library indexes with "OrganizationId" first.
--
-- NOT executed by any automated process (build, migration, CI, or agent run), and deliberately NOT
-- part of EF migration 20260815154837_AddOrganizationId or of DatabaseBootstrapper — same reason as
-- 40.10: a transactional index build takes an ACCESS EXCLUSIVE lock, ai-service runs
-- Database.Migrate() during startup, and a long build there stalls the readiness probe.
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d ai -f 40.11_ai_organization_indexes_concurrently.sql
--
-- Run it AFTER migration 20260815154837_AddOrganizationId has been applied. No backfill precedes
-- it: every pre-existing bundle and mode is global content, and NULL is already the right value
-- for all of them. The data that does need migrating lives in Mongo —
-- docs/TENANCY/mongo/40.11_dialog_sessions_organization_backfill.js.
--
-- CONCURRENTLY, AND WHAT THAT COSTS
--
--   * CREATE INDEX CONCURRENTLY cannot run inside a transaction block. There is no BEGIN/COMMIT in
--     this file, on purpose — every statement is its own transaction. Do not wrap it.
--   * A concurrent build that fails leaves behind an INVALID index: never used by the planner, but
--     still maintained on every write. The validity check at the bottom exists for exactly that
--     and is not optional. The fix is DROP INDEX CONCURRENTLY and re-run.
--   * The old unique index is dropped only AFTER both replacements are in place and valid, so no
--     window exists where the mode key is unconstrained.
--
-- Everything here is IF NOT EXISTS / IF EXISTS, so re-running is safe.

\set ON_ERROR_STOP on

\echo '--- DialogModes: per-organization uniqueness of the mode key ---'

-- Two indexes, not one. In a composite unique index Postgres treats NULLs as distinct, so
-- ("OrganizationId", "BundleId", "Key") does NOT stop two global modes sharing a key. The partial
-- index over the global rows preserves the guarantee the old index gave the shared library, while
-- the composite one lets each organization define its own "discovery-call".
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "IX_DialogModes_OrganizationId_BundleId_Key"
    ON "DialogModes" ("OrganizationId", "BundleId", "Key")
    WHERE "OrganizationId" IS NOT NULL;

CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "IX_DialogModes_BundleId_Key_Global"
    ON "DialogModes" ("BundleId", "Key")
    WHERE "OrganizationId" IS NULL;

\echo '--- DialogBundles: the practice list is read per organization ---'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DialogBundles_OrganizationId_SortOrder"
    ON "DialogBundles" ("OrganizationId", "SortOrder");

\echo '--- verifying the new indexes are VALID before dropping the superseded one ---'

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
      AND NOT pg_index.indisvalid
      AND pg_class.relname IN (
          'IX_DialogModes_OrganizationId_BundleId_Key',
          'IX_DialogModes_BundleId_Key_Global',
          'IX_DialogBundles_OrganizationId_SortOrder'
      );

    IF invalid_index_names IS NOT NULL THEN
        RAISE EXCEPTION
            'Concurrent build left invalid index(es): %. DROP INDEX CONCURRENTLY them and re-run; do NOT drop the old index.',
            invalid_index_names;
    END IF;
END
$$;

\echo '--- dropping the superseded installation-wide unique index ---'

DROP INDEX CONCURRENTLY IF EXISTS "IX_DialogModes_BundleId_Key";

\echo '--- final check: every expected index exists and is valid ---'

DO $$
DECLARE
    missing_index_names text;
    invalid_index_names text;
BEGIN
    SELECT string_agg(expected.index_name, ', ' ORDER BY expected.index_name)
    INTO missing_index_names
    FROM (VALUES
        ('IX_DialogModes_OrganizationId_BundleId_Key'),
        ('IX_DialogModes_BundleId_Key_Global'),
        ('IX_DialogBundles_OrganizationId_SortOrder')
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

    SELECT string_agg(pg_class.relname, ', ' ORDER BY pg_class.relname)
    INTO invalid_index_names
    FROM pg_index
    JOIN pg_class ON pg_class.oid = pg_index.indexrelid
    JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace
    WHERE pg_namespace.nspname = current_schema()
      AND NOT pg_index.indisvalid
      AND pg_class.relname LIKE 'IX_Dialog%';

    IF invalid_index_names IS NOT NULL THEN
        RAISE EXCEPTION 'Invalid index(es) remain: %.', invalid_index_names;
    END IF;
END
$$;

\echo 'ai-db: the dialog library is indexed per organization and every index is valid.'
