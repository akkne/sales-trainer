"use client";

import { useEffect, useRef, useState } from "react";
import { Icon } from "@/components/ui/Icon";
import { useUnreadNotificationCount } from "@/lib/hooks/useNotifications";
import { NotificationPanel } from "./NotificationPanel";

export function NotificationBell() {
    const [isPanelOpen, setIsPanelOpen] = useState(false);
    const containerReference = useRef<HTMLDivElement>(null);
    const { data: unreadCountData } = useUnreadNotificationCount();
    const unreadCount = unreadCountData?.count ?? 0;

    useEffect(() => {
        if (!isPanelOpen) return;

        function handleClickOutside(mouseEvent: MouseEvent) {
            if (!containerReference.current) return;
            if (containerReference.current.contains(mouseEvent.target as Node)) return;
            setIsPanelOpen(false);
        }

        document.addEventListener("mousedown", handleClickOutside);
        return () => document.removeEventListener("mousedown", handleClickOutside);
    }, [isPanelOpen]);

    return (
        <div ref={containerReference} className="relative">
            <button
                type="button"
                onClick={() => setIsPanelOpen((previouslyOpen) => !previouslyOpen)}
                className="relative p-2 rounded-full hover:bg-surface-container tonal-transition"
                aria-label="Уведомления"
                aria-expanded={isPanelOpen}
            >
                <Icon name="bell" size="md" className="text-on-surface-variant" />
                {unreadCount > 0 && (
                    <span
                        aria-label={`${unreadCount} непрочитанных`}
                        className="absolute -top-0.5 -right-0.5 min-w-4 h-4 flex items-center justify-center rounded-full bg-error text-on-error text-[10px] font-bold px-1"
                    >
                        {unreadCount > 9 ? "9+" : unreadCount}
                    </span>
                )}
            </button>

            <NotificationPanel
                isOpen={isPanelOpen}
                onRequestClose={() => setIsPanelOpen(false)}
            />
        </div>
    );
}
