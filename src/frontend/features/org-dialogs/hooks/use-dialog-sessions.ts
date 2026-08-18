"use client";

import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import {
    buildDialogSessionQueryPath,
    type DialogSessionFilters,
} from "@/features/org-dialogs/lib/dialog-session-query";

const DIALOG_SESSIONS_STALE_TIME_MILLISECONDS = 30_000;

/**
 * `AdminDialogSessionSummaryDto` (ai-service, `GET /admin/dialog-sessions`).
 *
 * `id` is a Mongo identifier and therefore a string, not a guid — everything else that identifies
 * something here is a guid, which is exactly why it is worth saying once in the type.
 */
export interface DialogSessionSummary {
    id: string;
    userId: string;
    bundleId: string;
    modeId: string;
    /** Null for a scenario the caller cannot see — a retired global mode, say. The row still counts. */
    modeKey: string | null;
    modeTitle: string | null;
    status: string;
    messageCount: number;
    /** ai-service's 0–10 grade. Convert with `toPanelScore` before showing it. */
    score: number | null;
    feedbackSummary: string | null;
    assignmentId: string | null;
    createdAt: string;
    completedAt: string | null;
}

/**
 * The team's graded conversations, newest first.
 *
 * Only graded ones exist on this route — the repository filters `Feedback != null` — so the empty
 * result of an unfiltered read means «команда ещё не провела ни одного оценённого разговора» and
 * not «ничего не нашлось».
 */
export function useDialogSessions(filters: DialogSessionFilters) {
    return useQuery<DialogSessionSummary[]>({
        queryKey: [
            "org",
            "dialog-sessions",
            filters.userId,
            filters.modeId,
            filters.maximumPanelScore,
            filters.limit,
        ],
        queryFn: () =>
            apiClient.get<DialogSessionSummary[]>(buildDialogSessionQueryPath(filters)),
        staleTime: DIALOG_SESSIONS_STALE_TIME_MILLISECONDS,
    });
}
