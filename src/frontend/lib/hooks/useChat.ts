import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/apiClient";
import { clientLogger } from "@/lib/clientLogger";

export interface ChatConversationSummary {
    conversationId: string;
    friendUserId: string;
    friendDisplayName: string;
    lastMessagePreview: string | null;
    lastMessageAt: string | null;
}

export interface ChatMessageData {
    id: string;
    senderId: string;
    content: string;
    sentAt: string;
    isOwn: boolean;
}

export function useConversations() {
    return useQuery({
        queryKey: ["chatConversations"],
        queryFn: () => apiClient.get<ChatConversationSummary[]>("/chat/conversations"),
    });
}

export function useCreateConversation() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (friendUserId: string) =>
            apiClient.post<ChatConversationSummary>("/chat/conversations", { friendUserId }),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["chatConversations"] });
        },
        onError: (error) => {
            const message = (error as Error).message;
            clientLogger.warn("Failed to open chat", { error: message });
            if (typeof window !== "undefined") {
                window.alert(`Не удалось открыть чат: ${message}`);
            }
        },
    });
}

export function useChatMessages(conversationId: string) {
    return useQuery({
        queryKey: ["chatMessages", conversationId],
        queryFn: () => apiClient.get<ChatMessageData[]>(`/chat/conversations/${conversationId}/messages`),
        enabled: !!conversationId,
        refetchInterval: 5000,
    });
}

export function useSendChatMessage() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: ({ conversationId, content }: { conversationId: string; content: string }) =>
            apiClient.post<ChatMessageData>(`/chat/conversations/${conversationId}/messages`, { content }),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({ queryKey: ["chatMessages", variables.conversationId] });
            queryClient.invalidateQueries({ queryKey: ["chatConversations"] });
        },
        onError: (error, variables) => {
            const message = (error as Error).message;
            clientLogger.warn("Failed to send chat message", {
                conversationId: variables.conversationId,
                error: message,
            });
            if (typeof window !== "undefined") {
                window.alert(`Не удалось отправить сообщение: ${message}`);
            }
        },
    });
}
