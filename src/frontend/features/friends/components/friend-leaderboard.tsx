"use client";

import { Icon } from "@/shared/components/icon";
import { useFriendLeaderboard } from "@/features/friends/hooks/use-friends";

const RANK_STYLES: Record<number, { bg: string; color: string }> = {
    1: { bg: "var(--rust)", color: "white" },
    2: { bg: "var(--ink-3)", color: "white" },
    3: { bg: "var(--clay)", color: "white" },
};

export function FriendLeaderboard() {
    const { data: leaderboard, isLoading } = useFriendLeaderboard();

    if (isLoading) {
        return (
            <div className="flex flex-col gap-2">
                {[1, 2, 3].map((index) => (
                    <div key={index} className="h-16 rounded-2xl bg-surface animate-pulse" />
                ))}
            </div>
        );
    }

    if (!leaderboard || leaderboard.length === 0) {
        return (
            <div
                className="bg-surface border border-line rounded-2xl px-5 py-8 text-center"
                style={{ boxShadow: "var(--sh-1)" }}
            >
                <div className="w-12 h-12 rounded-xl bg-bg-2 flex items-center justify-center mx-auto mb-3">
                    <Icon name="trophy" size="lg" className="text-ink-4" />
                </div>
                <p className="text-sm font-medium text-ink-3">
                    Добавь друзей, чтобы соревноваться
                </p>
            </div>
        );
    }

    return (
        <div
            className="bg-surface border border-line rounded-2xl overflow-hidden"
            style={{ boxShadow: "var(--sh-1)" }}
        >
            {/* Header */}
            <div className="grid grid-cols-[3rem_1fr_auto] items-center px-4 py-3 bg-bg-2 border-b border-line">
                <span className="text-[10px] font-mono tracking-[1px] uppercase text-ink-4">#</span>
                <span className="text-[10px] font-mono tracking-[1px] uppercase text-ink-4">Друг</span>
                <span className="text-[10px] font-mono tracking-[1px] uppercase text-ink-4 text-right">XP</span>
            </div>

            {/* Rows */}
            {leaderboard.map((entry) => {
                const rankStyle = RANK_STYLES[entry.rank];
                return (
                    <div
                        key={entry.userId}
                        className={`grid grid-cols-[3rem_1fr_auto] items-center px-4 py-3 gap-3 border-b border-line/50 transition-colors ${
                            entry.isCurrentUser ? "bg-indigo-soft" : ""
                        }`}
                        style={entry.isCurrentUser ? { borderLeft: "3px solid var(--indigo)" } : undefined}
                    >
                        {/* Rank */}
                        <div
                            className="w-8 h-8 rounded-lg flex items-center justify-center font-mono font-medium text-xs"
                            style={
                                rankStyle
                                    ? { background: rankStyle.bg, color: rankStyle.color }
                                    : { background: "var(--bg-2)", color: "var(--ink-3)" }
                            }
                        >
                            {String(entry.rank).padStart(2, "0")}
                        </div>

                        {/* User */}
                        <div className="flex items-center gap-3 min-w-0">
                            <div
                                className={`w-9 h-9 rounded-xl flex items-center justify-center text-sm font-medium shrink-0 ${
                                    entry.isCurrentUser
                                        ? "bg-indigo text-white"
                                        : "bg-bg-2 text-ink-3"
                                }`}
                            >
                                {entry.displayName[0]?.toUpperCase()}
                            </div>
                            <div className="min-w-0">
                                <p
                                    className={`font-medium text-sm truncate ${
                                        entry.isCurrentUser ? "text-indigo" : "text-ink"
                                    }`}
                                >
                                    {entry.displayName}
                                    {entry.isCurrentUser && (
                                        <span className="text-ink-4 font-normal"> (ты)</span>
                                    )}
                                </p>
                            </div>
                        </div>

                        {/* XP */}
                        <span
                            className={`font-mono font-medium text-sm tabular-nums ${
                                entry.isCurrentUser ? "text-indigo" : "text-ink-2"
                            }`}
                        >
                            {entry.totalXpAmount}
                        </span>
                    </div>
                );
            })}
        </div>
    );
}
