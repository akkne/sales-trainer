import { describe, expect, it } from "vitest";

import {
    describeCallCount,
    formatCurrencyAmount,
    formatModelCost,
    formatTotalCost,
    formatUsagePeriodLabel,
    formatWholeNumberRu,
    hasConfiguredLimit,
    pluralizeRussianCount,
    resolveMetricTone,
} from "@/features/org-usage/lib/format-usage";
import {
    NO_PRICE_LABEL,
    QUOTA_STATE_BANNER_COPY,
    TOTAL_COST_UNAVAILABLE_LABEL,
    describeUsageKind,
} from "@/features/org-usage/constants/usage-dictionary";

/** `Number.prototype.toLocaleString("ru-RU")` groups digits with U+00A0, not an ASCII space. */
const NON_BREAKING_SPACE = " ";

/**
 * O17 «Расход ИИ» (docs/TENANCY/ADMIN_UI_DESIGN.md O17, docs/AI_QUOTAS.md). What is checked here is
 * the one rule the task exists to enforce: an unpriced model must never render as free.
 */
describe("formatModelCost", () => {
    it("renders a null estimated cost as 'нет цены', never as 0 ₽", () => {
        expect(formatModelCost(null, "RUB")).toBe(NO_PRICE_LABEL);
        expect(formatModelCost(null, "RUB")).not.toContain("0");
    });

    it("renders a priced model with a comma decimal and the ruble sign", () => {
        expect(formatModelCost(543.6789, "RUB")).toBe("543,68 ₽");
    });

    it("renders a genuinely free (zero-cost) model as 0,00 ₽, distinct from unpriced", () => {
        expect(formatModelCost(0, "RUB")).toBe("0,00 ₽");
    });

    it("falls back to the raw currency code for an unmapped currency", () => {
        expect(formatCurrencyAmount(10, "USD")).toBe("10,00 USD");
    });
});

describe("formatTotalCost", () => {
    it("explains the total is unavailable when hasUnpricedModels is true, even if estimatedCost were somehow set", () => {
        expect(formatTotalCost(0, true, "RUB")).toBe(TOTAL_COST_UNAVAILABLE_LABEL);
    });

    it("explains the total is unavailable when estimatedCost is null", () => {
        expect(formatTotalCost(null, false, "RUB")).toBe(TOTAL_COST_UNAVAILABLE_LABEL);
    });

    it("renders the total when every model used this month is priced", () => {
        expect(formatTotalCost(543.68, false, "RUB")).toBe("Итого: 543,68 ₽");
    });
});

describe("formatUsagePeriodLabel", () => {
    it("turns a yyyy-MM period key into a Russian month heading", () => {
        expect(formatUsagePeriodLabel("2026-08")).toBe("август 2026");
        expect(formatUsagePeriodLabel("2026-01")).toBe("январь 2026");
        expect(formatUsagePeriodLabel("2026-12")).toBe("декабрь 2026");
    });

    it("falls back to the raw key for a value it cannot parse", () => {
        expect(formatUsagePeriodLabel("not-a-period")).toBe("not-a-period");
    });
});

describe("formatWholeNumberRu", () => {
    it("groups digits with the design's mock spacing", () => {
        expect(formatWholeNumberRu(1_027_245)).toBe(`1${NON_BREAKING_SPACE}027${NON_BREAKING_SPACE}245`);
        expect(formatWholeNumberRu(1841)).toBe(`1${NON_BREAKING_SPACE}841`);
    });
});

describe("pluralizeRussianCount and describeCallCount", () => {
    it("agrees with the design mock's two examples, which disagree with each other", () => {
        expect(describeCallCount(231)).toBe("231 вызов");
        expect(describeCallCount(1610)).toBe(`1${NON_BREAKING_SPACE}610 вызовов`);
    });

    it("picks the 'few' form for 2-4 (excluding 12-14)", () => {
        expect(pluralizeRussianCount(2, "вызов", "вызова", "вызовов")).toBe("вызова");
        expect(pluralizeRussianCount(4, "вызов", "вызова", "вызовов")).toBe("вызова");
    });

    it("picks the 'many' form for the 11-14 exception even though the last digit looks like 'one'", () => {
        expect(pluralizeRussianCount(11, "вызов", "вызова", "вызовов")).toBe("вызовов");
        expect(pluralizeRussianCount(12, "вызов", "вызова", "вызовов")).toBe("вызовов");
    });

    it("picks the 'one' form for 21, 31... not just 1", () => {
        expect(pluralizeRussianCount(21, "вызов", "вызова", "вызовов")).toBe("вызов");
    });

    it("picks the 'many' form for zero", () => {
        expect(pluralizeRussianCount(0, "вызов", "вызова", "вызовов")).toBe("вызовов");
    });
});

describe("hasConfiguredLimit and resolveMetricTone", () => {
    it("treats a limit of zero as 'no ceiling', not as an exhausted one", () => {
        expect(hasConfiguredLimit(0)).toBe(false);
        expect(resolveMetricTone(500, 0)).toBe("neutral");
    });

    it("stays neutral below the warning threshold", () => {
        expect(resolveMetricTone(1_000_000, 20_000_000)).toBe("neutral");
    });

    it("turns amber at and past 80% of the limit", () => {
        expect(resolveMetricTone(16_000_000, 20_000_000)).toBe("amber");
    });

    it("turns flame at and past the limit itself", () => {
        expect(resolveMetricTone(20_000_000, 20_000_000)).toBe("flame");
        expect(resolveMetricTone(25_000_000, 20_000_000)).toBe("flame");
    });
});

describe("describeUsageKind", () => {
    it("translates the three usage kinds the backend records", () => {
        expect(describeUsageKind("llm")).toBe("модель");
        expect(describeUsageKind("tts")).toBe("синтез");
        expect(describeUsageKind("stt")).toBe("распознавание");
    });

    it("falls back to the raw value for an unrecognized kind rather than guessing", () => {
        expect(describeUsageKind("unknown_kind")).toBe("unknown_kind");
    });
});

describe("QUOTA_STATE_BANNER_COPY", () => {
    it("has no entry for 'ok' — the absence of a banner, not an empty one", () => {
        expect(QUOTA_STATE_BANNER_COPY.ok).toBeUndefined();
    });

    it("has an entry for each of the three states that must show a banner", () => {
        expect(QUOTA_STATE_BANNER_COPY.warning).toBeDefined();
        expect(QUOTA_STATE_BANNER_COPY.batch_paused).toBeDefined();
        expect(QUOTA_STATE_BANNER_COPY.exhausted).toBeDefined();
    });

    it("explains batch_paused as background-only, distinguishing it from a full outage", () => {
        expect(QUOTA_STATE_BANNER_COPY.batch_paused?.description).toMatch(/как обычно/);
    });
});
