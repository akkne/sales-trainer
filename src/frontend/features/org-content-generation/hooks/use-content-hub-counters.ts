"use client";

import { useQueries } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { CONTENT_GENERATION_JOB_STATUSES } from "@/features/org-content-generation/constants/generation-dictionary";
import type {
    ContentAdaptationJobSummary,
    ContentGenerationJobSummary,
    ContentOverrideSummary,
} from "@/features/org-content-generation/types/content-generation";

const HUB_COUNTER_STALE_TIME_MILLISECONDS = 60_000;

export interface ContentQueueCounts {
    /** Runs sitting at the checkpoint — the only ones that need a person before money is spent. */
    awaitingReviewRunCount: number;
    /** Runs that were refused and are waiting for material or a typed-in structure. */
    insufficientRunCount: number;
    completedRunCount: number;
    totalRunCount: number;
    /** Proposals a person has to answer one at a time; there is no bulk-apply verb, by design. */
    awaitingReviewProposalCount: number;
    staleOverrideCount: number;
    totalOverrideCount: number;
}

export interface ContentHubCounters {
    counts: ContentQueueCounts;
    isLoading: boolean;
    /** True per queue when its own read failed — a card without numbers, never a broken screen. */
    hasRunCountFailure: boolean;
    hasAdaptationCountFailure: boolean;
    hasOverrideCountFailure: boolean;
}

const EMPTY_COUNTS: ContentQueueCounts = {
    awaitingReviewRunCount: 0,
    insufficientRunCount: 0,
    completedRunCount: 0,
    totalRunCount: 0,
    awaitingReviewProposalCount: 0,
    staleOverrideCount: 0,
    totalOverrideCount: 0,
};

/**
 * O9's three counters, and nothing else — the hub page reads no detail from any of the three
 * queues it points at.
 *
 * Three requests rather than one, because there is no aggregating endpoint and inventing one would
 * be backend work this slice does not own. Each failure is contained: a queue whose count could not
 * be read shows its card with the explanation and no number, because a card reading «0 ждёт
 * проверки» when the truth is «мы не смогли спросить» sends somebody away from work that is there.
 */
export function useContentHubCounters(): ContentHubCounters {
    const results = useQueries({
        queries: [
            {
                queryKey: ["org", "content-generation", "list", "all"],
                queryFn: () =>
                    apiClient.get<ContentGenerationJobSummary[]>("/admin/content-generation"),
                staleTime: HUB_COUNTER_STALE_TIME_MILLISECONDS,
                retry: false,
            },
            {
                queryKey: ["org", "content-adaptations", "list", "all"],
                queryFn: () =>
                    apiClient.get<ContentAdaptationJobSummary[]>("/admin/content/adaptations"),
                staleTime: HUB_COUNTER_STALE_TIME_MILLISECONDS,
                retry: false,
            },
            {
                queryKey: ["org", "content-overrides", "list", "all"],
                queryFn: () => apiClient.get<ContentOverrideSummary[]>("/admin/content/overrides"),
                staleTime: HUB_COUNTER_STALE_TIME_MILLISECONDS,
                retry: false,
            },
        ],
    });

    const [runs, adaptations, overrides] = results;

    const runSummaries = runs.data ?? [];
    const adaptationSummaries = adaptations.data ?? [];
    const overrideSummaries = overrides.data ?? [];

    return {
        counts: {
            ...EMPTY_COUNTS,
            awaitingReviewRunCount: runSummaries.filter(
                (run) => run.status === CONTENT_GENERATION_JOB_STATUSES.awaitingReview
            ).length,
            insufficientRunCount: runSummaries.filter(
                (run) => run.status === CONTENT_GENERATION_JOB_STATUSES.insufficient
            ).length,
            completedRunCount: runSummaries.filter(
                (run) => run.status === CONTENT_GENERATION_JOB_STATUSES.completed
            ).length,
            totalRunCount: runSummaries.length,
            awaitingReviewProposalCount: adaptationSummaries.reduce(
                (total, batch) => total + batch.awaitingReviewCount,
                0
            ),
            staleOverrideCount: overrideSummaries.filter((override) => override.isStale).length,
            totalOverrideCount: overrideSummaries.length,
        },
        isLoading: results.some((result) => result.isLoading),
        hasRunCountFailure: runs.isError,
        hasAdaptationCountFailure: adaptations.isError,
        hasOverrideCountFailure: overrides.isError,
    };
}
