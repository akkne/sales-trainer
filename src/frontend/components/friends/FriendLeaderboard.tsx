"use client";

import { Icon } from "@/components/ui/Icon";
import { useFriendLeaderboard } from "@/lib/hooks/useFriends";

const RANK_ICONS: Record<number, { icon: string; color: string }> = {
    1: { icon: "emoji_events", color: "text-[#FFC800]" },
    2: { icon: "emoji_events", color: "text-[#C0C0C0]" },
    3: { icon: "emoji_events", color: "text-[#CD7F32]" },
};

export function FriendLeaderboard() {
    const { data: leaderboard, isLoading } = useFriendLeaderboard();

    if (isLoading) {
        return (
            <div className="flex flex-col gap-3">
                {[1, 2, 3].map((index) => (
                    <div key={index} className="h-16 rounded-2xl bg-surface-container animate-pulse" />
                ))}
            </div>
        );
    }

    if (!leaderboard || leaderboard.length === 0) {
        return (
            <div className="bg-surface-container rounded-2xl px-5 py-8 text-center">
                <div className="w-12 h-12 rounded-full bg-surface-container-high flex items-center justify-center mx-auto mb-3">
                    <Icon name="leaderboard" size="lg" className="text-on-surface-variant" />
                </div>
                <p className="text-sm font-semibold text-on-surface-variant">
                    Добавь друзей, чтобы соревноваться
                </p>
            </div>
        );
    }

    return (
        <div className="flex flex-col gap-2">
            {leaderboard.map((entry) => {
                const rankStyle = RANK_ICONS[entry.rank];
                return (
                    <div
                        key={entry.userId}
                        className={`flex items-center gap-3 px-4 py-3 rounded-2xl tonal-transition ${
                            entry.isCurrentUser
                                ? "bg-primary-container border-l-4 border-primary"
                                : "bg-surface-container"
                        }`}
                    >
                        <div className="w-8 text-center shrink-0">
                            {rankStyle ? (
                                <Icon name={rankStyle.icon} size="md" className={rankStyle.color} />
                            ) : (
                                <span className="font-bold text-on-surface-variant text-sm">
                                    {entry.rank}
                                </span>
                            )}
                        </div>

                        <div className="w-9 h-9 rounded-full bg-primary flex items-center justify-center text-on-primary font-bold text-sm shrink-0">
                            {entry.displayName[0]?.toUpperCase()}
                        </div>

                        <div className="flex-1 min-w-0">
                            <p className={`font-semibold text-sm truncate ${
                                entry.isCurrentUser ? "text-primary" : "text-on-surface"
                            }`}>
                                {entry.displayName}
                                {entry.isCurrentUser && " (ты)"}
                            </p>
                        </div>

                        <span className="flex items-center gap-1 text-sm font-bold text-primary shrink-0">
                            <Icon name="bolt" size="sm" />
                            {entry.totalXpAmount}
                        </span>
                    </div>
                );
            })}
        </div>
    );
}
