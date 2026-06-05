"use client";

import Link from "next/link";
import type { ChatConversationSummary } from "@/features/friends/hooks/use-chat";

interface ConversationCardProps {
    conversation: ChatConversationSummary;
    isActive?: boolean;
    onSelect?: (conversationId: string) => void;
}

function formatConversationTime(dateString: string | null): string {
    if (!dateString) return "";
    const date = new Date(dateString);
    const now = new Date();
    const diffDays = Math.floor((now.getTime() - date.getTime()) / 86400000);

    if (diffDays === 0) {
        return date.toLocaleTimeString("ru-RU", { hour: "2-digit", minute: "2-digit" });
    }
    if (diffDays === 1) return "вчера";
    if (diffDays < 7) return `${diffDays} д назад`;
    return date.toLocaleDateString("ru-RU", { day: "numeric", month: "short" });
}

export function ConversationCard({ conversation, isActive, onSelect }: ConversationCardProps) {
    const activeClasses = isActive
        ? "bg-primary-container text-on-primary-container"
        : "bg-surface-container hover:bg-surface-container-high";

    const content = (
        <>
            <div className="w-11 h-11 rounded-full bg-primary flex items-center justify-center text-on-primary font-bold text-sm shrink-0">
                {conversation.friendDisplayName[0]?.toUpperCase()}
            </div>

            <div className="flex-1 min-w-0 text-left">
                <p className="font-semibold text-sm truncate">
                    {conversation.friendDisplayName}
                </p>
                {conversation.lastMessagePreview && (
                    <p className="text-xs text-on-surface-variant truncate mt-0.5">
                        {conversation.lastMessagePreview}
                    </p>
                )}
            </div>

            {conversation.lastMessageAt && (
                <span className="text-[10px] text-on-surface-variant shrink-0">
                    {formatConversationTime(conversation.lastMessageAt)}
                </span>
            )}
        </>
    );

    if (onSelect) {
        return (
            <button
                type="button"
                onClick={() => onSelect(conversation.conversationId)}
                className={`w-full flex items-center gap-3 px-4 py-3 rounded-2xl tonal-transition ${activeClasses}`}
            >
                {content}
            </button>
        );
    }

    return (
        <Link
            href={`/friends/chat/${conversation.conversationId}`}
            className={`flex items-center gap-3 px-4 py-3 rounded-2xl tonal-transition ${activeClasses}`}
        >
            {content}
        </Link>
    );
}
