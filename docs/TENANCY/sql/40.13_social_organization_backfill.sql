-- Sellevate — Phase 40.13, step 2 of 4: give every existing social-db row an organization.
--
-- NOT executed by any automated process (build, migration, CI, or agent run). A human runs it,
-- once, against a copy of production first and then against production, inside the maintenance
-- window described in docs/MICROSERVICES_PRODUCTION_MIGRATION.md ("Раскатка тенантов").
-- See docs/DONT_FORGET.md → "Раскатка organization_id в social-service (блок 40.13)".
--
-- ORDER MATTERS, AND GETTING IT WRONG IS VISIBLE TO USERS:
--
--   1. Deploy social-service on the new code with the service STOPPED, so its EF migration
--      20260816081204_AddOrganizationId runs. That migration adds the seven columns, turns on
--      row-level security, and performs exactly two unique-index swaps (DiscussTags.Slug and the
--      two Friendships pair indexes) — the ones without which the second customer is broken from
--      the moment the deploy lands. It creates and drops no read index — step 4 does that.
--   2. Run THIS file. Between step 1 and step 2 every pre-existing row carries the all-zeros
--      placeholder organization, and the RLS policy therefore hides it from everybody — fail-closed
--      working as designed (docs/TENANCY/TENANCY.md §1.5), but to a user it looks exactly like
--      "the whole forum and all my friends are gone". Do not leave that window open.
--   3. Run docs/TENANCY/mongo/40.13_chat_conversations_organization_backfill.js. Same window, same
--      reason: a chat document with no organizationId matches no filter, so the chat list is empty
--      until it runs.
--   4. Run 40.13_social_organization_indexes_concurrently.sql. That one can be done later with the
--      service running: it is performance, not correctness.
--
-- Invocation (scripts/tenancy-social-organization-rollout.sh does this for you):
--   psql -v ON_ERROR_STOP=1 \
--        -v organization_id="'00000000-0000-4000-8000-000000000001'" \
--        -d social -f 40.13_social_organization_backfill.sql
--
-- Use the SAME :organization_id as the 40.9–40.12 scripts. social-db has no tenant registry of its
-- own to look it up in — that is organization-db's job — so the value is passed in, and the
-- assertions below check the shape of what was written rather than trusting it.
--
-- Idempotent: re-running changes nothing.
--
-- WHAT IT TOUCHES
--
--   Friendships, DiscussThreads, DiscussReplies, DiscussVotes, DiscussThreadTags, DiscussPhotos —
--   all six strict tenant tables, placeholder -> the default organization.
--
--   DiscussTags is the interesting one and is handled separately at the end. Its column is
--   nullable, so the migration left every existing tag at NULL = "global, shared by every
--   organization". For curated tags that is exactly right — they ARE the shared vocabulary. For a
--   tag some salesperson typed while starting a thread it is wrong: leaving it global would publish
--   one customer's product names into every future customer's tag picker. So non-curated tags are
--   moved into the default organization and curated ones are deliberately left alone.
--
--   UserReplicas is deliberately untouched: it projects identity's cross-organization user
--   directory and has no OrganizationId column at all (docs/TENANCY/TENANCY.md §4.2).
--
-- WHAT IT DOES NOT TOUCH
--
--   MinIO object keys. New uploads are written under org/{organizationId}/…; existing objects keep
--   their old keys and are still served, because the key is read from the DiscussPhotos row and
--   never recomputed. Renaming live objects would be an operation on live infrastructure for zero
--   correctness gain.

\set ON_ERROR_STOP on

BEGIN;

-- psql does not interpolate :variables inside dollar-quoted blocks, so the value is handed to the
-- DO block through a session GUC instead — the same trick 40.9–40.12 used.
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
    table_name                text;
    tenant_tables             text[] := ARRAY[
        'Friendships',
        'DiscussThreads',
        'DiscussReplies',
        'DiscussVotes',
        'DiscussThreadTags',
        'DiscussPhotos'
    ];
BEGIN
    SELECT organization_id INTO recorded_organization_id FROM tenancy_backfill_40_13 WHERE id = 1;

    IF recorded_organization_id <> requested_organization_id THEN
        RAISE EXCEPTION
            'This database was already backfilled for organization %, refusing to re-point it at % — '
            'moving one customer''s conversations into another tenant is not something a script gets '
            'to decide.',
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

    -- Tags: curated stays global, user-authored moves into the organization. Only rows that are
    -- still NULL are touched, so a re-run after a partial failure finishes the job rather than
    -- re-pointing anything.
    UPDATE "DiscussTags"
       SET "OrganizationId" = requested_organization_id
     WHERE "OrganizationId" IS NULL
       AND "IsCurated" = false;

    -- Children must not end up in a different tenant from their parent. The foreign keys do not
    -- enforce this (they are on ThreadId / TagId alone), so it is asserted here instead. After a
    -- single-organization backfill this cannot trip; it exists because this file is also the
    -- template for the next customer's data import, where it can.
    IF EXISTS (
        SELECT 1 FROM "DiscussReplies" child
        JOIN "DiscussThreads" parent ON parent."Id" = child."ThreadId"
        WHERE child."OrganizationId" <> parent."OrganizationId"
        UNION ALL
        SELECT 1 FROM "DiscussThreadTags" child
        JOIN "DiscussThreads" parent ON parent."Id" = child."ThreadId"
        WHERE child."OrganizationId" <> parent."OrganizationId"
    ) THEN
        RAISE EXCEPTION
            'At least one reply or thread-tag belongs to a different organization than its thread. '
            'That row is unreachable through the API and its parent''s cascade delete would cross a '
            'tenant boundary — aborting.';
    END IF;

    -- A thread-tag pointing at a tag that is neither global nor in the same organization is the
    -- Discuss equivalent of the same mistake, and the join above cannot see it.
    IF EXISTS (
        SELECT 1 FROM "DiscussThreadTags" link
        JOIN "DiscussTags" tag ON tag."Id" = link."TagId"
        WHERE tag."OrganizationId" IS NOT NULL
          AND tag."OrganizationId" <> link."OrganizationId"
    ) THEN
        RAISE EXCEPTION
            'At least one thread is tagged with another organization''s tag — aborting.';
    END IF;
END
$$;

COMMIT;

\echo 'social-db: every friendship, thread, reply, vote, thread-tag and photo now belongs to the default organization; curated tags stayed global.'
\echo 'NEXT: docs/TENANCY/mongo/40.13_chat_conversations_organization_backfill.js — the chat list is empty until it runs.'
