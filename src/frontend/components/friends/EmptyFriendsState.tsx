"use client";

import { Icon } from "@/components/ui/Icon";

interface EmptyFriendsStateProps {
    onSearchFocus?: () => void;
}

export function EmptyFriendsState({ onSearchFocus }: EmptyFriendsStateProps) {
    return (
        <div className="bg-surface-container rounded-2xl px-6 py-10 text-center">
            <div className="w-16 h-16 rounded-full bg-primary-container flex items-center justify-center mx-auto mb-4">
                <Icon name="group_add" size="2xl" className="text-primary" />
            </div>
            <h3 className="font-headline font-bold text-lg text-on-surface mb-2">
                Найди первого напарника!
            </h3>
            <p className="text-sm text-on-surface-variant mb-5 max-w-xs mx-auto">
                Тренируйтесь вместе и соревнуйтесь в рейтинге друзей
            </p>
            {onSearchFocus && (
                <button
                    onClick={onSearchFocus}
                    className="inline-flex items-center gap-2 px-5 py-3 rounded-full bg-primary text-on-primary font-semibold text-sm shadow-[0_4px_0_var(--color-primary-dim)] active:shadow-none active:translate-y-1 tonal-transition"
                >
                    <Icon name="search" size="sm" />
                    Найти друзей
                </button>
            )}
        </div>
    );
}
