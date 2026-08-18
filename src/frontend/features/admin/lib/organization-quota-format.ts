import type {
    OrganizationQuotaState,
    OrganizationQuotaWriteModel,
} from "@/features/admin/hooks/use-organization-quota";

/**
 * Formatting and validation for the platform panel's quota editor (`/admin/organizations/[id]/quota`).
 *
 * English on purpose: this is the platform panel, which docs/LOCALIZATION.md and
 * docs/TENANCY/ADMIN_UI_DESIGN.md §1.4 keep in English while `/org/*` is Russian. The *rules* are the
 * ones slice 9 already settled in `features/org-usage/lib/format-usage.ts` — an unpriced model reports
 * "no price" and never `0`, `hasUnpricedModels` is authoritative for the total, and a limit of `0`
 * draws no bar — restated here in this panel's own dialect rather than imported, because importing
 * would drag the organization panel's Russian labels onto an English screen.
 */

/** `AiQuotaScales.MaximumBatchReservePercent`. ai-service clamps silently to this; the form refuses instead. */
export const MAXIMUM_BATCH_RESERVE_PERCENT = 90;

/** `AiQuotaScales.PercentScale`. */
const PERCENT_SCALE = 100;

/** Rendered in place of an `estimatedCost: null` line — never "0", which reads as "this model is free". */
export const NO_PRICE_LABEL = "No price";

/** Rendered under the model table when the report's own total is null because some model is unpriced. */
export const TOTAL_COST_UNAVAILABLE_LABEL =
    "Total cost is not computed: at least one model used this month has no price in the table.";

/** Currency codes this panel knows a symbol for. Anything else falls back to the raw code. */
const CURRENCY_SYMBOLS: Record<string, string> = {
    RUB: "₽",
    USD: "$",
    EUR: "€",
};

/** `1027245` → `"1,027,245"`. */
export function formatWholeNumber(value: number): string {
    return Math.round(value).toLocaleString("en-US");
}

function resolveCurrencySymbol(currencyCode: string): string {
    return CURRENCY_SYMBOLS[currencyCode] ?? currencyCode;
}

/** `543.6789` + `"RUB"` → `"543.68 ₽"`. */
export function formatCurrencyAmount(amount: number, currencyCode: string): string {
    const formattedAmount = amount.toLocaleString("en-US", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    });
    return `${formattedAmount} ${resolveCurrencySymbol(currencyCode)}`;
}

/**
 * One model line's cost. `null` is {@link NO_PRICE_LABEL}, never `"0.00 ₽"` — the mistake 40.34
 * removed from the backend, which the screen in front of it must not put back.
 */
export function formatModelCost(estimatedCost: number | null, currencyCode: string): string {
    return estimatedCost === null ? NO_PRICE_LABEL : formatCurrencyAmount(estimatedCost, currencyCode);
}

/**
 * The report's total. `hasUnpricedModels` outranks the amount: a total that silently omits the
 * unpriced lines is a partial sum wearing the clothes of a complete one.
 */
export function formatTotalCost(
    estimatedCost: number | null,
    hasUnpricedModels: boolean,
    currencyCode: string
): string {
    if (hasUnpricedModels || estimatedCost === null) {
        return TOTAL_COST_UNAVAILABLE_LABEL;
    }
    return `Estimated cost this month: ${formatCurrencyAmount(estimatedCost, currencyCode)}`;
}

/**
 * A limit of `0` is not a closed window. `AiSpendMeter`'s Lua reserve script and its LLM gate both
 * read `limit > 0` before refusing anything, so zero switches enforcement **off** for that window —
 * the opposite of what docs/AI_QUOTAS.md §2 says, and the reason this editor says it out loud.
 */
export function hasConfiguredLimit(limit: number): boolean {
    return limit > 0;
}

/**
 * `100% − batchReservePercent` of the monthly token limit, matching
 * `ResolvedAiQuota.BatchTokenCeiling` including its integer truncation. Background work — content
 * generation, batch rewrite, AI review — stops here while interactive work runs on to the full limit.
 */
export function calculateBatchTokenCeiling(monthlyTokenLimit: number, batchReservePercent: number): number {
    const clampedReservePercent = Math.min(Math.max(batchReservePercent, 0), MAXIMUM_BATCH_RESERVE_PERCENT);
    return monthlyTokenLimit - Math.floor((monthlyTokenLimit * clampedReservePercent) / PERCENT_SCALE);
}

/** Never negative: an organization past its ceiling has nothing left, not a debt. */
export function calculateRemainingTokens(ceiling: number, spentTokens: number): number {
    return Math.max(ceiling - spentTokens, 0);
}

/**
 * The grey line under each field. Names both the number in force and where it comes from, because
 * an empty field and a field holding the default look identical and mean different things.
 */
export function describeEffectiveValue(
    organizationValue: number | null,
    effectiveValue: number,
    unitLabel: string
): string {
    const source = organizationValue === null ? "platform default" : "this organization's own value";
    return `In effect now: ${formatWholeNumber(effectiveValue)} ${unitLabel} (${source})`;
}

/** English labels for `AiSpendReportDto.QuotaState`, translated in one place for the whole screen. */
export const QUOTA_STATE_LABELS: Record<OrganizationQuotaState, string> = {
    ok: "OK",
    warning: "Warning",
    batch_paused: "Background work paused",
    exhausted: "Exhausted",
};

/** What each state means for the two ceilings — the distinction the state exists to carry. */
export const QUOTA_STATE_DESCRIPTIONS: Record<OrganizationQuotaState, string> = {
    ok: "Below the soft warning threshold. Nothing is refused.",
    warning:
        "Past the soft warning threshold. Nothing is refused yet — this exists so somebody sees the wall coming.",
    batch_paused:
        "Past the background ceiling. Content generation, batch rewrite and AI review are refused; conversations and graded exercises still run.",
    exhausted: "At the token limit. Interactive calls are refused too, until the next UTC month.",
};

export const QUOTA_STATE_BADGE_CLASSES: Record<OrganizationQuotaState, string> = {
    ok: "bg-olive-soft text-olive",
    warning: "bg-warn-soft text-warn",
    batch_paused: "bg-warn-soft text-warn",
    exhausted: "bg-bad-soft text-bad",
};

export function describeQuotaState(quotaState: string): string {
    return QUOTA_STATE_LABELS[quotaState as OrganizationQuotaState] ?? quotaState;
}

/**
 * Whether this session may actually write the organization named in the URL.
 *
 * `PUT /admin/ai-quota` writes to `X-Organization-Id`, which the gateway derives from the token's
 * `org_id` claim and from nothing a caller can set. So the route parameter is a claim to be checked,
 * not a parameter to be sent: a session scoped elsewhere would silently edit the wrong tenant, and a
 * session scoped nowhere makes ai-service throw (`"Organization context is not set."` → 500).
 */
export type QuotaEditability =
    | { status: "editable" }
    | { status: "no_organization_in_session" }
    | { status: "different_organization_in_session"; sessionOrganizationId: string };

export function resolveQuotaEditability(
    routeOrganizationId: string,
    sessionOrganizationId: string | null | undefined
): QuotaEditability {
    if (!sessionOrganizationId) {
        return { status: "no_organization_in_session" };
    }
    if (sessionOrganizationId.toLowerCase() !== routeOrganizationId.toLowerCase()) {
        return { status: "different_organization_in_session", sessionOrganizationId };
    }
    return { status: "editable" };
}

export function describeQuotaEditability(editability: QuotaEditability): string | null {
    switch (editability.status) {
        case "no_organization_in_session":
            return "Your session carries no organization, so ai-service answers with the platform defaults and would refuse the write. Saving is disabled.";
        case "different_organization_in_session":
            return `Your session is scoped to organization ${editability.sessionOrganizationId}, not to the one in this URL. Saving would edit that other organization, so it is disabled.`;
        default:
            return null;
    }
}

export interface QuotaFormValues {
    voiceDailyLimitMinutes: string;
    voiceMonthlyLimitMinutes: string;
    llmMonthlyTokenLimit: string;
    batchReservePercent: string;
    note: string;
}

export type QuotaFormResult =
    | { status: "valid"; writeModel: OrganizationQuotaWriteModel }
    | { status: "invalid"; validationMessage: string };

type ParsedLimit =
    | { status: "cleared" }
    | { status: "parsed"; value: number }
    | { status: "invalid" };

function parseOptionalLimit(rawText: string): ParsedLimit {
    const trimmedText = rawText.trim();
    if (trimmedText.length === 0) {
        return { status: "cleared" };
    }
    if (!/^\d+$/.test(trimmedText)) {
        return { status: "invalid" };
    }
    return { status: "parsed", value: Number(trimmedText) };
}

/**
 * Turns the form into `AiQuotaWriteModel`, refusing what ai-service would accept and then quietly
 * reinterpret: a negative value becomes `null` (a reset to the platform default, not the number
 * typed) and a reserve above 90 is clamped without saying so.
 */
export function buildQuotaWriteModel(values: QuotaFormValues): QuotaFormResult {
    const fields: Array<{ label: string; rawText: string }> = [
        { label: "Voice minutes per day", rawText: values.voiceDailyLimitMinutes },
        { label: "Voice minutes per month", rawText: values.voiceMonthlyLimitMinutes },
        { label: "LLM tokens per month", rawText: values.llmMonthlyTokenLimit },
        { label: "Background reserve, %", rawText: values.batchReservePercent },
    ];

    const parsedFields = fields.map((field) => ({ ...field, parsed: parseOptionalLimit(field.rawText) }));
    const invalidField = parsedFields.find((field) => field.parsed.status === "invalid");
    if (invalidField) {
        return {
            status: "invalid",
            validationMessage: `"${invalidField.label}" must be a whole number of zero or more, or empty to fall back to the platform default.`,
        };
    }

    const [voiceDaily, voiceMonthly, llmTokens, batchReserve] = parsedFields.map((field) =>
        field.parsed.status === "parsed" ? field.parsed.value : null
    );

    if (batchReserve !== null && batchReserve > MAXIMUM_BATCH_RESERVE_PERCENT) {
        return {
            status: "invalid",
            validationMessage: `Background reserve is capped at ${MAXIMUM_BATCH_RESERVE_PERCENT}% — ai-service would clamp a larger number without telling you.`,
        };
    }

    const trimmedNote = values.note.trim();

    return {
        status: "valid",
        writeModel: {
            voiceDailyLimitMinutes: voiceDaily,
            voiceMonthlyLimitMinutes: voiceMonthly,
            llmMonthlyTokenLimit: llmTokens,
            batchReservePercent: batchReserve,
            note: trimmedNote.length === 0 ? null : trimmedNote,
        },
    };
}

/**
 * The warning under a field the operator has just set to `0`. Zero reads as "closed" and behaves as
 * "wide open" — see {@link hasConfiguredLimit}.
 */
export function describeZeroLimitEffect(rawText: string): string | null {
    return rawText.trim() === "0"
        ? "0 removes the ceiling entirely — ai-service stops refusing on this window. To lower a limit, set a small number instead."
        : null;
}

/** Fills the form from the row so an unset field stays visibly empty rather than showing the default as if it were set. */
export function toFormValues(settings: {
    voiceDailyLimitMinutes: number | null;
    voiceMonthlyLimitMinutes: number | null;
    llmMonthlyTokenLimit: number | null;
    batchReservePercent: number | null;
    note: string | null;
}): QuotaFormValues {
    return {
        voiceDailyLimitMinutes: settings.voiceDailyLimitMinutes?.toString() ?? "",
        voiceMonthlyLimitMinutes: settings.voiceMonthlyLimitMinutes?.toString() ?? "",
        llmMonthlyTokenLimit: settings.llmMonthlyTokenLimit?.toString() ?? "",
        batchReservePercent: settings.batchReservePercent?.toString() ?? "",
        note: settings.note ?? "",
    };
}
