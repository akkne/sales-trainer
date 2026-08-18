-- 40.33_ai_quotas_verify.sql — read-only checks for the per-organization AI meter (ai-db).
--
-- Phase 40.33. Run against the `ai` database after the AddOrganizationQuotas migration has been
-- applied. **Every statement here is a SELECT.** Nothing is created, updated or dropped, so it is
-- safe on production and safe to run twice.
--
--   psql "$AI_CONNECTION_STRING" -f docs/TENANCY/sql/40.33_ai_quotas_verify.sql
--
-- The agent that wrote this never executed it against any database (docs/DONT_FORGET.md, Rule №1).
--
-- There is deliberately no companion 40.33_*_indexes_concurrently.sql: both tables are created empty
-- by the migration and every read is a prefix scan on the leading primary-key columns.

\echo '== 1. Both tables exist and carry the tenant column first in their primary key =='

SELECT
    c.relname                                        AS table_name,
    i.indisprimary                                   AS is_primary,
    pg_get_indexdef(i.indexrelid)                    AS definition
FROM pg_class c
JOIN pg_index i ON i.indrelid = c.oid
WHERE c.relname IN ('OrganizationQuotas', 'AiUsageRecords')
  AND i.indisprimary
ORDER BY c.relname;

-- Expected: PK_OrganizationQuotas on ("OrganizationId");
--           PK_AiUsageRecords on ("OrganizationId", "PeriodKey", "Model").
-- If the tenant column is NOT first, every meter read becomes a scan and the leading-column
-- assumption behind "no extra index" is false.

\echo '== 2. RLS is enabled AND forced on both, with the strict (plain-equality) policy =='

SELECT
    c.relname            AS table_name,
    c.relrowsecurity     AS rls_enabled,
    c.relforcerowsecurity AS rls_forced
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND c.relname IN ('OrganizationQuotas', 'AiUsageRecords')
ORDER BY c.relname;

-- Expected: t / t on both rows. `relforcerowsecurity = f` means the owning role bypasses the policy
-- and the boundary is EF's query filter alone.

\echo '== 2b. The policy must NOT admit NULL owners (the content-policy mistake) =='

SELECT
    tablename,
    policyname,
    qual        AS using_expression,
    with_check  AS with_check_expression
FROM pg_policies
WHERE tablename IN ('OrganizationQuotas', 'AiUsageRecords')
ORDER BY tablename, policyname;

-- Expected: an expression comparing "OrganizationId" to current_setting('app.organization_id'),
-- and **no `IS NULL OR`**. Both neighbours in this database (DialogBundles, DialogModes) legitimately
-- use the content flavour, so copying the neighbouring policy is the natural mistake — and here it
-- would mean one customer's allowance, or one customer's bill, standing in for everybody's.
-- 40.19 needed exactly this check for OrganizationProfileReplicas.

\echo '== 3. Which organizations have limits of their own (everyone else uses AiQuotas:Default*) =='

SELECT
    "OrganizationId",
    "VoiceDailyLimitMinutes",
    "VoiceMonthlyLimitMinutes",
    "LlmMonthlyTokenLimit",
    "BatchReservePercent",
    "UpdatedAt",
    "Note"
FROM "OrganizationQuotas"
ORDER BY "UpdatedAt" DESC;

-- An empty result is the expected state right after the deploy and is NOT a problem: a missing row
-- means "the platform defaults", not "unmetered". A NULL in a column means the same, per column.
-- A 0 means "this window is disabled" and is a different thing entirely.

\echo '== 4. This month, per organization: tokens, calls, speech =='

SELECT
    "OrganizationId",
    "PeriodKey",
    SUM("PromptTokens")                    AS prompt_tokens,
    SUM("CompletionTokens")                AS completion_tokens,
    SUM("PromptTokens" + "CompletionTokens") AS total_tokens,
    SUM("CallCount")                       AS calls,
    SUM("EstimatedCallCount")              AS estimated_calls,
    SUM("SpeechCharacters")                AS speech_characters
FROM "AiUsageRecords"
WHERE "PeriodKey" = to_char(now() AT TIME ZONE 'utc', 'YYYY-MM')
GROUP BY "OrganizationId", "PeriodKey"
ORDER BY total_tokens DESC;

\echo '== 5. The same, broken down by model — the row the cost estimate is built from =='

SELECT
    "OrganizationId",
    "PeriodKey",
    "Model",
    "Kind",
    "PromptTokens",
    "CompletionTokens",
    "CallCount",
    "EstimatedCallCount",
    "SpeechCharacters",
    "UpdatedAt"
FROM "AiUsageRecords"
WHERE "PeriodKey" = to_char(now() AT TIME ZONE 'utc', 'YYYY-MM')
ORDER BY "OrganizationId", "Kind", "Model";

-- A model appearing here that is absent from AiQuotas:PricePerMillionTokens is reported on
-- GET /admin/ai-usage as unpriced (hasUnpricedModels = true), never as free. That is the signal to
-- add its price — not to ignore the line.

\echo '== 6. How much of the month is a measurement and how much is an estimate =='

SELECT
    "PeriodKey",
    SUM("CallCount")                                              AS calls,
    SUM("EstimatedCallCount")                                     AS estimated_calls,
    ROUND(100.0 * SUM("EstimatedCallCount") / NULLIF(SUM("CallCount"), 0), 1) AS estimated_percent
FROM "AiUsageRecords"
WHERE "Kind" = 'llm'
GROUP BY "PeriodKey"
ORDER BY "PeriodKey" DESC;

-- Only streamed dialog turns are estimated, and they are capped at OpenAI:MaximumDialogTokenCount
-- each, so a high *count* here is normal. What matters is whether estimated calls start carrying a
-- large share of the *tokens*, which they cannot from this query alone — if estimated_percent is
-- high AND the month's total looks wrong against the provider's invoice, that is when
-- `stream_options: {include_usage: true}` becomes worth trying against whichever gateway is in front
-- of the provider.

\echo '== 7. Rows that should not exist: an owner-less or kind-less record =='

SELECT COUNT(*) AS orphan_usage_rows
FROM "AiUsageRecords"
WHERE "OrganizationId" IS NULL
   OR "Kind" NOT IN ('llm', 'tts', 'stt')
   OR "PeriodKey" !~ '^\d{4}-\d{2}$';

SELECT COUNT(*) AS orphan_quota_rows
FROM "OrganizationQuotas"
WHERE "OrganizationId" IS NULL
   OR COALESCE("BatchReservePercent", 0) NOT BETWEEN 0 AND 90;

-- Both must be 0. The first two cannot happen through the service (the tenant column is part of the
-- primary key and Kind is set from a constant), so a non-zero result means something wrote these
-- tables directly — which is worth knowing before the numbers are used for anything commercial.
