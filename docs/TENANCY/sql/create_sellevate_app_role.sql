-- Sellevate — application role for tenant-scoped Postgres connections (Phase 40.4).
--
-- NOT executed by any automated process (build, migration, CI, or agent run). A human runs the
-- relevant sections by hand, once per real Postgres server/cluster and once per service database
-- that has RLS-protected tables. See docs/DONT_FORGET.md ("Отложено человеку" → "База данных и
-- RLS") and docs/TESTING/TENANCY.md.
--
-- Contract this role must satisfy (docs/TENANCY/TENANCY.md §1.5):
--   * NOT the owner of any tenant-scoped table — EF migrations keep running as whatever role
--     already owns the schema today, never as this one.
--   * NOSUPERUSER, NOBYPASSRLS — this is exactly the role whose queries must be filtered by RLS.
--   * The connection string the running services use (ConnectionStrings__Postgres) switches to
--     this role; the migration/admin connection string used by DatabaseBootstrapper does not.
--
-- IMPORTANT — the trap this script exists to avoid: Postgres only exempts a table's OWNER from a
-- FORCE ROW LEVEL SECURITY policy automatically when that owner is a superuser. A non-superuser
-- owner that is not also granted BYPASSRLS becomes subject to its own table's RLS policy the
-- moment FORCE is turned on — which would break migrations, not just the app. Before enabling RLS
-- on any table in a database, confirm the role EF migrations connect as is either a Postgres
-- superuser, or has been granted BYPASSRLS explicitly (see the ALTER ROLE line below). Local dev
-- (`scripts/dev-infra.sh`, user `st`) already satisfies this — `st` is the Postgres image's
-- initial superuser — but a real server's migration role may not be, and this is easy to miss
-- because everything keeps working right up until the first EnableTenantRls(...) migration runs.

-- 1) Create the role once per Postgres cluster (roles are cluster-wide, not per-database).
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'sellevate_app') THEN
        CREATE ROLE sellevate_app
            WITH LOGIN
            NOSUPERUSER
            NOCREATEDB
            NOCREATEROLE
            NOBYPASSRLS
            PASSWORD 'REPLACE_ME_BEFORE_RUNNING';
    END IF;
END
$$;

-- 2) Repeat the block below for every service database that has RLS-protected tables. This is a
--    Stage C (40.10+) concern — do not run it against a database with no EnableTenantRls(...)
--    migrations yet, there is nothing for the role to be granted on.
--
--    \c <database_name>
--
--    GRANT CONNECT ON DATABASE <database_name> TO sellevate_app;
--    GRANT USAGE ON SCHEMA public TO sellevate_app;
--    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO sellevate_app;
--    ALTER DEFAULT PRIVILEGES IN SCHEMA public
--        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO sellevate_app;
--
--    -- Only if the migration/admin role for this database is not already a superuser:
--    -- ALTER ROLE <migration_admin_role> WITH BYPASSRLS;

-- 3) Point the *application's* ConnectionStrings__Postgres at sellevate_app for that database.
--    The migration/admin connection string (DatabaseBootstrapper, `dotnet ef database update`)
--    keeps using the existing owning/admin role — never sellevate_app, which owns nothing and
--    would fail to create tables or run DDL.
