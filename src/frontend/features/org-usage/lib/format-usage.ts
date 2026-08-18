import type { MetricBarTone } from "@/shared/components/metric-bar";
import {
    CURRENCY_SYMBOLS,
    NO_PRICE_LABEL,
    RUSSIAN_MONTH_NAMES_NOMINATIVE,
    TOTAL_COST_UNAVAILABLE_LABEL,
} from "@/features/org-usage/constants/usage-dictionary";

/** The percentage of a limit past which a meter turns amber — mirrors `AiQuotas:SoftWarningPercent`'s
 * documented default (docs/AI_QUOTAS.md §5). The API does not echo the configured percent back on the
 * report, so this is the same fixed number the backend ships with, used only for the bar's color. */
const METER_WARNING_THRESHOLD_PERCENT = 80;

/**
 * Turns a `yyyy-MM` period key (`AiUsagePeriod.From`) into the heading's month name, e.g.
 * `"2026-08"` → `"август 2026"`. Read from a fixed table rather than `Intl.DateTimeFormat`, whose
 * `ru-RU` "long month" output is locale-data-dependent and appends a trailing "г." this screen does
 * not want.
 */
export function formatUsagePeriodLabel(periodKey: string): string {
    const [yearText, monthText] = periodKey.split("-");
    const monthIndex = Number(monthText) - 1;
    const monthName = RUSSIAN_MONTH_NAMES_NOMINATIVE[monthIndex];
    return monthName ? `${monthName} ${yearText}` : periodKey;
}

/** Groups digits the way the design's mock does — `1027245` → `"1 027 245"`. */
export function formatWholeNumberRu(value: number): string {
    return Math.round(value).toLocaleString("ru-RU");
}

function resolveCurrencySymbol(currencyCode: string): string {
    return CURRENCY_SYMBOLS[currencyCode] ?? currencyCode;
}

/** `543.6789` + `"RUB"` → `"543,68 ₽"`. */
export function formatCurrencyAmount(amount: number, currencyCode: string): string {
    const formattedAmount = amount.toLocaleString("ru-RU", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    });
    return `${formattedAmount} ${resolveCurrencySymbol(currencyCode)}`;
}

/**
 * A single model line's cost. `null` renders as {@link NO_PRICE_LABEL}, never `"0 ₽"` — a
 * partial-looking zero on an unpriced model is the exact mistake docs/AI_QUOTAS.md §3 and §8
 * describe the backend fixing; this screen must not reintroduce it on the way to the page.
 */
export function formatModelCost(estimatedCost: number | null, currencyCode: string): string {
    return estimatedCost === null ? NO_PRICE_LABEL : formatCurrencyAmount(estimatedCost, currencyCode);
}

/**
 * The report's total cost line. `hasUnpricedModels` is authoritative over `estimatedCost === null`
 * being merely "no spend yet" — the two happen to coincide today because `EstimatedCost` is only
 * ever null when a model is unpriced, but reading the explicit flag keeps this function correct if
 * that ever changes.
 */
export function formatTotalCost(
    estimatedCost: number | null,
    hasUnpricedModels: boolean,
    currencyCode: string
): string {
    if (hasUnpricedModels || estimatedCost === null) {
        return TOTAL_COST_UNAVAILABLE_LABEL;
    }
    return `Итого: ${formatCurrencyAmount(estimatedCost, currencyCode)}`;
}

/**
 * Russian noun agreement for a count: `one` for 1, 21, 31…; `few` for 2–4, 22–24…; `many`
 * otherwise (0, 5–20, 25–30…). Needed because the model table's mock genuinely disagrees with
 * itself in the shown numbers — `"231 вызов"` next to `"1 610 вызовов"` — and that disagreement is
 * correct Russian, not a typo.
 */
export function pluralizeRussianCount(count: number, one: string, few: string, many: string): string {
    const remainderOfHundred = Math.abs(count) % 100;
    const remainderOfTen = remainderOfHundred % 10;

    if (remainderOfHundred > 10 && remainderOfHundred < 20) {
        return many;
    }
    if (remainderOfTen > 1 && remainderOfTen < 5) {
        return few;
    }
    if (remainderOfTen === 1) {
        return one;
    }
    return many;
}

/** `1610` → `"1 610 вызовов"`. */
export function describeCallCount(callCount: number): string {
    return `${formatWholeNumberRu(callCount)} ${pluralizeRussianCount(callCount, "вызов", "вызова", "вызовов")}`;
}

/** A limit of `0` means "no ceiling configured" (docs/AI_QUOTAS.md §2) — the meter draws no bar for it. */
export function hasConfiguredLimit(limit: number): boolean {
    return limit > 0;
}

/**
 * The color a meter's fill takes as it approaches its own limit. Independent of `quotaState`,
 * which is derived only from the LLM token limit on the backend — the two voice windows need their
 * own read of how close they are, and this keeps all three bars consistent with each other.
 */
export function resolveMetricTone(value: number, limit: number): MetricBarTone {
    if (!hasConfiguredLimit(limit)) {
        return "neutral";
    }
    if (value >= limit) {
        return "flame";
    }
    if (value >= (limit * METER_WARNING_THRESHOLD_PERCENT) / 100) {
        return "amber";
    }
    return "neutral";
}
