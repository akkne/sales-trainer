-- Sellevate — Phase 40.19: verify that the organization-profile replicas landed correctly.
--
-- READ-ONLY. Every statement is a SELECT or a DO block that only raises. It creates nothing, drops
-- nothing, updates nothing, and is safe to run against a production database with the services up.
-- NOT executed by any automated process (build, migration, CI, or agent run).
--
-- This block touches THREE databases and the file is meant to be run against each in turn:
--
--   psql -v ON_ERROR_STOP=1 -d organization -f 40.19_organization_profile_verify.sql
--   psql -v ON_ERROR_STOP=1 -d learning     -f 40.19_organization_profile_verify.sql
--   psql -v ON_ERROR_STOP=1 -d ai           -f 40.19_organization_profile_verify.sql
--
-- Every section starts with a to_regclass() guard, so the sections that do not apply to the database
-- you are pointed at skip themselves with a NOTICE instead of failing. That is why one file serves
-- all three: the alternative was three near-identical files whose checks would drift apart.
--
-- Run it AFTER learning-service and ai-service have each started once on the 40.19 code, so that
-- migrations 20260817215519_AddOrganizationProfileReplica (learning) and
-- 20260817215820_AddOrganizationProfileReplica (ai) have applied.
--
--
-- WHY THERE IS NO 40.19_..._indexes_concurrently.sql
--
-- Deliberate, and stated so nobody wonders whether it was forgotten. Blocks 40.10-40.13 each shipped
-- one because each rebuilt indexes on tables that were already large and already live. This block
-- creates two brand-new tables that hold at most one row per customer — tens of rows, not millions —
-- and the only query either of them ever serves is a lookup by the primary key. There is no second
-- access path to index, so a CONCURRENTLY script would have nothing to build.
--
--
-- WHY THERE IS NO BACKFILL, AND THEREFORE NO WINDOW OF INVISIBLE DATA
--
-- Same shape as 40.15, 40.17 and 40.18. Nothing here fills a column on an existing row: the two
-- replica tables start empty and stay empty until an organization saves its profile. An empty
-- replica is not a degraded state — it is exactly what an organization that has not filled the form
-- in has, and the renderer answers it with the neutral base wording the lessons were written with.
--
-- The one thing that IS owed to a human is not a backfill but a republish: a profile saved through
-- PUT /organizations/profile BEFORE this phase shipped was never published to Kafka, so its replicas
-- do not exist. Section 4 below finds exactly those rows. See docs/DONT_FORGET.md.


\echo ''
\echo '=== 40.19 organization profile — verification ==='
\echo ''


-- ─────────────────────────────────────────────────────────────────────────────
-- 1. organization-db: the source table exists and is tenant-scoped (from 40.5)
-- ─────────────────────────────────────────────────────────────────────────────

DO $$
BEGIN
    IF to_regclass('public."OrganizationProfiles"') IS NULL THEN
        RAISE NOTICE '1. skipped — no "OrganizationProfiles" here, this is not organization-db.';
        RETURN;
    END IF;

    RAISE NOTICE '1. "OrganizationProfiles" found; checks below apply.';
END $$;

-- 1a. Row-level security is on and forced. Forced matters: without it the table's owner — which is
--     what the migration role is — reads every tenant's profile, and "it works for me" is how an
--     RLS gap survives a review.
SELECT
    c.relname                                    AS table_name,
    c.relrowsecurity                             AS rls_enabled,
    c.relforcerowsecurity                        AS rls_forced
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND c.relname = 'OrganizationProfiles';

-- 1b. The policy must be plain equality, NOT the content flavour. A profile with a NULL owner would
--     be every organization's product name and every organization's banned claims at once. If the
--     qual below contains "IS NULL", something has replaced the policy with the content one and the
--     compliance guarantee is gone.
SELECT
    tablename,
    policyname,
    qual        AS using_clause,
    with_check  AS write_clause
FROM pg_policies
WHERE schemaname = 'public'
  AND tablename = 'OrganizationProfiles';


-- ─────────────────────────────────────────────────────────────────────────────
-- 2. learning-db / ai-db: the replica table exists, with the same guarantees
-- ─────────────────────────────────────────────────────────────────────────────

DO $$
BEGIN
    IF to_regclass('public."OrganizationProfileReplicas"') IS NULL THEN
        RAISE NOTICE '2. skipped — no "OrganizationProfileReplicas" here, this is not learning-db or ai-db.';
        RETURN;
    END IF;

    RAISE NOTICE '2. "OrganizationProfileReplicas" found; checks below apply.';
END $$;

-- 2a. RLS enabled and forced, exactly as at the source.
SELECT
    c.relname             AS table_name,
    c.relrowsecurity      AS rls_enabled,
    c.relforcerowsecurity AS rls_forced
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND c.relname = 'OrganizationProfileReplicas';

-- 2b. Same policy check as 1b, and it matters more here. In ai-db this is the FIRST table that is
--     not content: both neighbours (DialogBundles, DialogModes) legitimately use the
--     "IS NULL OR = current" flavour, so copying the neighbouring policy is the natural mistake.
--     Expected: qual carries the platform-staff branch (app.platform_mode) OR plain equality;
--     with_check carries plain equality ONLY, and neither clause contains "IS NULL".
SELECT
    tablename,
    policyname,
    qual        AS using_clause,
    with_check  AS write_clause
FROM pg_policies
WHERE schemaname = 'public'
  AND tablename = 'OrganizationProfileReplicas';

-- 2c. The primary key is the tenant column and nothing else. This is what makes "a row without an
--     owner" unrepresentable rather than merely forbidden, and what makes the projection naturally
--     idempotent: a redelivered Kafka message updates the row it already wrote.
SELECT
    i.indisprimary                                        AS is_primary_key,
    array_agg(a.attname ORDER BY a.attnum)                AS key_columns
FROM pg_index i
JOIN pg_class t     ON t.oid = i.indrelid
JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY (i.indkey)
WHERE t.relname = 'OrganizationProfileReplicas'
  AND i.indisprimary
GROUP BY i.indisprimary;


-- ─────────────────────────────────────────────────────────────────────────────
-- 3. learning-db: the seeder still owns nothing (the 40.19 seeder fix)
-- ─────────────────────────────────────────────────────────────────────────────
--
-- The bug this block fixed was silent: a re-run of the bundle import could overwrite an
-- organization's override lesson with the base text, because the seeder's reads admitted "global or
-- mine". There is no trace of that in any log, so the check has to be structural rather than
-- historical: count how the library is split. What you are looking for is that the base library
-- (organization_id IS NULL) is non-empty and that the number of organization-owned rows equals the
-- number of overrides somebody deliberately created — on a fresh deployment, zero.

DO $$
BEGIN
    IF to_regclass('public."Lessons"') IS NULL THEN
        RAISE NOTICE '3. skipped — no "Lessons" here, this is not learning-db.';
        RETURN;
    END IF;
END $$;

SELECT
    'Skills'   AS table_name,
    count(*) FILTER (WHERE "OrganizationId" IS NULL)     AS global_rows,
    count(*) FILTER (WHERE "OrganizationId" IS NOT NULL) AS organization_owned_rows
FROM "Skills"
UNION ALL
SELECT 'Topics',
    count(*) FILTER (WHERE "OrganizationId" IS NULL),
    count(*) FILTER (WHERE "OrganizationId" IS NOT NULL)
FROM "Topics"
UNION ALL
SELECT 'Lessons',
    count(*) FILTER (WHERE "OrganizationId" IS NULL),
    count(*) FILTER (WHERE "OrganizationId" IS NOT NULL)
FROM "Lessons"
UNION ALL
SELECT 'Exercises',
    count(*) FILTER (WHERE "OrganizationId" IS NULL),
    count(*) FILTER (WHERE "OrganizationId" IS NOT NULL)
FROM "Exercises";

-- 3b. Every organization-owned lesson must be an override of a base lesson, never an orphan. An
--     organization-owned lesson with no parent would mean something other than the copy-on-write
--     path created it — the seeder pointed at a customer being the case this block exists to make
--     impossible. Expected: zero rows.
SELECT
    "Id",
    "OrganizationId",
    "Title"
FROM "Lessons"
WHERE "OrganizationId" IS NOT NULL
  AND "ParentLessonId" IS NULL;


-- ─────────────────────────────────────────────────────────────────────────────
-- 4. Profiles saved before 40.19 shipped, which have never been published
-- ─────────────────────────────────────────────────────────────────────────────
--
-- Run against organization-db. Any row this returns needs a human to open the profile form and press
-- save once: the replica projections only learn about a profile when it is saved, and these were
-- saved before anything published. Nothing breaks in the meantime — an absent replica renders as the
-- neutral base wording — but the customer's substitutions and, more importantly, their banned_claims
-- are not in effect until they do.
--
-- There is no cross-database join available here, so this cannot be answered exactly from one
-- connection. What it CAN say is "these profiles exist"; compare the count with section 5's count in
-- each of the two replica databases.

DO $$
BEGIN
    IF to_regclass('public."OrganizationProfiles"') IS NULL THEN
        RAISE NOTICE '4. skipped — not organization-db.';
        RETURN;
    END IF;
END $$;

SELECT
    "OrganizationId",
    "UpdatedAt",
    ("Product" IS NOT NULL)                       AS has_product,
    ("Icp" IS NOT NULL)                           AS has_icp,
    jsonb_array_length("BannedClaimsJson"::jsonb) AS banned_claim_count
FROM "OrganizationProfiles"
ORDER BY "UpdatedAt";


-- ─────────────────────────────────────────────────────────────────────────────
-- 5. Replica freshness (run against learning-db and ai-db)
-- ─────────────────────────────────────────────────────────────────────────────
--
-- UpdatedAt here is the SOURCE row's timestamp, not the time the replica was written, so comparing
-- this list against section 4's tells you two different things at once: a profile missing from this
-- list has never been published (section 4's problem), and a profile whose UpdatedAt is older than
-- the source's has a projection that is behind — a stuck or dead-lettered consumer.

DO $$
BEGIN
    IF to_regclass('public."OrganizationProfileReplicas"') IS NULL THEN
        RAISE NOTICE '5. skipped — not learning-db or ai-db.';
        RETURN;
    END IF;
END $$;

SELECT
    "OrganizationId",
    "UpdatedAt"                                   AS source_updated_at,
    jsonb_array_length("BannedClaimsJson"::jsonb) AS banned_claim_count
FROM "OrganizationProfileReplicas"
ORDER BY "UpdatedAt";


\echo ''
\echo '=== done — nothing above modified anything ==='
\echo ''
