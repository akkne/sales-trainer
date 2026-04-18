"use client";

import { useEffect, useRef } from "react";
import { Icon } from "@/components/ui/Icon";
import {
    useChatMessages,
    useSendChatMessage,
    useConversations,
} from "@/lib/hooks/useChat";
import { ChatBubble } from "./ChatBubble";
import { ChatInput } from "./ChatInput";

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
        <div className="flex flex-col h-full min-h-0 bg-surface rounded-2xl overflow-hidden border border-outline-variant">
            <div className="flex items-center gap-3 px-4 py-3 border-b border-outline-variant bg-surface-container-low shrink-0">
                {onBack && (
                    <button
                        type="button"
                        onClick={onBack}
                        className="p-1 rounded-full hover:bg-surface-container tonal-transition"
                        aria-label="Назад"
                    >
                        <Icon name="arrow_back" size="md" className="text-on-surface" />
                    </button>
                )}

                <div className="w-9 h-9 rounded-full bg-primary flex items-center justify-center text-on-primary font-bold text-sm shrink-0">
                    {currentConversation?.friendDisplayName?.[0]?.toUpperCase() ?? "?"}
                </div>

                <h2 className="font-semibold text-on-surface truncate">
                    {currentConversation?.friendDisplayName ?? "Чат"}
                </h2>
            </div>

            <div className="flex-1 min-h-0 overflow-y-auto px-4 py-4 flex flex-col gap-2">
                {messagesLoading ? (
                    <div className="flex items-center justify-center flex-1">
                        <div className="w-8 h-8 rounded-full border-3 border-primary border-t-transparent animate-spin" />
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
                        <p className="text-sm text-on-surface-variant">
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
