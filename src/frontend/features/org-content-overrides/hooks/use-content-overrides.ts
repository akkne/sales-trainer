"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import type {
    AdminDialogMode,
    ContentOverrideReview,
    ContentOverrideSummary,
    DialogModeOverrideReview,
    DialogModeOverrideSummary,
    LearningOverrideKind,
} from "../types/content-override";

const OVERRIDES_STALE_TIME_MILLISECONDS = 60_000;

export const OVERRIDES_QUERY_KEY = ["org", "content", "overrides"] as const;
export const DIALOG_MODE_OVERRIDES_QUERY_KEY = ["org", "content", "overrides", "modes"] as const;

/**
 * The whole list, not `?staleOnly=true`.
 *
 * The screen needs both counters («Только устаревшие 3» / «Все 8») and the endpoint does not
 * paginate, so one read answers both and the filter is a client-side partition of rows the server
 * already flagged. Nothing about staleness is computed here — `isStale` arrives per row.
 */
export function useContentOverrides() {
    return useQuery<ContentOverrideSummary[]>({
        queryKey: OVERRIDES_QUERY_KEY,
        queryFn: () => apiClient.get<ContentOverrideSummary[]>("/admin/content/overrides"),
        staleTime: OVERRIDES_STALE_TIME_MILLISECONDS,
    });
}

/**
 * The prompt overrides, from a different service. Kept a separate query on purpose: when ai-service
 * is down the table still shows the three learning kinds and says so, instead of failing whole.
 */
export function useDialogModeOverrides() {
    return useQuery<DialogModeOverrideSummary[]>({
        queryKey: DIALOG_MODE_OVERRIDES_QUERY_KEY,
        queryFn: () => apiClient.get<DialogModeOverrideSummary[]>("/admin/dialog/overrides/modes"),
        staleTime: OVERRIDES_STALE_TIME_MILLISECONDS,
    });
}

export function useContentOverrideReview(kind: LearningOverrideKind, overrideId: string) {
    return useQuery<ContentOverrideReview>({
        queryKey: [...OVERRIDES_QUERY_KEY, kind, overrideId],
        queryFn: () =>
            apiClient.get<ContentOverrideReview>(
                `/admin/content/overrides/${kind}/${encodeURIComponent(overrideId)}`
            ),
    });
}

export function useDialogModeOverrideReview(overrideId: string) {
    return useQuery<DialogModeOverrideReview>({
        queryKey: [...DIALOG_MODE_OVERRIDES_QUERY_KEY, overrideId],
        queryFn: () =>
            apiClient.get<DialogModeOverrideReview>(
                `/admin/dialog/overrides/modes/${encodeURIComponent(overrideId)}`
            ),
    });
}

interface ReviewActionVariables {
    kind: LearningOverrideKind;
    overrideId: string;
}

/**
 * «Взять базу». The copy is archived, never deleted — progress rows point at it without a foreign
 * key. Answers 204, so every list has to be re-read.
 */
export function useAcceptBase() {
    const queryClient = useQueryClient();

    return useMutation<void, Error, ReviewActionVariables>({
        mutationFn: ({ kind, overrideId }) =>
            apiClient.post<void>(
                `/admin/content/overrides/${kind}/${encodeURIComponent(overrideId)}/accept-base`,
                {}
            ),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: OVERRIDES_QUERY_KEY });
        },
    });
}

/** «Оставить своё». Re-points the fork marker; the text is untouched. This records a decision. */
export function useKeepOverride() {
    const queryClient = useQueryClient();

    return useMutation<void, Error, ReviewActionVariables>({
        mutationFn: ({ kind, overrideId }) =>
            apiClient.post<void>(
                `/admin/content/overrides/${kind}/${encodeURIComponent(overrideId)}/keep-override`,
                {}
            ),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: OVERRIDES_QUERY_KEY });
        },
    });
}

export function useAcceptDialogModeBase() {
    const queryClient = useQueryClient();

    return useMutation<void, Error, string>({
        mutationFn: (overrideId) =>
            apiClient.post<void>(
                `/admin/dialog/overrides/modes/${encodeURIComponent(overrideId)}/accept-base`,
                {}
            ),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: DIALOG_MODE_OVERRIDES_QUERY_KEY });
        },
    });
}

export function useKeepDialogModeOverride() {
    const queryClient = useQueryClient();

    return useMutation<void, Error, string>({
        mutationFn: (overrideId) =>
            apiClient.post<void>(
                `/admin/dialog/overrides/modes/${encodeURIComponent(overrideId)}/keep-override`,
                {}
            ),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: DIALOG_MODE_OVERRIDES_QUERY_KEY });
        },
    });
}

export interface UpdateDialogModePromptsVariables {
    overrideId: string;
    chatSystemPrompt: string;
    feedbackSystemPrompt: string;
}

/**
 * The third action for a dialog mode. A mode has no versions and nothing to publish, so saving is
 * the whole act — and the service re-points the fork marker as part of it.
 *
 * `key` and `bundleId` are not sent: they are the override's link to the row it shadows and the
 * service refuses to move them.
 */
export function useUpdateDialogModePrompts() {
    const queryClient = useQueryClient();

    return useMutation<AdminDialogMode, Error, UpdateDialogModePromptsVariables>({
        mutationFn: ({ overrideId, chatSystemPrompt, feedbackSystemPrompt }) =>
            apiClient.put<AdminDialogMode>(
                `/admin/dialog/overrides/modes/${encodeURIComponent(overrideId)}`,
                { chatSystemPrompt, feedbackSystemPrompt }
            ),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: DIALOG_MODE_OVERRIDES_QUERY_KEY });
        },
    });
}

/**
 * Copy-on-write, and the only code path in either service that creates a copy. Idempotent: a second
 * press returns the copy that already exists.
 *
 * 409 means the row is already somebody's copy — for the lesson editor that answer is information,
 * not a failure (see `app/(org)/org/content/lessons/[lessonId]/page.tsx`).
 */
export function useCreateContentOverride() {
    const queryClient = useQueryClient();

    return useMutation<ContentOverrideSummary, Error, { kind: LearningOverrideKind; baseId: string }>({
        mutationFn: ({ kind, baseId }) =>
            apiClient.post<ContentOverrideSummary>(
                `/admin/content/overrides/${kind}/${encodeURIComponent(baseId)}`,
                {}
            ),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: OVERRIDES_QUERY_KEY });
        },
    });
}
