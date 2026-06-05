"use client";

import { Icon } from "@/shared/components/icon";
import { useConversations } from "@/features/friends/hooks/use-chat";
import { ConversationCard } from "./ConversationCard";
import { ChatWindow } from "./ChatWindow";

interface ChatsPaneProps {
    selectedConversationId: string | null;
    onSelectConversation: (conversationId: string | null) => void;
}

export function ChatsPane({ selectedConversationId, onSelectConversation }: ChatsPaneProps) {
    const { data: conversations, isLoading } = useConversations();

    const isEmpty = !isLoading && (!conversations || conversations.length === 0);

    return (
        <div className="grid md:grid-cols-[320px_1fr] gap-3 h-[calc(100dvh-16rem)] md:h-[calc(100dvh-14rem)] min-h-[500px]">
            {/* Conversations list */}
            <aside
                className={`flex flex-col min-h-0 ${
                    selectedConversationId ? "hidden md:flex" : "flex"
                }`}
            >
                {isLoading ? (
                    <div className="flex flex-col gap-2">
                        {[1, 2, 3].map((index) => (
                            <div key={index} className="h-16 rounded-2xl bg-surface-container animate-pulse" />
                        ))}
                    </div>
                ) : isEmpty ? (
                    <div className="bg-surface-container rounded-2xl px-5 py-8 text-center">
                        <div className="w-12 h-12 rounded-full bg-surface-container-high flex items-center justify-center mx-auto mb-3">
                            <Icon name="chat_bubble" size="lg" className="text-on-surface-variant" />
                        </div>
                        <p className="text-sm font-semibold text-on-surface-variant">
                            Нет сообщений
                        </p>
                        <p className="text-xs text-on-surface-variant mt-1">
                            Начни общение с другом!
                        </p>
                    </div>
                ) : (
                    <div className="flex flex-col gap-2 overflow-y-auto pr-1">
                        {conversations?.map((conversation) => (
                            <ConversationCard
                                key={conversation.conversationId}
                                conversation={conversation}
                                isActive={conversation.conversationId === selectedConversationId}
                                onSelect={onSelectConversation}
                            />
                        ))}
                    </div>
                )}
            </aside>

            {/* Chat window */}
            <section
                className={`min-h-0 ${
                    selectedConversationId ? "flex flex-col" : "hidden md:flex md:flex-col md:items-center md:justify-center"
                }`}
            >
                {selectedConversationId ? (
                    <ChatWindow
                        conversationId={selectedConversationId}
                        onBack={() => onSelectConversation(null)}
                    />
                ) : (
                    <div className="hidden md:flex flex-col items-center justify-center text-center p-8">
                        <div className="w-14 h-14 rounded-full bg-surface-container flex items-center justify-center mb-3">
                            <Icon name="chat" size="lg" className="text-on-surface-variant" />
                        </div>
                        <p className="text-sm font-semibold text-on-surface">
                            Выбери чат слева
                        </p>
                        <p className="text-xs text-on-surface-variant mt-1">
                            Или начни новый, нажав на иконку чата у друга
                        </p>
                    </div>
                )}
            </section>
        </div>
    );
}
