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
            <h3 className="h4" style={{ marginBottom: 8 }}>Найди своего первого напарника!</h3>
            <p className="small" style={{ marginBottom: 18 }}>
                Тренируйтесь вместе и соревнуйтесь в таблице лидеров среди друзей
            </p>
            {onSearchFocus && (
                <button onClick={onSearchFocus} className="btn btn-primary">
                    <Icon name="search" size={16} />
                    Найти друзей
                </button>
            )}
        </div>
    );
}
