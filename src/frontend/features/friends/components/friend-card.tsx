"use client";

import Link from "next/link";
import { Icon } from "@/shared/components/icon";
import { GeoAvatar } from "@/shared/components/geo-avatar";
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
        <div className="card card-pad lift friend-row">
            <Link href={`/friends/${friend.userId}`} style={{ flexShrink: 0 }} aria-label={friend.displayName}>
                <GeoAvatar seed={friend.displayName} size={48} />
            </Link>

            <Link href={`/friends/${friend.userId}`} className="grow" style={{ minWidth: 0, textDecoration: "none", color: "inherit" }}>
                <div className="h4" style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                    {friend.displayName}
                </div>
                {friend.persona && (
                    <div className="small" style={{ color: "var(--ink-4)" }}>
                        {PERSONA_LABELS[friend.persona] ?? friend.persona}
                    </div>
                )}
                <div className="row gap-3 small" style={{ marginTop: 3 }}>
                    <span className="row gap-1">
                        <Icon name="bolt" size={14} style={{ color: "var(--primary)" }} />
                        <span className="num">{friend.totalXpAmount.toLocaleString("ru")}</span>
                    </span>
                    {friend.currentStreakDayCount > 0 && (
                        <span className="row gap-1">
                            <Icon name="flame" size={14} style={{ color: "var(--flame)" }} />
                            <span className="num">{friend.currentStreakDayCount}</span>
                        </span>
                    )}
                    <span className="row gap-1">
                        <Icon name="trophy" size={14} style={{ color: "var(--amber)" }} />
                        <span className="num">{friend.achievementCount}</span>
                    </span>
                </div>
            </Link>

            <button
                onClick={() => onChatClick(friend.userId)}
                className="icon-btn"
                aria-label="Написать"
            >
                <Icon name="message" size={18} />
            </button>
        </div>
    );
}
