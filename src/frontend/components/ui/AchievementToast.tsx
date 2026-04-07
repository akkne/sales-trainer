"use client";

import { useEffect, useState } from "react";
import { Icon } from "@/components/ui/Icon";

export interface AchievementToastData {
    key: string;
    iconEmoji: string;
    title: string;
    description: string;
}

interface AchievementToastProps {
    achievement: AchievementToastData;
    onDismiss: () => void;
}

export function AchievementToast({ achievement, onDismiss }: AchievementToastProps) {
    const [visible, setVisible] = useState(false);

    useEffect(() => {
        // Trigger slide-in after mount
        const showTimer = requestAnimationFrame(() => setVisible(true));

        // Auto-dismiss after 4s
        const dismissTimer = setTimeout(() => {
            setVisible(false);
            // Wait for slide-out animation before calling onDismiss
            setTimeout(onDismiss, 350);
        }, 4000);

        return () => {
            cancelAnimationFrame(showTimer);
            clearTimeout(dismissTimer);
        };
    }, [onDismiss]);

    return (
        <div
            onClick={() => {
                setVisible(false);
                setTimeout(onDismiss, 350);
            }}
            className={`flex items-center gap-4 bg-surface-container-lowest border-2 border-primary rounded-2xl px-4 py-3 shadow-lg cursor-pointer select-none
                transition-all duration-300 ease-out
                ${visible ? "opacity-100 translate-y-0" : "opacity-0 -translate-y-4"}`}
            role="alert"
            aria-live="polite"
        >
            <div className="w-12 h-12 rounded-full bg-primary-container flex items-center justify-center shrink-0">
                <span className="text-2xl">{achievement.iconEmoji}</span>
            </div>
            <div className="flex-1 min-w-0">
                <div className="flex items-center gap-1 text-xs font-bold uppercase tracking-wider text-primary mb-0.5">
                    <Icon name="emoji_events" size="sm" />
                    Достижение разблокировано!
                </div>
                <p className="font-bold text-on-surface text-sm leading-tight truncate">
                    {achievement.title}
                </p>
                <p className="text-xs text-on-surface-variant truncate">{achievement.description}</p>
            </div>
        </div>
    );
}

interface AchievementToastQueueProps {
    queue: AchievementToastData[];
    onDismiss: (key: string) => void;
}

/**
 * Renders only the first toast in the queue.
 * When it dismisses, the parent pops it → next one appears.
 */
export function AchievementToastQueue({ queue, onDismiss }: AchievementToastQueueProps) {
    if (queue.length === 0) return null;
    const current = queue[0];

    return (
        <div className="fixed top-4 left-0 right-0 z-50 flex justify-center px-4 pointer-events-none">
            <div className="pointer-events-auto w-full max-w-sm">
                <AchievementToast
                    key={current.key}
                    achievement={current}
                    onDismiss={() => onDismiss(current.key)}
                />
            </div>
        </div>
    );
}
