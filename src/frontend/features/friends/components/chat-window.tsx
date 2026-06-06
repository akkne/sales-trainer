"use client";

import { useEffect, useRef } from "react";
import { Icon } from "@/shared/components/icon";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import {
    useChatMessages,
    useSendChatMessage,
    useConversations,
} from "@/features/friends/hooks/use-chat";
import { ChatBubble } from "./chat-bubble";
import { ChatInput } from "./chat-input";

interface ChatWindowProps {
    conversationId: string;
    onBack?: () => void;
}

export function ChatWindow({ conversationId, onBack }: ChatWindowProps) {
    const messagesEndRef = useRef<HTMLDivElement>(null);

    const { data: messages, isLoading: messagesLoading } = useChatMessages(conversationId);
    const sendMessageMutation = useSendChatMessage();
    const { data: conversations } = useConversations();

    const currentConversation = conversations?.find(
        (conversation) => conversation.conversationId === conversationId,
    );

    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    }, [messages?.length]);

    function handleSendMessage(content: string) {
        sendMessageMutation.mutate({ conversationId, content });
    }

    return (
        <div
            className="flex flex-col h-full min-h-0 bg-surface rounded-2xl overflow-hidden border border-line"
            style={{ boxShadow: "var(--sh-1)" }}
        >
            <div className="flex items-center gap-3 px-4 py-3 border-b border-line bg-surface shrink-0">
                {onBack && (
                    <button
                        type="button"
                        onClick={onBack}
                        className="p-1 rounded-full hover:bg-bg-2 transition-colors"
                        aria-label="Назад"
                    >
                        <Icon name="arrow-left" size="md" className="text-ink" />
                    </button>
                )}

                <GeoAvatar
                    seed={currentConversation?.friendDisplayName ?? "?"}
                    size={36}
                />

                <h2 className="font-medium text-ink truncate">
                    {currentConversation?.friendDisplayName ?? "Чат"}
                </h2>
            </div>

            <div className="flex-1 min-h-0 overflow-y-auto px-4 py-4 flex flex-col gap-2">
                {messagesLoading ? (
                    <div className="flex items-center justify-center flex-1">
                        <div
                            className="w-8 h-8 rounded-full border-2 border-t-transparent animate-spin"
                            style={{ borderColor: "var(--ink)", borderTopColor: "transparent" }}
                        />
                    </div>
                ) : messages && messages.length > 0 ? (
                    <>
                        {messages.map((message) => (
                            <ChatBubble key={message.id} message={message} />
                        ))}
                        <div ref={messagesEndRef} />
                    </>
                ) : (
                    <div className="flex items-center justify-center flex-1">
                        <p className="text-sm text-ink-4">
                            Напишите первое сообщение!
                        </p>
                    </div>
                )}
            </div>

            <ChatInput
                onSend={handleSendMessage}
                disabled={sendMessageMutation.isPending}
            />
        </div>
    );
}
