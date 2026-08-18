"use client";

import { useQuery } from "@tanstack/react-query";
import { apiClient, ApiError } from "@/shared/api/api-client";

const TRANSCRIPT_STALE_TIME_MILLISECONDS = 60_000;

/** `AdminDialogTranscriptMessageDto`. `index` is the server's, and a quote points at it. */
export interface DialogTranscriptMessage {
    index: number;
    /** `DialogMessageRoles`: `user` is the manager being trained, `assistant` is the client. */
    role: string;
    content: string;
    timestamp: string;
}

/** `DialogFeedbackDto`. `score` here is ai-service's 0–10 grade, as on the summary. */
export interface DialogTranscriptFeedback {
    summary: string;
    content: string;
    score: number;
    generatedAt: string;
}

/** `AdminDialogTranscriptDto` (ai-service, `GET /admin/dialog-sessions/{sessionId}`). */
export interface DialogTranscript {
    id: string;
    userId: string;
    bundleId: string;
    modeId: string;
    modeKey: string | null;
    modeTitle: string | null;
    status: string;
    score: number | null;
    feedback: DialogTranscriptFeedback | null;
    assignmentId: string | null;
    createdAt: string;
    completedAt: string | null;
    messages: DialogTranscriptMessage[];
}

/**
 * One conversation in full.
 *
 * A 404 is a final answer — the conversation is not this organization's, or no longer exists — so
 * it is not retried. Everything else keeps react-query's default retries: ai-service is a separate
 * process from the learning-service half of this screen and a blip on one must not close the other.
 */
export function useDialogTranscript(sessionId: string) {
    return useQuery<DialogTranscript>({
        queryKey: ["org", "dialog-transcript", sessionId],
        queryFn: () => apiClient.get<DialogTranscript>(`/admin/dialog-sessions/${sessionId}`),
        enabled: sessionId.length > 0,
        staleTime: TRANSCRIPT_STALE_TIME_MILLISECONDS,
        retry: (failureCount, error) =>
            !(error instanceof ApiError && error.status === 404) && failureCount < 2,
    });
}

/** Whether a failed transcript read means «нет такого разговора» rather than «не смогли прочитать». */
export function isTranscriptNotFound(error: unknown): boolean {
    return error instanceof ApiError && error.status === 404;
}
