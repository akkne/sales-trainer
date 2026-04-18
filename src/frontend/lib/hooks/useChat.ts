import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/apiClient";

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
    });
}
