-- Sellevate — Phase 40.13, step 2 of 3: give every existing gamification-db row an organization.
--
-- NOT executed by any automated process (build, migration, CI, or agent run). A human runs it,
-- once, against a copy of production first and then against production, inside the maintenance
-- window described in docs/MICROSERVICES_PRODUCTION_MIGRATION.md ("Раскатка тенантов").
-- See docs/DONT_FORGET.md → "Раскатка organization_id в gamification-db (блок 40.13)".
--
-- ORDER MATTERS:
--
--   1. Deploy gamification-service on the new code with the service STOPPED, so its EF migration
--      20260815213223_AddOrganizationId runs. That migration adds the seven columns, turns on
--      row-level security, and — unlike 40.10-40.12 — DOES swap four constraints, because those
--      four are load-bearing for correctness and cannot wait for step 3. See the migration's own
--      remarks.
--   2. Run THIS file. Between step 1 and step 2 every pre-existing row carries the all-zeros
--      placeholder organization and the RLS policy hides it from everybody. That is fail-closed
--      working as designed (docs/TENANCY/TENANCY.md §1.5). The user-visible cost here is smaller
--      than in learning-db or company-db — XP, streaks and leagues have no UI in this product —
--      but the streak-reset job would see zero streaks in that window, so do not leave it open.
--   3. Run 40.13_gamification_organization_indexes_concurrently.sql. That one can be done later
--      with the service running: it is performance, not correctness.
--
-- Invocation (scripts/tenancy-gamification-organization-rollout.sh does this for you):
--   psql -v ON_ERROR_STOP=1 \
--        -v organization_id="'00000000-0000-4000-8000-000000000001'" \
--        -d gamification -f 40.13_gamification_organization_backfill.sql
--
-- Use the SAME :organization_id as the 40.9-40.12 scripts. gamification-db has no tenant registry
-- of its own to look it up in — that is organization-db's job — so the value is passed in, and the
-- assertions below check what was written rather than trusting it.
--
-- Idempotent: re-running changes nothing.
--
-- WHAT IT TOUCHES
--
--   UserXpRecords, UserStreaks, UserLearningProgress, UserAchievements, LeagueSettings, Leagues,
--   LeagueMemberships — all seven, all placeholder -> the default organization.
--
-- WHAT IT DELIBERATELY DOES NOT TOUCH
--
--   Achievements and LeagueTiers (catalogues), GamificationSettings, StreakMilestones and
--   ExerciseTypeRewards (installation-wide configuration), UserReplicas (cross-organization
--   identities, TENANCY.md §4.2) and OutboxMessages (read only by the system-mode relay). None of
--   them has an "OrganizationId" column at all, which is what keeps "a row with no organization is
--   invisible, not shared" true for the seven that do.
--
-- LEAGUESETTINGS IS THE ONE TO LOOK AT TWICE
--
--   Before 40.13 there was exactly one LeagueSettings row for the whole installation, holding the
--   current league period. It now belongs to the default organization, which is correct for a
--   single-customer installation and is the only answer available: the row cannot be split, and
--   inventing a period for organizations that do not exist yet would be worse than letting them
--   start with the defaults GetSettingsAsync already returns. A second customer onboarded later
--   gets its own row the first time an admin saves league settings.

\set ON_ERROR_STOP on

BEGIN;

-- psql does not interpolate :variables inside dollar-quoted blocks, so the value is handed to the
-- DO block through a session GUC instead — the same trick 40.9-40.12 used.
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

CREATE TABLE IF NOT EXISTS tenancy_backfill_40_13 (
    id              integer PRIMARY KEY CHECK (id = 1),
    organization_id uuid        NOT NULL,
    applied_at      timestamptz NOT NULL
);

INSERT INTO tenancy_backfill_40_13 (id, organization_id, applied_at)
VALUES (1, :organization_id::uuid, now())
ON CONFLICT (id) DO NOTHING;

DO $$
DECLARE
    requested_organization_id uuid := current_setting('sellevate.organization_id')::uuid;
    recorded_organization_id  uuid;
    orphan_count              bigint;
    settings_count            bigint;
    table_name                text;
    tenant_tables             text[] := ARRAY[
        'UserXpRecords',
        'UserStreaks',
        'UserLearningProgress',
        'UserAchievements',
        'LeagueSettings',
        'Leagues',
        'LeagueMemberships'
    ];
BEGIN
    SELECT organization_id INTO recorded_organization_id FROM tenancy_backfill_40_13 WHERE id = 1;

    IF recorded_organization_id <> requested_organization_id THEN
        RAISE EXCEPTION
            'This database was already backfilled for organization %, refusing to re-point it at % — '
            'moving one customer''s progress and leagues into another tenant is not something a '
            'script gets to decide.',
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

    -- One league period per organization. The migration installed UNIQUE(OrganizationId) on
    -- LeagueSettings, so a pre-existing installation with more than one settings row would have
    -- failed the UPDATE above with a unique violation rather than reaching here — this check turns
    -- the surprising case (zero rows) into a readable message instead of a silent success.
    SELECT count(*) INTO settings_count FROM "LeagueSettings";
    IF settings_count = 0 THEN
        RAISE NOTICE
            'No LeagueSettings row exists. That is fine: organization % will start from the built-in '
            'defaults and get its own row the first time an admin saves league settings.',
            requested_organization_id;
    END IF;

    -- A membership must not end up in a different tenant from its league. The FK is on LeagueId
    -- alone and does not enforce this, so it is asserted here — a membership pointing across the
    -- boundary is a leaderboard row that shows one customer's user inside another's league.
    IF EXISTS (
        SELECT 1 FROM "LeagueMemberships" membership
        JOIN "Leagues" league ON league."Id" = membership."LeagueId"
        WHERE membership."OrganizationId" <> league."OrganizationId"
    ) THEN
        RAISE EXCEPTION
            'At least one league membership belongs to a different organization than its league. '
            'That is a cross-tenant leaderboard entry — aborting.';
    END IF;
END
$$;

COMMIT;

\echo 'gamification-db: every XP record, streak, achievement, learning counter, league, membership and league period now belongs to the default organization.'
