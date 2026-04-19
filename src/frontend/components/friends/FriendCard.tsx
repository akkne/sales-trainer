"use client";

import Link from "next/link";
import { Icon } from "@/components/ui/Icon";
import type { Friend } from "@/lib/hooks/useFriends";

const PERSONA_LABELS: Record<string, string> = {
    sdr: "SDR",
    account_executive: "Account Executive",
    account_manager: "Account Manager",
    founder: "Founder",
    other: "Other",
};

interface FriendCardProps {
    friend: Friend;
    onChatClick: (friendUserId: string) => void;
}

export function FriendCard({ friend, onChatClick }: FriendCardProps) {
    return (
        <div
            className="bg-surface border border-line rounded-2xl p-4 flex items-center gap-4 transition-all hover:border-line-2"
            style={{ boxShadow: "var(--sh-1)" }}
        >
            <Link
                href={`/friends/${friend.userId}`}
                className="w-12 h-12 rounded-xl flex items-center justify-center text-white font-medium text-lg shrink-0"
                style={{ background: "var(--indigo)" }}
            >
                {friend.displayName[0]?.toUpperCase()}
            </Link>

            <Link href={`/friends/${friend.userId}`} className="flex-1 min-w-0">
                <p className="font-medium text-ink truncate">
                    {friend.displayName}
                </p>
                {friend.persona && (
                    <p className="text-xs text-ink-4">
                        {PERSONA_LABELS[friend.persona] ?? friend.persona}
                    </p>
                )}
                <div className="flex items-center gap-3 mt-1.5">
                    <span className="flex items-center gap-1 text-xs text-ink-3">
                        <Icon name="bolt" size="sm" className="text-indigo" />
                        <span className="font-mono">{friend.totalXpAmount}</span>
                    </span>
                    {friend.currentStreakDayCount > 0 && (
                        <span className="flex items-center gap-1 text-xs text-ink-3">
                            <Icon name="flame" size="sm" className="text-rust" />
                            <span className="font-mono">{friend.currentStreakDayCount}</span>
                        </span>
                    )}
                    <span className="flex items-center gap-1 text-xs text-ink-3">
                        <Icon name="trophy" size="sm" className="text-olive" />
                        <span className="font-mono">{friend.achievementCount}</span>
                    </span>
                </div>
            </Link>

            <button
                onClick={() => onChatClick(friend.userId)}
                className="p-2.5 rounded-xl transition-colors hover:bg-indigo-soft"
                style={{ color: "var(--indigo)" }}
                aria-label="Написать"
            >
                <Icon name="message" size="md" />
            </button>
        </div>
    );
}
