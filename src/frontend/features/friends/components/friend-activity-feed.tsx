"use client";

import { Icon } from "@/shared/components/icon";
import type { IconName } from "@/shared/components/icon";
import { useFriendActivity } from "@/features/friends/hooks/use-friends";

const ACTIVITY_CONFIG: Record<string, { icon: IconName; color: string }> = {
    earned_xp: { icon: "bolt", color: "var(--primary)" },
    completed_lesson: { icon: "check", color: "var(--primary)" },
    streak_milestone: { icon: "flame", color: "var(--flame)" },
};

function formatRelativeTime(dateString: string): string {
    const now = new Date();
    const date = new Date(dateString);
    const diffMs = now.getTime() - date.getTime();
    const diffMinutes = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMinutes < 1) return "только что";
    if (diffMinutes < 60) return `${diffMinutes} мин назад`;
    if (diffHours < 24) return `${diffHours} ч назад`;
    if (diffDays < 7) return `${diffDays} д назад`;
    return date.toLocaleDateString("ru-RU", { day: "numeric", month: "short" });
}

export function FriendActivityFeed() {
    const { data: activities, isLoading } = useFriendActivity();

    if (isLoading) {
        return (
            <>
                <span className="eyebrow muted">Активность друзей</span>
                <div className="col gap-2" style={{ marginTop: 14 }}>
                    {[1, 2, 3].map((index) => (
                        <div key={index} style={{ height: 40, borderRadius: 12, background: "var(--surface-2)" }} />
                    ))}
                </div>
            </>
        );
    }

    if (!activities || activities.length === 0) {
        return (
            <>
                <span className="eyebrow muted">Активность друзей</span>
                <p className="small" style={{ marginTop: 14 }}>Пока тихо. Активность друзей появится здесь.</p>
            </>
        );
    }

    return (
        <>
            <span className="eyebrow muted">Активность друзей</span>
            <div className="col" style={{ marginTop: 14 }}>
                {activities.slice(0, 10).map((activity, index) => {
                    const config = ACTIVITY_CONFIG[activity.activityType] ?? {
                        icon: "info" as IconName,
                        color: "var(--ink-3)",
                    };

                    return (
                        <div
                            key={`${activity.userId}-${activity.occurredAt}-${index}`}
                            className="act-row"
                        >
                            <span
                                className="itile"
                                style={{ width: 32, height: 32, background: "var(--surface-2)", color: config.color }}
                            >
                                <Icon name={config.icon} size={17} />
                            </span>
                            <span className="grow small">
                                <b style={{ color: "var(--ink)" }}>{activity.displayName}</b>{" "}
                                {activity.description}
                            </span>
                            <span className="small" style={{ color: "var(--ink-4)", flexShrink: 0 }}>
                                {formatRelativeTime(activity.occurredAt)}
                            </span>
                        </div>
                    );
                })}
            </div>
        </>
    );
}
