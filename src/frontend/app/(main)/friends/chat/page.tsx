"use client";

import { Icon } from "@/components/ui/Icon";
import { useConversations } from "@/lib/hooks/useChat";
import { ConversationCard } from "@/components/friends/ConversationCard";

export default function ConversationsPage() {
    const { data: conversations, isLoading } = useConversations();

    return (
        <div className="max-w-2xl mx-auto px-4 py-6">
            <h1 className="font-headline font-bold text-2xl text-on-surface mb-5">
                Сообщения
            </h1>

            {isLoading ? (
                <div className="flex flex-col gap-3">
                    {[1, 2, 3].map((index) => (
                        <div key={index} className="h-16 rounded-2xl bg-surface-container animate-pulse" />
                    ))}
                </div>
            ) : conversations && conversations.length > 0 ? (
                <div className="flex flex-col gap-2">
                    {conversations.map((conversation) => (
                        <ConversationCard
                            key={conversation.conversationId}
                            conversation={conversation}
                        />
                    ))}
                </div>
            ) : (
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
            )}
        </div>
    );
}
