import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

export interface DialogBundle {
    id: string;
    skillId: string;
    skillSlug: string;
    skillTitle: string;
    title: string;
    description: string;
    iconEmoji: string;
    sortOrder: number;
    isActive: boolean;
}

export interface DialogMode {
    id: string;
    bundleId: string;
    key: string;
    title: string;
    description: string;
    sortOrder: number;
    isActive: boolean;
    voiceEnabled: boolean;
}

export interface DialogMessage {
    role: "assistant" | "user";
    content: string;
    timestamp: string;
    isStopSignal: boolean;
}

export interface DialogFeedback {
    summary: string;
    content: string;
    generatedAt: string;
    xpEarned: number;
}

export interface DialogSession {
    id: string;
    bundleId: string;
    modeId: string;
    status: "active" | "completed" | "abandoned";
    messages: DialogMessage[];
    feedback: DialogFeedback | null;
    xpEarned: number;
    createdAt: string;
    completedAt: string | null;
}

export interface DialogSessionSummary {
    id: string;
    bundleId: string;
    modeId: string;
    modeTitle: string;
    bundleTitle: string;
    status: "active" | "completed" | "abandoned";
    messageCount: number;
    xpEarned: number;
    createdAt: string;
    completedAt: string | null;
}

export function useDialogBundles() {
    return useQuery({
        queryKey: ["dialog", "bundles"],
        queryFn: () => apiClient.get<DialogBundle[]>("/dialog/bundles"),
    });
}

export function useDialogModes(bundleId: string) {
    return useQuery({
        queryKey: ["dialog", "bundles", bundleId, "modes"],
        queryFn: () => apiClient.get<DialogMode[]>(`/dialog/bundles/${bundleId}/modes`),
        enabled: !!bundleId,
    });
}

export function useDialogSessions() {
    return useQuery({
        queryKey: ["dialog", "sessions"],
        queryFn: () => apiClient.get<DialogSessionSummary[]>("/dialog/sessions"),
    });
}

export function useDialogSession(sessionId: string | null) {
    return useQuery({
        queryKey: ["dialog", "sessions", sessionId],
        queryFn: () => apiClient.get<DialogSession>(`/dialog/sessions/${sessionId}`),
        enabled: !!sessionId,
    });
}

export async function startDialogSession(bundleId: string, modeId: string): Promise<DialogSession> {
    return apiClient.post<DialogSession>("/dialog/sessions", { bundleId, modeId });
}

export async function sendDialogMessage(sessionId: string, content: string): Promise<DialogMessage> {
    return apiClient.post<DialogMessage>(`/dialog/sessions/${sessionId}/messages`, { content });
}

/**
 * Completes the session and returns AI feedback. Returns null when the call
 * had no user messages (backend responds 204) — nothing was evaluated and
 * no feedback should be shown.
 */
export async function completeDialogSession(sessionId: string): Promise<DialogFeedback | null> {
    const feedback = await apiClient.post<DialogFeedback | undefined>(`/dialog/sessions/${sessionId}/complete`, {});
    return feedback ?? null;
}

export async function deleteDialogSession(sessionId: string): Promise<void> {
    return apiClient.delete(`/dialog/sessions/${sessionId}`);
}
