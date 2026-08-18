import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";

import { ModelUsageTable } from "@/features/org-usage/components/model-usage-table";
import { QuotaMeters } from "@/features/org-usage/components/quota-meters";
import { QuotaStateBanner } from "@/features/org-usage/components/quota-state-banner";
import type { AiSpendModelLine, AiSpendReport } from "@/features/org-usage/hooks/use-ai-usage-report";

function buildReport(overrides: Partial<AiSpendReport> = {}): AiSpendReport {
    return {
        periodKey: "2026-08",
        currency: "RUB",
        quotaState: "ok",
        llmPromptTokens: 612_340,
        llmCompletionTokens: 180_905,
        llmTotalTokens: 793_245,
        llmMonthlyTokenLimit: 20_000_000,
        llmCallCount: 1841,
        llmEstimatedCallCount: 0,
        speechCharacters: 418_220,
        voiceUsedMinutesToday: 37,
        voiceDailyLimitMinutes: 600,
        voiceUsedMinutesThisMonth: 612,
        voiceMonthlyLimitMinutes: 6000,
        estimatedCost: null,
        hasUnpricedModels: true,
        models: [],
        ...overrides,
    };
}

/**
 * O17 «Расход ИИ». The requirement this file exists to pin: an unpriced model's `estimatedCost:
 * null` must render as words, never as `"0 ₽"` — a partial sum dressed up as a total is worse than
 * no total at all (docs/AI_QUOTAS.md §3).
 */
describe("ModelUsageTable", () => {
    it("renders 'нет цены' for a model with a null estimated cost, not '0 ₽'", () => {
        const models: AiSpendModelLine[] = [
            {
                model: "gpt-4o",
                kind: "llm",
                promptTokens: 612_340,
                completionTokens: 180_905,
                callCount: 1610,
                speechCharacters: 0,
                estimatedCost: null,
            },
        ];

        render(<ModelUsageTable models={models} currency="RUB" />);

        expect(screen.getByText("нет цены")).toBeTruthy();
        expect(screen.queryByText("0 ₽")).toBeNull();
        expect(screen.queryByText("0,00 ₽")).toBeNull();
    });

    it("renders a priced model's cost normally", () => {
        const models: AiSpendModelLine[] = [
            {
                model: "yandex-tts",
                kind: "tts",
                promptTokens: 0,
                completionTokens: 0,
                callCount: 231,
                speechCharacters: 418_220,
                estimatedCost: 543.68,
            },
        ];

        render(<ModelUsageTable models={models} currency="RUB" />);

        expect(screen.getByText("543,68 ₽")).toBeTruthy();
        expect(screen.getByText("231 вызов")).toBeTruthy();
    });

    it("shows an explanatory empty state for a fresh organization with no spend yet", () => {
        render(<ModelUsageTable models={[]} currency="RUB" />);

        expect(screen.getByText("В этом месяце расхода ещё не было")).toBeTruthy();
    });
});

describe("QuotaMeters", () => {
    it("draws no bar and says 'без лимита' when a limit is zero", () => {
        const report = buildReport({ llmMonthlyTokenLimit: 0 });
        render(<QuotaMeters report={report} />);

        expect(screen.getByText("без лимита")).toBeTruthy();
        expect(screen.queryAllByRole("progressbar", { name: "Токены модели" })).toHaveLength(0);
    });

    it("draws a bar for every metric with a configured limit", () => {
        const report = buildReport();
        render(<QuotaMeters report={report} />);

        expect(screen.getByRole("progressbar", { name: "Токены модели" })).toBeTruthy();
        expect(screen.getByRole("progressbar", { name: "Голос сегодня, мин" })).toBeTruthy();
        expect(screen.getByRole("progressbar", { name: "Голос за месяц, мин" })).toBeTruthy();
    });
});

describe("QuotaStateBanner", () => {
    it("renders nothing for the ok state", () => {
        const { container } = render(<QuotaStateBanner quotaState="ok" />);
        expect(container.firstChild).toBeNull();
    });

    it("explains that background generation paused while conversations kept working", () => {
        render(<QuotaStateBanner quotaState="batch_paused" />);
        expect(screen.getByText("Фоновая генерация приостановлена")).toBeTruthy();
        expect(screen.getByText(/Напишите вашему менеджеру Sellevate/)).toBeTruthy();
    });

    it("renders the exhausted state distinctly from batch_paused", () => {
        render(<QuotaStateBanner quotaState="exhausted" />);
        expect(screen.getByText("Лимит исчерпан")).toBeTruthy();
    });
});
