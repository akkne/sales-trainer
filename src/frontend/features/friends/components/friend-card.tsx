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
    // Online presence is not tracked by the backend — omit the live dot
    // (the status dot slot is reserved for when presence is available).
    const focusLine = friend.persona
        ? (PERSONA_LABELS[friend.persona] ?? friend.persona)
        : null;

    return (
        <div className="frd-card">
            {/* Top row: avatar + message button */}
            <div className="frd-card-top">
                <div className="frd-card-avatar-wrap">
                    <Link href={`/friends/${friend.userId}`} aria-label={friend.displayName}>
                        <GeoAvatar seed={friend.displayName} size={44} />
                    </Link>
                    {/* Status dot — omitted (no presence data); add .online/.offline when available */}
                </div>
                <button
                    className="frd-card-msg-btn"
                    onClick={() => onChatClick(friend.userId)}
                    aria-label={`Message ${friend.displayName}`}
                >
                    <Icon name="message" size={16} />
                </button>
            </div>

            {/* Name + focus line */}
            <Link
                href={`/friends/${friend.userId}`}
                style={{ textDecoration: "none", color: "inherit" }}
            >
                <p className="frd-card-name">{friend.displayName}</p>
                {focusLine && <p className="frd-card-sub">{focusLine}</p>}
            </Link>
        </div>
    );
}
