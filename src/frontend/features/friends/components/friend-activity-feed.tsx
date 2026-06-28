"use client";

import { useFriendActivity } from "@/features/friends/hooks/use-friends";
import { Icon } from "@/shared/components/icon";

function formatRelativeTime(dateString: string): string {
    const now = new Date();
    const date = new Date(dateString);
    const diffMs = now.getTime() - date.getTime();
    const diffMinutes = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMinutes < 1) return "just now";
    if (diffMinutes < 60) return `${diffMinutes} min`;
    if (diffHours < 24) return `${diffHours} h`;
    if (diffDays < 7) return `${diffDays} d`;
    return date.toLocaleDateString("en-GB", { day: "numeric", month: "short" });
}

/** Derive initials from a display name for the activity avatar */
function initials(name: string): string {
    const parts = name.trim().split(/\s+/);
    if (parts.length >= 2) return (parts[0][0] + parts[1][0]).toUpperCase();
    return name.slice(0, 2).toUpperCase();
}

export function FriendActivityFeed() {
    const { data: activities, isLoading } = useFriendActivity();

    return (
        <>
            {/* Rail header */}
            <div className="frd-rail-head">
                <p className="frd-rail-title">Activity</p>
            </div>

            <div className="frd-activity-scroll">
                {isLoading ? (
                    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                        {[1, 2, 3, 4].map((i) => (
                            <div
                                key={i}
                                className="frd-skeleton"
                                style={{ height: 40, borderRadius: 10 }}
                            />
                        ))}
                    </div>
                ) : !activities || activities.length === 0 ? (
                    <div className="frd-empty">
                        <div className="frd-empty-icon">
                            <Icon name="users" size={18} />
                        </div>
                        <p className="frd-empty-title">Nothing yet</p>
                        <p className="frd-empty-sub">
                            Your friends' activity will appear here
                        </p>
                    </div>
                ) : (
                    activities.slice(0, 15).map((activity, index) => (
                        <div
                            key={`${activity.userId}-${activity.occurredAt}-${index}`}
                            className="frd-act-row"
                        >
                            <div className="frd-act-avatar">
                                {initials(activity.displayName)}
                            </div>
                            <span className="frd-act-body">
                                <b>{activity.displayName}</b> {activity.description}
                            </span>
                            <span className="frd-act-time">
                                {formatRelativeTime(activity.occurredAt)}
                            </span>
                        </div>
                    ))
                )}
            </div>
        </>
    );
}
