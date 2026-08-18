-- Sellevate — Phase 40.18: verify that copy-on-write overrides landed correctly (learning-db).
--
-- READ-ONLY. Every statement is a SELECT or a DO block that only raises. It creates nothing, drops
-- nothing, updates nothing, and is safe to run against a production database with the service up.
-- NOT executed by any automated process (build, migration, CI, or agent run).
--
-- Invocation:
--   psql -v ON_ERROR_STOP=1 -d learning -f 40.18_content_overrides_verify.sql
--
-- Run it AFTER learning-service has started once on the 40.18 code, so that EF migration
-- 20260817211121_AddContentOverrides has applied. The ai-service half of the block is a separate
-- database and a separate migration (20260817_AddDialogModeOverrides); its two checks are at the
-- bottom of this file, commented, because they must be run against `ai`, not `learning`.
--
--
-- WHY THERE IS NO 40.18_..._indexes_concurrently.sql
--
-- Deliberate, and stated so nobody wonders whether it was forgotten. Blocks 40.10-40.13 each
-- shipped one because each rebuilt indexes on tables that were already large and already live.
-- Everything this migration does is cheap on Postgres 11+:
--
--   * two nullable columns per table (ParentTechniqueId / ParentMaterialId, BaseContentHash) —
--     a catalog change, no table rewrite;
--   * one NOT NULL boolean per table with a constant default (IsArchived) — also a catalog change,
--     because Postgres 11 stores the default in pg_attribute.attmissingval instead of rewriting;
--   * two indexes, over Techniques and ReferenceMaterials, which hold tens to hundreds of rows —
--     not the millions the 40.10-40.13 progress tables hold;
--   * three CHECK constraints, each a single scan of those same small tables.
--
-- The indexes are not decoration: read resolution ("an override exists -> use it") is an anti-join
-- on exactly these columns and runs on the learner's hot path.
--
--
-- WHY THERE IS NO BACKFILL, AND THEREFORE NO WINDOW OF INVISIBLE DATA
--
-- Same shape as 40.15 and 40.17, and it is worth being explicit because 40.10-40.13 each did have
-- such a window. Nothing here fills a column on an existing row: every existing lesson, technique
-- and reference material stays exactly as it is, with a null parent and a false IsArchived, which
-- is the correct value for "this is the global library, not somebody's copy". The first non-null
-- parent appears when an administrator presses "edit" — and if nobody ever does, the database is
-- indistinguishable from its pre-40.18 state.
--
-- That is also the block's central product rule, checked below: onboarding an organization must
-- create zero copies.

\set ON_ERROR_STOP on

\echo '--- 1. schema: the 40.18 columns, indexes and constraints are present ---'

DO $$
DECLARE
    missing_names text;
BEGIN
    SELECT string_agg(expected.name, ', ' ORDER BY expected.name)
    INTO missing_names
    FROM (VALUES
        ('Techniques.ParentTechniqueId'),
        ('Techniques.BaseContentHash'),
        ('Techniques.IsArchived'),
        ('ReferenceMaterials.ParentMaterialId'),
        ('ReferenceMaterials.BaseContentHash'),
        ('ReferenceMaterials.IsArchived')
    ) AS expected(name)
    WHERE NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = split_part(expected.name, '.', 1)
          AND column_name = split_part(expected.name, '.', 2)
    );

    IF missing_names IS NOT NULL THEN
        RAISE EXCEPTION 'FAIL: missing 40.18 columns: %', missing_names;
    END IF;

    SELECT string_agg(expected.name, ', ' ORDER BY expected.name)
    INTO missing_names
    FROM (VALUES
        ('IX_Techniques_ParentTechniqueId'),
        ('IX_ReferenceMaterials_ParentMaterialId'),
        ('IX_Lessons_ParentLessonId')
    ) AS expected(name)
    WHERE NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = expected.name);

    IF missing_names IS NOT NULL THEN
        RAISE EXCEPTION 'FAIL: missing 40.18 resolution indexes: %', missing_names;
    END IF;

    SELECT string_agg(expected.name, ', ' ORDER BY expected.name)
    INTO missing_names
    FROM (VALUES
        ('CK_Techniques_OverrideHasOwner'),
        ('CK_ReferenceMaterials_OverrideHasOwner'),
        ('CK_Lessons_OverrideHasOwner')
    ) AS expected(name)
    WHERE NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = expected.name);

    IF missing_names IS NOT NULL THEN
        RAISE EXCEPTION 'FAIL: missing 40.18 CHECK constraints: %', missing_names;
    END IF;

    RAISE NOTICE 'OK: 40.18 columns, indexes and constraints present.';
END $$;

\echo '--- 2. every index this block relies on is valid (a failed concurrent build leaves an invalid one) ---'

SELECT
    class_index.relname AS index_name,
    index_meta.indisvalid,
    index_meta.indisready
FROM pg_index AS index_meta
JOIN pg_class AS class_index ON class_index.oid = index_meta.indexrelid
WHERE class_index.relname IN (
    'IX_Techniques_ParentTechniqueId',
    'IX_ReferenceMaterials_ParentMaterialId',
    'IX_Lessons_ParentLessonId'
)
ORDER BY class_index.relname;

DO $$
DECLARE
    invalid_names text;
BEGIN
    SELECT string_agg(class_index.relname, ', ' ORDER BY class_index.relname)
    INTO invalid_names
    FROM pg_index AS index_meta
    JOIN pg_class AS class_index ON class_index.oid = index_meta.indexrelid
    WHERE class_index.relname IN (
        'IX_Techniques_ParentTechniqueId',
        'IX_ReferenceMaterials_ParentMaterialId',
        'IX_Lessons_ParentLessonId'
    )
      AND NOT (index_meta.indisvalid AND index_meta.indisready);

    IF invalid_names IS NOT NULL THEN
        RAISE EXCEPTION 'FAIL: invalid indexes (planner ignores them, writes still pay for them): %', invalid_names;
    END IF;

    RAISE NOTICE 'OK: all resolution indexes valid.';
END $$;

\echo '--- 3. THE PRODUCT RULE: an override always has an owner, and a global row never has a parent ---'

DO $$
DECLARE
    orphan_count bigint;
BEGIN
    SELECT
        (SELECT count(*) FROM "Lessons" WHERE "ParentLessonId" IS NOT NULL AND "OrganizationId" IS NULL)
      + (SELECT count(*) FROM "Techniques" WHERE "ParentTechniqueId" IS NOT NULL AND "OrganizationId" IS NULL)
      + (SELECT count(*) FROM "ReferenceMaterials" WHERE "ParentMaterialId" IS NOT NULL AND "OrganizationId" IS NULL)
    INTO orphan_count;

    IF orphan_count > 0 THEN
        RAISE EXCEPTION
            'FAIL: % ownerless override row(s). A global row that overrides a global row hides the shared library behind a copy of itself, for every customer at once.',
            orphan_count;
    END IF;

    RAISE NOTICE 'OK: every override has an owning organization.';
END $$;

\echo '--- 4. an override never points at another organization''s row ---'

DO $$
DECLARE
    cross_tenant_count bigint;
BEGIN
    SELECT
        (SELECT count(*)
           FROM "Lessons" AS child
           JOIN "Lessons" AS parent ON parent."Id" = child."ParentLessonId"
          WHERE parent."OrganizationId" IS NOT NULL)
      + (SELECT count(*)
           FROM "Techniques" AS child
           JOIN "Techniques" AS parent ON parent."Id" = child."ParentTechniqueId"
          WHERE parent."OrganizationId" IS NOT NULL)
      + (SELECT count(*)
           FROM "ReferenceMaterials" AS child
           JOIN "ReferenceMaterials" AS parent ON parent."Id" = child."ParentMaterialId"
          WHERE parent."OrganizationId" IS NOT NULL)
    INTO cross_tenant_count;

    IF cross_tenant_count > 0 THEN
        RAISE EXCEPTION
            'FAIL: % override(s) forked from a row that is not global. Overrides fork the shared library, never another customer''s copy.',
            cross_tenant_count;
    END IF;

    RAISE NOTICE 'OK: every override forks a global row.';
END $$;

\echo '--- 5. at most one live override of one base per organization (resolution assumes it) ---'

DO $$
DECLARE
    duplicate_count bigint;
BEGIN
    SELECT count(*) INTO duplicate_count FROM (
        SELECT "OrganizationId", "ParentLessonId"
          FROM "Lessons"
         WHERE "ParentLessonId" IS NOT NULL AND NOT "IsArchived"
         GROUP BY 1, 2 HAVING count(*) > 1
        UNION ALL
        SELECT "OrganizationId", "ParentTechniqueId"
          FROM "Techniques"
         WHERE "ParentTechniqueId" IS NOT NULL AND NOT "IsArchived"
         GROUP BY 1, 2 HAVING count(*) > 1
        UNION ALL
        SELECT "OrganizationId", "ParentMaterialId"
          FROM "ReferenceMaterials"
         WHERE "ParentMaterialId" IS NOT NULL AND NOT "IsArchived"
         GROUP BY 1, 2 HAVING count(*) > 1
    ) AS duplicates;

    IF duplicate_count > 0 THEN
        RAISE EXCEPTION
            'FAIL: % base row(s) have more than one live override in one organization. Read resolution would then pick one arbitrarily.',
            duplicate_count;
    END IF;

    RAISE NOTICE 'OK: at most one live override per base per organization.';
END $$;

\echo '--- 6. an override lesson''s exercises belong to the same organization as the lesson ---'

DO $$
DECLARE
    stray_count bigint;
BEGIN
    SELECT count(*)
    INTO stray_count
    FROM "Exercises" AS exercise
    JOIN "Lessons" AS lesson ON lesson."Id" = exercise."LessonId"
    WHERE lesson."OrganizationId" IS DISTINCT FROM exercise."OrganizationId";

    IF stray_count > 0 THEN
        RAISE EXCEPTION
            'FAIL: % exercise row(s) whose organization disagrees with their lesson. An exercise left global inside an organization''s override appears in that lesson for every other customer.',
            stray_count;
    END IF;

    RAISE NOTICE 'OK: every exercise agrees with its lesson about who owns it.';
END $$;

\echo '--- 7. informational: the override population and how much of it is stale ---'

SELECT
    'lesson' AS kind,
    override."OrganizationId",
    count(*) AS overrides,
    count(*) FILTER (
        WHERE base_current."Id" IS NOT NULL
          AND base_current."Id" IS DISTINCT FROM (
              SELECT version."BaseVersionId"
                FROM "LessonVersions" AS version
               WHERE version."LessonId" = override."Id"
               ORDER BY version."VersionNumber" DESC
               LIMIT 1)
    ) AS stale
FROM "Lessons" AS override
LEFT JOIN LATERAL (
    SELECT version."Id"
      FROM "LessonVersions" AS version
     WHERE version."LessonId" = override."ParentLessonId"
       AND version."Status" = 'published'
     ORDER BY version."VersionNumber" DESC
     LIMIT 1
) AS base_current ON TRUE
WHERE override."ParentLessonId" IS NOT NULL AND NOT override."IsArchived"
GROUP BY 1, 2

UNION ALL

SELECT
    'technique',
    override."OrganizationId",
    count(*),
    NULL
FROM "Techniques" AS override
WHERE override."ParentTechniqueId" IS NOT NULL AND NOT override."IsArchived"
GROUP BY 1, 2

UNION ALL

SELECT
    'reference-material',
    override."OrganizationId",
    count(*),
    NULL
FROM "ReferenceMaterials" AS override
WHERE override."ParentMaterialId" IS NOT NULL AND NOT override."IsArchived"
GROUP BY 1, 2

ORDER BY 1, 2;

-- The NULLs in the "stale" column above are honest, not missing: a technique's and a reference
-- material's fork point is a content fingerprint computed in C# (ContentSnapshotSerializer), and SQL
-- cannot reproduce it — jsonb re-normalizes key order on write, so a hash assembled here would not
-- match the one the service produces. That is the same reason 40.16 minted lesson "version 1" in C#
-- rather than in SQL. Read those two counts from GET /admin/content/overrides?staleOnly=true.

\echo '--- 8. sanity: onboarding creates no copies ---'

DO $$
DECLARE
    total_overrides bigint;
BEGIN
    SELECT
        (SELECT count(*) FROM "Lessons" WHERE "ParentLessonId" IS NOT NULL)
      + (SELECT count(*) FROM "Techniques" WHERE "ParentTechniqueId" IS NOT NULL)
      + (SELECT count(*) FROM "ReferenceMaterials" WHERE "ParentMaterialId" IS NOT NULL)
    INTO total_overrides;

    RAISE NOTICE
        'INFO: % override row(s) in total. On a fresh 40.18 deployment this must be 0 — copies are made only when an administrator presses "edit", never at onboarding.',
        total_overrides;
END $$;

\echo '--- done ---'

--
-- THE ai-service HALF — run these against the `ai` database, not `learning`.
--
--   psql -v ON_ERROR_STOP=1 -d ai -c '<one of the statements below>'
--
-- 1. The columns and the constraint exist:
--
--   SELECT column_name FROM information_schema.columns
--    WHERE table_name = 'DialogModes' AND column_name IN ('ParentModeId', 'BaseContentHash');
--   SELECT conname FROM pg_constraint WHERE conname = 'CK_DialogModes_OverrideHasOwner';
--
-- 2. The seeded hidden modes are still global — this is the invariant the roadmap calls out by
--    name, and ai-service also has a unit test for it:
--
--   SELECT mode."Key", mode."OrganizationId", mode."ParentModeId"
--     FROM "DialogModes" AS mode
--    WHERE mode."Key" IN ('company-call', 'custom-scenario');
--   -- both rows must show OrganizationId IS NULL and ParentModeId IS NULL.
--
-- 3. No override forks a non-global mode:
--
--   SELECT count(*) FROM "DialogModes" AS child
--     JOIN "DialogModes" AS parent ON parent."Id" = child."ParentModeId"
--    WHERE parent."OrganizationId" IS NOT NULL;   -- must be 0
--
