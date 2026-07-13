"use client";

import { useEffect, useRef } from "react";
import { Icon } from "@/shared/components/icon";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import {
    useChatMessages,
    useSendChatMessage,
    useConversations,
} from "@/features/friends/hooks/use-chat";
import { RailChatBubble } from "./chat-bubble";
import { RailChatInput } from "./chat-input";

// ─── Rail chat view (330px right rail, V2 design) ───────────────────────────

interface RailChatViewProps {
    conversationId: string;
    onBack: () => void;
}

export function RailChatView({ conversationId, onBack }: RailChatViewProps) {
    const messagesEndRef = useRef<HTMLDivElement>(null);

    // useChatMessages already has refetchInterval for live-feel polling — preserved as-is.
    const { data: messages, isLoading } = useChatMessages(conversationId);
    const sendMutation = useSendChatMessage();
    const { data: conversations } = useConversations();

    const conversation = conversations?.find(
        (c) => c.conversationId === conversationId,
    );

    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    }, [messages?.length]);

    function handleSend(content: string) {
        sendMutation.mutate({ conversationId, content });
    }

    return (
        <>
            {/* Rail header */}
            <div className="frd-rail-head">
                <button
                    className="frd-rail-back"
                    onClick={onBack}
                    aria-label="Назад к активности"
                >
                    <Icon name="arrow-left" size={16} />
                </button>

                {conversation && (
                    <GeoAvatar seed={conversation.friendDisplayName} size={30} />
                )}

                <div style={{ flex: 1, minWidth: 0 }}>
                    <p
                        className="frd-rail-title"
                        style={{ fontSize: 14, marginBottom: 0 }}
                    >
                        {conversation?.friendDisplayName ?? "Чат"}
                    </p>
                    {/* Online status omitted — presence not tracked by backend */}
                </div>
            </div>

            {/* Message thread */}
            <div className="frd-chat-msgs">
                {isLoading ? (
                    <div
                        style={{
                            flex: 1,
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                        }}
                    >
                        <div
                            style={{
                                width: 24,
                                height: 24,
                                borderRadius: "50%",
                                border: "2px solid var(--primary)",
                                borderTopColor: "transparent",
                                animation: "spin 0.8s linear infinite",
                            }}
                        />
                    </div>
                ) : messages && messages.length > 0 ? (
                    <>
                        {messages.map((message) => (
                            <RailChatBubble key={message.id} message={message} />
                        ))}
                        <div ref={messagesEndRef} />
                    </>
                ) : (
                    <div
                        style={{
                            flex: 1,
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                        }}
                    >
                        <p
                            style={{
                                fontSize: 13,
                                color: "var(--ink-4)",
                                textAlign: "center",
                            }}
                        >
                            Отправь первое сообщение!
                        </p>
                    </div>
                )}
            </div>

            {/* Composer */}
            <RailChatInput
                onSend={handleSend}
                disabled={sendMutation.isPending}
            />
        </>
    );
}

// ─── Legacy full-page ChatWindow (kept for chats-pane / deep-link routes) ───

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
                            <RailChatBubble key={message.id} message={message} />
                        ))}
                        <div ref={messagesEndRef} />
                    </>
                ) : (
                    <div className="flex items-center justify-center flex-1">
                        <p className="text-sm text-ink-4">
                            Отправь первое сообщение!
                        </p>
                    </div>
                )}
            </div>

            <RailChatInput
                onSend={handleSendMessage}
                disabled={sendMessageMutation.isPending}
            />
        </div>
    );
}
