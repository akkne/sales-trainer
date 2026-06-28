"use client";

import Link from "next/link";
import { GeoAvatar } from "@/shared/components/geo-avatar";
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
        return date.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" });
    }
    if (diffDays === 1) return "yesterday";
    if (diffDays < 7) return `${diffDays} d ago`;
    return date.toLocaleDateString("en-GB", { day: "numeric", month: "short" });
}

export function ConversationCard({ conversation, isActive, onSelect }: ConversationCardProps) {
    const activeClasses = isActive
        ? "bg-ink text-bg border-ink"
        : "bg-surface border border-line hover:bg-bg-2";

    const nameClass = isActive ? "text-bg" : "text-ink";
    const previewClass = isActive ? "text-bg opacity-70" : "text-ink-4";
    const timeClass = isActive ? "text-bg opacity-70" : "text-ink-4";

    const content = (
        <>
            <GeoAvatar seed={conversation.friendDisplayName} size={44} />

            <div className="flex-1 min-w-0 text-left">
                <p className={`font-medium text-sm truncate ${nameClass}`}>
                    {conversation.friendDisplayName}
                </p>
                {conversation.lastMessagePreview && (
                    <p className={`text-xs truncate mt-0.5 ${previewClass}`}>
                        {conversation.lastMessagePreview}
                    </p>
                )}
            </div>

            {conversation.lastMessageAt && (
                <span className={`text-[10px] font-mono shrink-0 ${timeClass}`}>
                    {formatConversationTime(conversation.lastMessageAt)}
                </span>
            )}
        </>
    );

    const shared = `w-full flex items-center gap-3 px-4 py-3 rounded-2xl transition-colors border ${activeClasses}`;
    const shadow = isActive ? { boxShadow: "var(--sh-2)" } : { boxShadow: "var(--sh-1)" };

    if (onSelect) {
        return (
            <button
                type="button"
                onClick={() => onSelect(conversation.conversationId)}
                className={shared}
                style={shadow}
            >
                {content}
            </button>
        );
    }

    return (
        <Link
            href={`/friends/chat/${conversation.conversationId}`}
            className={shared}
            style={shadow}
        >
            {content}
        </Link>
    );
}
