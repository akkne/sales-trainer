"use client";

import Link from "next/link";
import { Icon } from "@/shared/components/icon";
import type { Friend } from "@/features/friends/hooks/use-friends";

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
        <div className="bg-surface-container rounded-2xl p-4 flex items-center gap-4 tonal-transition hover:bg-surface-container-high">
            <Link
                href={`/friends/${friend.userId}`}
                className="w-12 h-12 rounded-full bg-primary flex items-center justify-center text-on-primary font-bold text-lg ring-2 ring-primary-container shrink-0"
            >
                {friend.displayName[0]?.toUpperCase()}
            </Link>

            <Link href={`/friends/${friend.userId}`} className="flex-1 min-w-0">
                <p className="font-semibold text-on-surface truncate">
                    {friend.displayName}
                </p>
                {friend.persona && (
                    <p className="text-xs text-on-surface-variant">
                        {PERSONA_LABELS[friend.persona] ?? friend.persona}
                    </p>
                )}
                <div className="flex items-center gap-3 mt-1">
                    <span className="flex items-center gap-0.5 text-xs text-on-surface-variant">
                        <Icon name="bolt" size="sm" className="text-primary" />
                        {friend.totalXpAmount} XP
                    </span>
                    {friend.currentStreakDayCount > 0 && (
                        <span className="flex items-center gap-0.5 text-xs text-on-surface-variant">
                            <Icon name="local_fire_department" size="sm" className="text-error" />
                            {friend.currentStreakDayCount}
                        </span>
                    )}
                    <span className="flex items-center gap-0.5 text-xs text-on-surface-variant">
                        <Icon name="emoji_events" size="sm" className="text-tertiary" />
                        {friend.achievementCount}
                    </span>
                </div>
            </Link>

            <button
                onClick={() => onChatClick(friend.userId)}
                className="p-2 rounded-full hover:bg-primary-container tonal-transition"
                aria-label="Написать"
            >
                <Icon name="chat" size="md" className="text-primary" />
            </button>
        </div>
    );
}
