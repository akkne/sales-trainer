"use client";

import { Icon, IconName } from "@/components/ui/Icon";
import { useFriendActivity } from "@/lib/hooks/useFriends";

const ACTIVITY_CONFIG: Record<string, { icon: IconName; color: string }> = {
    earned_achievement: { icon: "trophy", color: "text-olive" },
    earned_xp: { icon: "bolt", color: "text-indigo" },
    completed_lesson: { icon: "check", color: "text-indigo" },
    streak_milestone: { icon: "flame", color: "text-rust" },
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
            <div className="flex flex-col gap-2">
                {[1, 2, 3].map((index) => (
                    <div key={index} className="h-12 rounded-xl bg-surface animate-pulse" />
                ))}
            </div>
        );
    }

    if (!activities || activities.length === 0) {
        return null;
    }

    return (
        <div className="flex flex-col gap-1">
            <h3 className="text-xs font-mono tracking-[1px] uppercase text-ink-4 mb-2">
                Активность друзей
            </h3>
            {activities.slice(0, 10).map((activity, index) => {
                const config = ACTIVITY_CONFIG[activity.activityType] ?? {
                    icon: "info",
                    color: "text-ink-3",
                };

                return (
                    <div
                        key={`${activity.userId}-${activity.occurredAt}-${index}`}
                        className="flex items-center gap-3 px-3 py-2 rounded-xl"
                    >
                        <div className="w-8 h-8 rounded-xl bg-bg-2 flex items-center justify-center shrink-0">
                            <Icon name={config.icon} size="sm" className={config.color} />
                        </div>
                        <div className="flex-1 min-w-0">
                            <p className="text-xs text-ink-3">
                                <span className="font-medium text-ink">{activity.displayName}</span>{" "}
                                {activity.description}
                            </p>
                        </div>
                        <span className="text-[10px] text-ink-4 font-mono shrink-0">
                            {formatRelativeTime(activity.occurredAt)}
                        </span>
                    </div>
                );
            })}
        </div>
    );
}
