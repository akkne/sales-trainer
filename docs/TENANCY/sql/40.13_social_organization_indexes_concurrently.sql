-- Sellevate — Phase 40.13, step 4 of 4: rebuild social-db's read indexes with "OrganizationId" first.
--
-- NOT executed by any automated process (build, migration, CI, or agent run), and deliberately NOT
-- part of EF migration 20260816081204_AddOrganizationId or of DatabaseBootstrapper. A transactional
-- index build takes an ACCESS EXCLUSIVE lock on the table, social-service runs Database.Migrate()
-- during startup, and a long build there stalls the readiness probe and races the replicas. So it
-- lives here, gets run by a human against a live database, and can be run with the service up.
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d social -f 40.13_social_organization_indexes_concurrently.sql
--
-- Run it AFTER 40.13_social_organization_backfill.sql. Building an index over rows that all share
-- the placeholder organization is not wrong, but it wastes the maintenance window.
--
-- WHAT THE MIGRATION ALREADY DID, AND WHY THIS FILE HAS THE REST
--
--   The migration performed exactly two unique swaps — "DiscussTags"."Slug" and the two
--   "Friendships" pair indexes — because without them the second customer is broken the moment the
--   deploy lands: their first tag or their first friend request fails on a platform-wide unique
--   violation. Both tables are small and a short lock is the cheaper problem. Everything in this
--   file is a read index on a table that grows, where the lock is the expensive problem.
--
--   Two of the drops below would leave a foreign key without a leading-column index:
--   "DiscussReplies"."ThreadId" (was covered by IX_DiscussReplies_ThreadId_CreatedAt) and
--   "DiscussThreadTags"."ThreadId" (was covered by the old UNIQUE(ThreadId, TagId)). Deleting a
--   thread cascades to both, so this file creates the plain ("ThreadId") indexes before dropping
--   anything — otherwise deleting one thread would sequential-scan two tables.
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

\echo '--- DiscussThreads: every list on the Discuss screen is "this organization, then sort" ---'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DiscussThreads_OrganizationId_AuthorId"
    ON "DiscussThreads" ("OrganizationId", "AuthorId");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DiscussThreads_OrganizationId_IsPinned"
    ON "DiscussThreads" ("OrganizationId", "IsPinned");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DiscussThreads_OrganizationId_LastActivityAt"
    ON "DiscussThreads" ("OrganizationId", "LastActivityAt");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DiscussThreads_OrganizationId_UpvoteCount"
    ON "DiscussThreads" ("OrganizationId", "UpvoteCount");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DiscussThreads_OrganizationId_CreatedAt"
    ON "DiscussThreads" ("OrganizationId", "CreatedAt");

\echo '--- DiscussReplies ---'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DiscussReplies_OrganizationId_AuthorId"
    ON "DiscussReplies" ("OrganizationId", "AuthorId");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DiscussReplies_OrganizationId_ThreadId_CreatedAt"
    ON "DiscussReplies" ("OrganizationId", "ThreadId", "CreatedAt");

\echo '--- DiscussVotes ---'

-- Unique, and organization-first. The old UNIQUE("UserId","TargetType","TargetId") was not a
-- correctness problem across tenants — a target id belongs to exactly one organization — so unlike
-- the tag and friendship swaps this one could wait for a concurrent build.
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "IX_DiscussVotes_OrganizationId_UserId_TargetType_TargetId"
    ON "DiscussVotes" ("OrganizationId", "UserId", "TargetType", "TargetId");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DiscussVotes_OrganizationId_TargetType_TargetId"
    ON "DiscussVotes" ("OrganizationId", "TargetType", "TargetId");

\echo '--- DiscussThreadTags ---'

CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "IX_DiscussThreadTags_OrganizationId_ThreadId_TagId"
    ON "DiscussThreadTags" ("OrganizationId", "ThreadId", "TagId");

\echo '--- DiscussPhotos ---'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DiscussPhotos_OrganizationId_OwnerType_OwnerId_OrderIndex"
    ON "DiscussPhotos" ("OrganizationId", "OwnerType", "OwnerId", "OrderIndex");

\echo '--- DiscussTags ---'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DiscussTags_OrganizationId_IsCurated"
    ON "DiscussTags" ("OrganizationId", "IsCurated");

\echo '--- Friendships ---'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Friendships_OrganizationId_RequesterId"
    ON "Friendships" ("OrganizationId", "RequesterId");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Friendships_OrganizationId_AddresseeId"
    ON "Friendships" ("OrganizationId", "AddresseeId");

\echo '--- keeping the cascade-delete foreign keys indexed ---'

-- Not decoration. Dropping IX_DiscussReplies_ThreadId_CreatedAt and the old
-- UNIQUE(ThreadId, TagId) below leaves both foreign keys to "DiscussThreads" without a
-- leading-column index, and deleting one thread would then scan both tables.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DiscussReplies_ThreadId"
    ON "DiscussReplies" ("ThreadId");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DiscussThreadTags_ThreadId"
    ON "DiscussThreadTags" ("ThreadId");

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

DROP INDEX CONCURRENTLY IF EXISTS "IX_DiscussThreads_AuthorId";
DROP INDEX CONCURRENTLY IF EXISTS "IX_DiscussThreads_IsPinned";
DROP INDEX CONCURRENTLY IF EXISTS "IX_DiscussThreads_LastActivityAt";
DROP INDEX CONCURRENTLY IF EXISTS "IX_DiscussThreads_UpvoteCount";
DROP INDEX CONCURRENTLY IF EXISTS "IX_DiscussThreads_CreatedAt";
DROP INDEX CONCURRENTLY IF EXISTS "IX_DiscussReplies_AuthorId";
DROP INDEX CONCURRENTLY IF EXISTS "IX_DiscussReplies_ThreadId_CreatedAt";
DROP INDEX CONCURRENTLY IF EXISTS "IX_DiscussVotes_UserId_TargetType_TargetId";
DROP INDEX CONCURRENTLY IF EXISTS "IX_DiscussVotes_TargetType_TargetId";
DROP INDEX CONCURRENTLY IF EXISTS "IX_DiscussThreadTags_ThreadId_TagId";
DROP INDEX CONCURRENTLY IF EXISTS "IX_DiscussPhotos_OwnerType_OwnerId_OrderIndex";
DROP INDEX CONCURRENTLY IF EXISTS "IX_DiscussTags_IsCurated";
DROP INDEX CONCURRENTLY IF EXISTS "IX_Friendships_RequesterId";
DROP INDEX CONCURRENTLY IF EXISTS "IX_Friendships_AddresseeId";

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
        ('IX_DiscussThreads_OrganizationId_AuthorId'),
        ('IX_DiscussThreads_OrganizationId_IsPinned'),
        ('IX_DiscussThreads_OrganizationId_LastActivityAt'),
        ('IX_DiscussThreads_OrganizationId_UpvoteCount'),
        ('IX_DiscussThreads_OrganizationId_CreatedAt'),
        ('IX_DiscussReplies_OrganizationId_AuthorId'),
        ('IX_DiscussReplies_OrganizationId_ThreadId_CreatedAt'),
        ('IX_DiscussReplies_ThreadId'),
        ('IX_DiscussVotes_OrganizationId_UserId_TargetType_TargetId'),
        ('IX_DiscussVotes_OrganizationId_TargetType_TargetId'),
        ('IX_DiscussThreadTags_OrganizationId_ThreadId_TagId'),
        ('IX_DiscussThreadTags_ThreadId'),
        ('IX_DiscussPhotos_OrganizationId_OwnerType_OwnerId_OrderIndex'),
        ('IX_DiscussTags_OrganizationId_IsCurated'),
        ('IX_DiscussTags_OrganizationId_Slug'),
        ('IX_DiscussTags_Slug_Global'),
        ('IX_Friendships_OrganizationId_RequesterId'),
        ('IX_Friendships_OrganizationId_AddresseeId'),
        ('IX_Friendships_OrganizationId_RequesterId_AddresseeId'),
        ('IX_Friendships_CanonicalPair')
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

\echo 'social-db: every tenant index now leads with "OrganizationId", every cascade-delete FK stays indexed, and every index is valid.'
