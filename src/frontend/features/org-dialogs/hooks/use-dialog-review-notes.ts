"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import type {
    DialogReviewKind,
    DialogReviewStatus,
    ScoreDisputeOutcome,
} from "@/features/org-dialogs/constants/dialog-review-dictionary";

const REVIEW_NOTES_STALE_TIME_MILLISECONDS = 30_000;

/**
 * `DialogReviewNoteDto` (learning-service). The manager's half of the product mirrors the same DTO
 * in `features/dialog-reviews/hooks/use-dialog-reviews.ts`; both are copies of one server record
 * seen from opposite ends, and the panel keeps its own so a slice of block 40.20 never reaches
 * into the manager app's feature folder.
 */
export interface DialogReviewNote {
    id: string;
    kind: DialogReviewKind;
    status: DialogReviewStatus;
    sessionId: string;
    dialogModeKey: string;
    /** Whose conversation it is — the manager, on either kind of note. */
    subjectUserId: string;
    subjectDisplayName: string | null;
    /** Who wrote the row: the РОП on a coaching note, the manager on a dispute. */
    authorUserId: string;
    authorDisplayName: string | null;
    quotedFromMessageIndex: number | null;
    quotedToMessageIndex: number | null;
    quotedText: string | null;
    comment: string;
    /** The contested grade, frozen at write time, on learning-service's 0–100 scale. */
    disputedScore: number | null;
    resolution: string | null;
    /** Recorded, never applied to the conversation's grade — see `ADJUSTED_SCORE_DISCLAIMER`. */
    adjustedScore: number | null;
    resolvedBy: string | null;
    resolvedAt: string | null;
    createdAt: string;
    updatedAt: string;
}

const ORGANIZATION_REVIEW_NOTES_QUERY_KEY = ["org", "dialog-reviews"] as const;

/**
 * Every review note of the organization, both kinds, in one read.
 *
 * O7 has three tabs and the endpoint has `kind` and `status` parameters, and this still asks for
 * the unfiltered list. Two reasons, both about the same absence: the route does not paginate
 * (`GET /admin/dialog-reviews` returns the whole array whatever is asked of it), so a filtered read
 * is no cheaper; and the notification link `/admin/dialog-reviews?note={id}` names a note without
 * saying which queue it is in, and there is no by-id route to ask. One list makes the deep link
 * resolvable and the tab counts exact instead of guessed.
 */
export function useOrganizationReviewNotes() {
    return useQuery<DialogReviewNote[]>({
        queryKey: ORGANIZATION_REVIEW_NOTES_QUERY_KEY,
        queryFn: () => apiClient.get<DialogReviewNote[]>("/admin/dialog-reviews"),
        staleTime: REVIEW_NOTES_STALE_TIME_MILLISECONDS,
    });
}

/**
 * The notes already written about one conversation — what O6 shows under «Уже отправлено», and
 * where an open dispute on that conversation becomes visible before the РОП writes a fourth note
 * about it.
 */
export function useSessionReviewNotes(sessionId: string) {
    return useQuery<DialogReviewNote[]>({
        queryKey: ["org", "dialog-reviews", "session", sessionId],
        queryFn: () =>
            apiClient.get<DialogReviewNote[]>(
                `/admin/dialog-reviews?sessionId=${encodeURIComponent(sessionId)}`
            ),
        enabled: sessionId.length > 0,
        staleTime: REVIEW_NOTES_STALE_TIME_MILLISECONDS,
    });
}

export interface CreateCoachingNoteInput {
    sessionId: string;
    quotedFromMessageIndex: number | null;
    quotedToMessageIndex: number | null;
    quotedText: string;
    comment: string;
}

/**
 * «РОП выделяет фрагмент диалога, комментирует, отправляет менеджеру».
 *
 * The body names the conversation and nothing about who held it: the manager, the scenario and the
 * grade are read server-side out of the score row, which is what makes it impossible to address a
 * note at another organization's employee.
 */
export function useCreateCoachingNote() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (input: CreateCoachingNoteInput) =>
            apiClient.post<DialogReviewNote>("/admin/dialog-reviews", input),
        onSuccess: (note) => {
            void queryClient.invalidateQueries({
                queryKey: ["org", "dialog-reviews", "session", note.sessionId],
            });
            void queryClient.invalidateQueries({ queryKey: ORGANIZATION_REVIEW_NOTES_QUERY_KEY });
        },
    });
}

export interface ResolveScoreDisputeInput {
    noteId: string;
    outcome: ScoreDisputeOutcome;
    /** Required by the server on a rejection, and omitted rather than sent empty otherwise. */
    resolution: string | null;
    /** Allowed only on an upheld dispute, 0–100. */
    adjustedScore: number | null;
}

/**
 * The verdict. Both outcomes notify the manager — a dispute closed in silence rebuilds the black
 * box the mechanism exists to open (ASSIGNMENTS.md §4.1).
 */
export function useResolveScoreDispute() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: ({ noteId, ...verdict }: ResolveScoreDisputeInput) =>
            apiClient.post<DialogReviewNote>(`/admin/dialog-reviews/${noteId}/resolve`, verdict),
        onSuccess: (note) => {
            void queryClient.invalidateQueries({ queryKey: ORGANIZATION_REVIEW_NOTES_QUERY_KEY });
            void queryClient.invalidateQueries({
                queryKey: ["org", "dialog-reviews", "session", note.sessionId],
            });
            // The sidebar counter is «сколько открытых споров ждёт вас» (ADMIN_UI_DESIGN.md §1.6),
            // and a verdict is the one action in the panel that changes it. Its key belongs to
            // `features/org-shell/hooks/use-org-nav-badges.ts`.
            void queryClient.invalidateQueries({
                queryKey: ["org", "nav-badges", "score-disputes"],
            });
        },
    });
}
