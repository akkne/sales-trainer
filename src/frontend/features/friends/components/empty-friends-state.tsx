"use client";

import { Icon } from "@/shared/components/icon";

interface EmptyFriendsStateProps {
    onSearchFocus?: () => void;
}

export function EmptyFriendsState({ onSearchFocus }: EmptyFriendsStateProps) {
    return (
        <div className="empty">
            <div className="ic">
                <Icon name="users" size="lg" />
            </div>
            <h3 className="h4" style={{ marginBottom: 8 }}>Find your first partner!</h3>
            <p className="small" style={{ marginBottom: 18 }}>
                Practice together and compete on the friends leaderboard
            </p>
            {onSearchFocus && (
                <button onClick={onSearchFocus} className="btn btn-primary">
                    <Icon name="search" size={16} />
                    Find friends
                </button>
            )}
        </div>
    );
}
