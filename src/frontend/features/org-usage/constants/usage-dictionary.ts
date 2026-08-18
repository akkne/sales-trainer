/**
 * O17 «Расход ИИ» (docs/TENANCY/ADMIN_UI_DESIGN.md §1.4, §2 O17). Every backend value this screen
 * shows to a human is translated exactly once, here, and never as a literal in a component —
 * otherwise the same `batch_paused` becomes a different sentence on two different renders.
 */

/** `AiSpendReportDto.QuotaState` — mirrors `Sellevate.Ai.Features.Quotas.Constants.AiQuotaStates`. */
export type AiUsageQuotaState = "ok" | "warning" | "batch_paused" | "exhausted";

/** `AiSpendModelLineDto.Kind` — mirrors `Sellevate.Ai.Features.Quotas.Models.AiUsageKinds`. */
export type AiUsageKind = "llm" | "tts" | "stt";

/** How a per-model row names what it metered. Falls back to the raw kind for a value not in this table. */
export const AI_USAGE_KIND_LABELS: Record<AiUsageKind, string> = {
    llm: "модель",
    tts: "синтез",
    stt: "распознавание",
};

export function describeUsageKind(kind: string): string {
    return AI_USAGE_KIND_LABELS[kind as AiUsageKind] ?? kind;
}

export interface QuotaStateBannerCopy {
    title: string;
    description: string;
    tone: "amber" | "bad";
}

/**
 * The three states that show a banner. `ok` shows none — the absence of a row here is the "no
 * banner" case, not a fourth entry with empty strings.
 */
export const QUOTA_STATE_BANNER_COPY: Partial<Record<AiUsageQuotaState, QuotaStateBannerCopy>> = {
    warning: {
        title: "Приближаетесь к лимиту",
        description:
            "Расход токенов этого месяца близок к лимиту. Пока ничего не остановлено — разговоры и генерация работают как обычно.",
        tone: "amber",
    },
    batch_paused: {
        title: "Фоновая генерация приостановлена",
        description:
            "Разговоры и упражнения работают как обычно. Массовая правка и генерация уроков возобновятся в следующем месяце или после того, как лимит поднимут. Напишите вашему менеджеру Sellevate.",
        tone: "amber",
    },
    exhausted: {
        title: "Лимит исчерпан",
        description: "Разговоры и генерация недоступны до следующего месяца.",
        tone: "bad",
    },
};

/** Nominative Russian month names, index 0 = January — for turning a `yyyy-MM` period key into a heading. */
export const RUSSIAN_MONTH_NAMES_NOMINATIVE = [
    "январь",
    "февраль",
    "март",
    "апрель",
    "май",
    "июнь",
    "июль",
    "август",
    "сентябрь",
    "октябрь",
    "ноябрь",
    "декабрь",
] as const;

/** Currency codes this screen knows a symbol for. Anything else falls back to the raw code. */
export const CURRENCY_SYMBOLS: Record<string, string> = {
    RUB: "₽",
};

/** Rendered in place of an `estimatedCost: null` line — never "0 ₽", which reads as "this model is free". */
export const NO_PRICE_LABEL = "нет цены";

/** Rendered under the model table when the report's own total is null because some model is unpriced. */
export const TOTAL_COST_UNAVAILABLE_LABEL =
    "Итоговая стоимость не считается: для части моделей не задана цена.";
