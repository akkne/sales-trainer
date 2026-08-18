"use client";

import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import type { AiUsageKind, AiUsageQuotaState } from "@/features/org-usage/constants/usage-dictionary";

export interface AiSpendModelLine {
    model: string;
    kind: AiUsageKind;
    promptTokens: number;
    completionTokens: number;
    callCount: number;
    speechCharacters: number;
    /** Null when this model has no entry in the price table — never a stand-in `0`. */
    estimatedCost: number | null;
}

/**
 * `GET /admin/ai-usage` (docs/AI_QUOTAS.md §8, `AiSpendReportDto`). Organization-scoped for a
 * `TenancyAdmin`; the one route this screen calls, and it takes no parameters at all — always the
 * current UTC calendar month, so there is no period selector on O17.
 */
export interface AiSpendReport {
    periodKey: string;
    currency: string;
    quotaState: AiUsageQuotaState;
    llmPromptTokens: number;
    llmCompletionTokens: number;
    llmTotalTokens: number;
    llmMonthlyTokenLimit: number;
    llmCallCount: number;
    /** Not rendered on its own — an estimate-derived count next to the real one would only confuse. */
    llmEstimatedCallCount: number;
    speechCharacters: number;
    voiceUsedMinutesToday: number;
    voiceDailyLimitMinutes: number;
    voiceUsedMinutesThisMonth: number;
    voiceMonthlyLimitMinutes: number;
    /** Null as soon as any model used this month is unpriced — see `formatTotalCost`. */
    estimatedCost: number | null;
    hasUnpricedModels: boolean;
    models: AiSpendModelLine[];
}

/**
 * This month's AI spend for the caller's organization. No arguments: the endpoint has none, and a
 * period selector would imply a choice the backend does not offer.
 */
export function useAiUsageReport() {
    return useQuery<AiSpendReport>({
        queryKey: ["org", "ai-usage"],
        queryFn: () => apiClient.get<AiSpendReport>("/admin/ai-usage"),
        staleTime: 30_000,
    });
}
