"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { resolveJobPollInterval } from "@/features/org-content-generation/utils/job-state";
import type {
    ContentGenerationJob,
    ContentGenerationJobSummary,
    ContentStructure,
    StartContentGenerationRequest,
} from "@/features/org-content-generation/types/content-generation";

const JOB_LIST_STALE_TIME_MILLISECONDS = 15_000;

const buildJobListQueryKey = (status: string | null) =>
    ["org", "content-generation", "list", status ?? "all"] as const;

const buildJobQueryKey = (jobId: string) =>
    ["org", "content-generation", "job", jobId] as const;

/**
 * `GET /admin/content-generation?status=` — the runs, newest first, without their material or their
 * structure. The refusal **is** on the summary, which is what lets O10 print the first gap in the
 * row instead of sending somebody inside for every refused run.
 */
export function useContentGenerationJobs(status: string | null) {
    return useQuery<ContentGenerationJobSummary[]>({
        queryKey: buildJobListQueryKey(status),
        queryFn: () =>
            apiClient.get<ContentGenerationJobSummary[]>(
                status
                    ? `/admin/content-generation?status=${encodeURIComponent(status)}`
                    : "/admin/content-generation"
            ),
        staleTime: JOB_LIST_STALE_TIME_MILLISECONDS,
    });
}

/**
 * `document.hidden`, as state. Polling behind a background tab buys nothing and keeps a connection
 * warm for a run nobody is watching; the `visibilitychange` listener is what resumes it the moment
 * the РОП comes back — which is the whole scenario, because this is an operation people leave.
 */
function useIsDocumentHidden(): boolean {
    const [isDocumentHidden, setIsDocumentHidden] = useState(false);

    useEffect(() => {
        const readVisibility = () => setIsDocumentHidden(document.hidden);
        readVisibility();

        document.addEventListener("visibilitychange", readVisibility);
        return () => document.removeEventListener("visibilitychange", readVisibility);
    }, []);

    return isDocumentHidden;
}

/**
 * `GET /admin/content-generation/{jobId}` — one run with its structure. Polled every three seconds
 * while a worker owns it and not at all otherwise: there is no SSE and no websocket in the
 * contract, and both halves are minutes long.
 */
export function useContentGenerationJob(jobId: string) {
    const isDocumentHidden = useIsDocumentHidden();

    return useQuery<ContentGenerationJob>({
        queryKey: buildJobQueryKey(jobId),
        queryFn: () =>
            apiClient.get<ContentGenerationJob>(
                `/admin/content-generation/${encodeURIComponent(jobId)}`
            ),
        enabled: jobId.length > 0,
        refetchInterval: (query) =>
            resolveJobPollInterval(query.state.data?.status, isDocumentHidden),
        refetchIntervalInBackground: false,
        retry: false,
    });
}

/**
 * `POST /admin/content-generation`. Answers with a run in `structuring` — or straight in
 * `insufficient`, which is not an error and is why the caller navigates to the run rather than
 * staying to celebrate.
 */
export function useStartContentGeneration() {
    const queryClient = useQueryClient();

    return useMutation<ContentGenerationJob, Error, StartContentGenerationRequest>({
        mutationFn: (request) =>
            apiClient.post<ContentGenerationJob>("/admin/content-generation", request),
        onSuccess: (job) => {
            queryClient.setQueryData(buildJobQueryKey(job.id), job);
            void queryClient.invalidateQueries({
                queryKey: ["org", "content-generation", "list"],
            });
        },
    });
}

/**
 * Everything a mutation on one run has in common: the response **is** the run, so the cache is set
 * from it rather than invalidated into a second read, and the list is marked stale because a run
 * that just changed state changes its row too.
 */
function useJobMutationCacheUpdate(jobId: string) {
    const queryClient = useQueryClient();

    return (job: ContentGenerationJob) => {
        queryClient.setQueryData(buildJobQueryKey(jobId), job);
        void queryClient.invalidateQueries({ queryKey: ["org", "content-generation", "list"] });
    };
}

/**
 * `PUT …/structure` — the reviewer's edit, replaced wholesale rather than patched, and idempotent,
 * which is what makes a debounced autosave safe. Allowed at the checkpoint and on a refused run;
 * on a refused run the result is re-inspected, so the response may come back `awaiting_review`.
 */
export function useUpdateJobStructure(jobId: string) {
    const applyJobToCache = useJobMutationCacheUpdate(jobId);

    return useMutation<ContentGenerationJob, Error, ContentStructure>({
        mutationFn: (structure) =>
            apiClient.put<ContentGenerationJob>(
                `/admin/content-generation/${encodeURIComponent(jobId)}/structure`,
                structure
            ),
        onSuccess: applyJobToCache,
    });
}

/**
 * `POST …/material` — «вот ещё материал». Appends and resumes; the next structuring call reads only
 * the added part, so arguing with a refusal does not re-pay for the deck already read.
 */
export function useSupplementJobMaterial(jobId: string) {
    const applyJobToCache = useJobMutationCacheUpdate(jobId);

    return useMutation<ContentGenerationJob, Error, string>({
        mutationFn: (material) =>
            apiClient.post<ContentGenerationJob>(
                `/admin/content-generation/${encodeURIComponent(jobId)}/material`,
                { material }
            ),
        onSuccess: applyJobToCache,
    });
}

/**
 * `POST …/approve` — «всё верно», the only transition no worker can make and the door into the paid
 * half. A 409 carrying an `insufficiency` means the server re-inspected and moved the run to
 * `insufficient` **before** answering, so the failure path re-reads rather than arguing.
 */
export function useApproveJob(jobId: string) {
    const queryClient = useQueryClient();
    const applyJobToCache = useJobMutationCacheUpdate(jobId);

    return useMutation<ContentGenerationJob, Error, void>({
        mutationFn: () =>
            apiClient.post<ContentGenerationJob>(
                `/admin/content-generation/${encodeURIComponent(jobId)}/approve`,
                {}
            ),
        onSuccess: applyJobToCache,
        onError: () => {
            void queryClient.invalidateQueries({ queryKey: buildJobQueryKey(jobId) });
        },
    });
}

/** `POST …/retry` — puts a failed run back into the half it failed in, never back to the start. */
export function useRetryJob(jobId: string) {
    const applyJobToCache = useJobMutationCacheUpdate(jobId);

    return useMutation<ContentGenerationJob, Error, void>({
        mutationFn: () =>
            apiClient.post<ContentGenerationJob>(
                `/admin/content-generation/${encodeURIComponent(jobId)}/retry`,
                {}
            ),
        onSuccess: applyJobToCache,
    });
}
