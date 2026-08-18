"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import type {
    ContentAdaptationItem,
    ContentAdaptationJob,
    ContentAdaptationJobSummary,
    StartContentAdaptationRequest,
} from "@/features/org-content-adaptation/types/adaptation";
import { shouldPollJob } from "@/features/org-content-adaptation/utils/proposal-queue";

/**
 * The seven org-admin routes of `AdminContentAdaptationController` (Phase 40.32), and nothing else.
 *
 * There is no bulk mutation in this file because there is no bulk route: accept and reject each
 * take one item id, and that is the block the whole feature exists to keep
 * (docs/CONTENT_PIPELINE.md §6a).
 */

const ADAPTATION_LIST_STALE_TIME_MILLISECONDS = 30_000;

/**
 * A batch answers a few exercises per tick — four, by `ContentAdaptation:MaximumItemsPerTick` — so
 * five seconds is the interval at which a person watching the queue fill sees it move. React Query
 * suspends interval refetching while the tab is in the background, which is exactly what a poll
 * over a paid pipeline should do.
 */
const PREPARING_POLL_INTERVAL_MILLISECONDS = 5_000;

export const ADAPTATION_LIST_QUERY_KEY = ["org", "content-adaptations"] as const;

export function adaptationJobQueryKey(jobId: string) {
    return ["org", "content-adaptation", jobId] as const;
}

export function adaptationItemQueryKey(jobId: string, itemId: string) {
    return ["org", "content-adaptation", jobId, "item", itemId] as const;
}

/**
 * `GET /admin/content/adaptations`.
 *
 * The whole array arrives: the route takes `mode` and `status`, but it does not paginate, and the
 * two tabs of O12 are a split of one list rather than two requests that could disagree about how
 * many batches exist.
 */
export function useContentAdaptationJobs() {
    return useQuery<ContentAdaptationJobSummary[]>({
        queryKey: ADAPTATION_LIST_QUERY_KEY,
        queryFn: () => apiClient.get<ContentAdaptationJobSummary[]>("/admin/content/adaptations"),
        staleTime: ADAPTATION_LIST_STALE_TIME_MILLISECONDS,
    });
}

/**
 * `GET /admin/content/adaptations/{jobId}` — the batch and its queue, polled while the sweep still
 * owns it.
 */
export function useContentAdaptationJob(jobId: string) {
    return useQuery<ContentAdaptationJob>({
        queryKey: adaptationJobQueryKey(jobId),
        queryFn: () => apiClient.get<ContentAdaptationJob>(`/admin/content/adaptations/${jobId}`),
        enabled: jobId.length > 0,
        retry: false,
        refetchInterval: (query) =>
            query.state.data !== undefined && shouldPollJob(query.state.data.summary.status)
                ? PREPARING_POLL_INTERVAL_MILLISECONDS
                : false,
    });
}

/**
 * `GET /admin/content/adaptations/{jobId}/items/{itemId}` — both documents and the leaf list for one
 * proposal. Fetched per item rather than with the queue: sixty exercise bodies is not a list payload.
 */
export function useContentAdaptationItem(jobId: string, itemId: string | null) {
    return useQuery<ContentAdaptationItem>({
        queryKey: adaptationItemQueryKey(jobId, itemId ?? ""),
        queryFn: () =>
            apiClient.get<ContentAdaptationItem>(
                `/admin/content/adaptations/${jobId}/items/${itemId}`
            ),
        enabled: jobId.length > 0 && itemId !== null && itemId.length > 0,
        retry: false,
    });
}

/** `POST /admin/content/adaptations` — spends nothing; the scope is one database query. */
export function useStartContentAdaptation() {
    const queryClient = useQueryClient();

    return useMutation<ContentAdaptationJob, unknown, StartContentAdaptationRequest>({
        mutationFn: (request) =>
            apiClient.post<ContentAdaptationJob>("/admin/content/adaptations", request),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: ADAPTATION_LIST_QUERY_KEY });
        },
    });
}

function useQueueInvalidation(jobId: string) {
    const queryClient = useQueryClient();

    return (itemId: string) => {
        void queryClient.invalidateQueries({ queryKey: adaptationJobQueryKey(jobId) });
        void queryClient.invalidateQueries({ queryKey: adaptationItemQueryKey(jobId, itemId) });
        void queryClient.invalidateQueries({ queryKey: ADAPTATION_LIST_QUERY_KEY });
    };
}

/**
 * `POST …/items/{itemId}/accept` — the only call in the whole block that writes an exercise, and it
 * carries one item id. Accepting a rewrite of a global exercise forks the lesson first (40.18
 * copy-on-write): the organization gets its own copy, and nobody else's curriculum moves.
 */
export function useAcceptAdaptationItem(jobId: string) {
    const invalidateQueue = useQueueInvalidation(jobId);

    return useMutation<ContentAdaptationItem, unknown, string>({
        mutationFn: (itemId) =>
            apiClient.post<ContentAdaptationItem>(
                `/admin/content/adaptations/${jobId}/items/${itemId}/accept`,
                {}
            ),
        onSuccess: (item) => invalidateQueue(item.summary.id),
    });
}

/** `POST …/items/{itemId}/reject` — touches no content and remembers nothing beyond the item row. */
export function useRejectAdaptationItem(jobId: string) {
    const invalidateQueue = useQueueInvalidation(jobId);

    return useMutation<ContentAdaptationItem, unknown, string>({
        mutationFn: (itemId) =>
            apiClient.post<ContentAdaptationItem>(
                `/admin/content/adaptations/${jobId}/items/${itemId}/reject`,
                {}
            ),
        onSuccess: (item) => invalidateQueue(item.summary.id),
    });
}

/** `POST …/retry` — re-queues the items that burned their attempts, and only those. */
export function useRetryContentAdaptation(jobId: string) {
    const queryClient = useQueryClient();

    return useMutation<ContentAdaptationJob, unknown, void>({
        mutationFn: () =>
            apiClient.post<ContentAdaptationJob>(`/admin/content/adaptations/${jobId}/retry`, {}),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: adaptationJobQueryKey(jobId) });
            void queryClient.invalidateQueries({ queryKey: ADAPTATION_LIST_QUERY_KEY });
        },
    });
}
