"use client";

import { Icon } from "@/components/ui/Icon";

interface EmptyFriendsStateProps {
    onSearchFocus?: () => void;
}

export function EmptyFriendsState({ onSearchFocus }: EmptyFriendsStateProps) {
    return (
        <div
            className="bg-surface border border-line rounded-2xl px-6 py-10 text-center"
            style={{ boxShadow: "var(--sh-1)" }}
        >
            <div
                className="w-16 h-16 rounded-2xl flex items-center justify-center mx-auto mb-4"
                style={{ background: "var(--indigo-soft)" }}
            >
                <Icon name="users" size="2xl" className="text-indigo" />
            </div>
            <h3 className="font-medium text-lg text-ink mb-2">
                Найди первого напарника!
            </h3>
            <p className="text-sm text-ink-3 mb-5 max-w-xs mx-auto">
                Тренируйтесь вместе и соревнуйтесь в рейтинге друзей
            </p>
            {onSearchFocus && (
                <button
                    onClick={onSearchFocus}
                    className="inline-flex items-center gap-2 px-5 py-3 rounded-xl text-white font-medium text-sm transition-opacity hover:opacity-90"
                    style={{ background: "var(--indigo)", boxShadow: "var(--sh-2)" }}
                >
                    <Icon name="search" size="sm" />
                    Найти друзей
                </button>
            )}
        </div>
    );
}
