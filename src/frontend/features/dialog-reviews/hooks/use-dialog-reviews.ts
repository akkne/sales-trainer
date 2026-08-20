"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { toast } from "@/features/notifications/store/toast-store";

/** Phase 40.25. Mirrors learning-service's `DialogReviewKinds`. */
export type DialogReviewKind = "coaching_note" | "score_dispute";

/** Phase 40.25. Mirrors learning-service's `DialogReviewStatuses`. */
export type DialogReviewStatus = "open" | "acknowledged" | "upheld" | "rejected";

/**
 * Phase 40.25. One annotation on one graded conversation — either the РОП coaching this person on
 * a fragment of it, or this person's own disputed score with the verdict once it arrives.
 */
export interface DialogReviewNote {
    id: string;
    kind: DialogReviewKind;
    status: DialogReviewStatus;
    sessionId: string;
    dialogModeKey: string;
    subjectUserId: string;
    subjectDisplayName: string | null;
    authorUserId: string;
    authorDisplayName: string | null;
    quotedFromMessageIndex: number | null;
    quotedToMessageIndex: number | null;
    quotedText: string | null;
    comment: string;
    disputedScore: number | null;
    resolution: string | null;
    adjustedScore: number | null;
    resolvedBy: string | null;
    resolvedAt: string | null;
    createdAt: string;
    updatedAt: string;
}

const REVIEWS_QUERY_KEY = ["dialog-reviews", "mine"] as const;

/**
 * Phase 40.25. Coaching notes addressed to the caller and disputes they filed, newest first.
 *
 * One list rather than two, matching the server: both are "the conversation about my conversations",
 * and a manager who has to look in two places will look in neither.
 */
export function useDialogReviews() {
    return useQuery<DialogReviewNote[]>({
        queryKey: REVIEWS_QUERY_KEY,
        queryFn: () => apiClient.get<DialogReviewNote[]>("/dialog-reviews"),
        staleTime: 30_000,
    });
}

/**
 * Phase 40.25. «Менеджер оспаривает оценку ИИ».
 *
 * The request names only the conversation: who held it, on which scenario and with what grade are
 * read server-side from the recorded score, so nothing here can address a dispute at somebody
 * else's call.
 */
export function useDisputeScore() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (input: {
            sessionId: string;
            comment: string;
            quotedText?: string | null;
            quotedFromMessageIndex?: number | null;
            quotedToMessageIndex?: number | null;
        }) => apiClient.post<DialogReviewNote>("/dialog-reviews/disputes", input),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: REVIEWS_QUERY_KEY });
        },
    });
}

/** Phase 40.25. "I have read this." Idempotent server-side, so a double click is harmless. */
export function useAcknowledgeCoachingNote() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (noteId: string) =>
            apiClient.post<DialogReviewNote>(`/dialog-reviews/${noteId}/acknowledge`, {}),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: REVIEWS_QUERY_KEY });
        },
        onError: (error) => {
            toast.error(`Не удалось отметить как прочитанное: ${(error as Error).message}`);
        },
    });
}

/**
 * Phase 40.25. The verdict in the words a manager reads, or null while the row is still open.
 *
 * Returns null for an unknown status rather than guessing, the same rule
 * `describeCompletionRule` follows: a client that invents a sentence for a status it does not know
 * puts a wrong claim on the screen of the person the status is about.
 */
export function describeReviewStatus(note: DialogReviewNote): string | null {
    if (note.kind === "coaching_note") {
        return note.status === "acknowledged" ? "Прочитано" : null;
    }

    if (note.status === "upheld") {
        return note.adjustedScore !== null
            ? `РОП согласился: должно было быть ${note.adjustedScore} из 100`
            : "РОП согласился с вами";
    }

    if (note.status === "rejected") {
        return "Оценка оставлена прежней";
    }

    if (note.status === "open") {
        return "Ждёт рассмотрения";
    }

    return null;
}
