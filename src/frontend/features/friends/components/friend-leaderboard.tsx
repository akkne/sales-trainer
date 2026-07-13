"use client";

import { Icon } from "@/shared/components/icon";
import { UserAvatar } from "@/shared/components/user-avatar";
import { useFriendLeaderboard } from "@/features/friends/hooks/use-friends";

export function FriendLeaderboard() {
    const { data: leaderboard, isLoading } = useFriendLeaderboard();

    if (isLoading) {
        return (
            <div className="col gap-2">
                {[1, 2, 3].map((index) => (
                    <div key={index} className="card" style={{ height: 64 }} />
                ))}
            </div>
        );
    }

    if (!leaderboard || leaderboard.length === 0) {
        return (
            <div className="empty">
                <div className="ic">
                    <Icon name="trophy" size="lg" />
                </div>
                <p className="small">Добавь друзей, чтобы соревноваться</p>
            </div>
        );
    }

    return (
        <div className="card lb-card">
            <div className="lb-head">
                <span>#</span>
                <span>Друг</span>
                <span>XP</span>
            </div>

            {leaderboard.map((entry) => (
                <div
                    key={entry.userId}
                    className={`lb-row${entry.isCurrentUser ? " you" : ""}`}
                >
                    <span className={`rank-badge r${Math.min(entry.rank, 4)}`}>
                        {String(entry.rank).padStart(2, "0")}
                    </span>
                    <div className="row gap-3 grow" style={{ minWidth: 0 }}>
                        <UserAvatar avatarUrl={entry.avatarUrl} seed={entry.displayName} size={36} circle />
                        <span className="lb-name">
                            {entry.displayName}
                            {entry.isCurrentUser && <span className="you-tag"> · ты</span>}
                        </span>
                    </div>
                    <span className="num lb-xp">{entry.totalXpAmount.toLocaleString("en")}</span>
                </div>
            ))}
        </div>
    );
}
