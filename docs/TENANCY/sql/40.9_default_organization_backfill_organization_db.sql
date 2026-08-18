-- Sellevate — Phase 40.9, step 1 of 2: create the default organization in organization-db.
--
-- NOT executed by any automated process (build, migration, CI, or agent run). A human runs it,
-- once, against a copy of production first and then against production, inside the maintenance
-- window described in docs/MICROSERVICES_PRODUCTION_MIGRATION.md ("Раскатка тенантов").
-- See docs/DONT_FORGET.md → "Миграция живых данных (блок 40.9)".
--
-- Run order matters: this file first, then
-- 40.9_default_organization_backfill_identity_db.sql against identity-db with the SAME
-- :organization_id. The two databases have no foreign key between them (DB-per-service,
-- docs/TENANCY/TENANCY.md §1.1) — the shared uuid IS the link, and nothing will complain if the
-- two runs disagree, which is why the driver script passes one value to both.
--
-- Invocation (scripts/tenancy-default-organization-backfill.sh does this for you):
--   psql -v ON_ERROR_STOP=1 \
--        -v organization_id="'00000000-0000-4000-8000-000000000001'" \
--        -v organization_name="'Sellevate'" \
--        -v organization_slug="'default'" \
--        -d organization -f 40.9_default_organization_backfill_organization_db.sql
--
-- Idempotent: re-running changes nothing. Non-destructive: it contains no DELETE, no UPDATE of an
-- existing row and no DROP.

\set ON_ERROR_STOP on

BEGIN;

-- psql does not interpolate :variables inside dollar-quoted bodies, so the value is parked in a
-- session GUC that the DO blocks below read back with current_setting().
SET LOCAL sellevate.organization_id = :organization_id;

-- Bookkeeping for the rollback. It records which organization this run created and when, which is
-- what lets the rollback delete exactly what was added and refuse if anything newer has appeared
-- since. Deliberately a plain lower-case table: it is not part of the EF model and must never be
-- picked up by a future scaffold.
CREATE TABLE IF NOT EXISTS tenancy_backfill_40_9 (
    id              integer PRIMARY KEY CHECK (id = 1),
    organization_id uuid        NOT NULL,
    applied_at      timestamptz NOT NULL
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

-- "Status" is stored as text in organization-db (HasConversion<string>) and as an int in
-- identity-db's OrganizationReplicas projection. The two are not interchangeable; each file uses
-- its own representation.
INSERT INTO "Organizations" ("Id", "Name", "Slug", "Status", "CreatedAt", "UpdatedAt")
VALUES (
    :organization_id::uuid,
    :organization_name,
    :organization_slug,
    'Active',
    now(),
    now()
)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO tenancy_backfill_40_9 (id, organization_id, applied_at)
VALUES (1, :organization_id::uuid, now())
ON CONFLICT (id) DO NOTHING;

DO $$
DECLARE
    requested_organization_id uuid := current_setting('sellevate.organization_id')::uuid;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "Organizations" WHERE "Id" = requested_organization_id) THEN
        RAISE EXCEPTION 'The default organization row is missing after the insert — aborting.';
    END IF;
END
$$;

COMMIT;

\echo 'organization-db: default organization present.'
