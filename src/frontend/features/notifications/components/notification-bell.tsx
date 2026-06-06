"use client";

import { useEffect, useRef, useState } from "react";
import { Icon } from "@/shared/components/icon";
import { useUnreadNotificationCount } from "@/features/notifications/hooks/use-notifications";
import { NotificationPanel } from "./notification-panel";

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
                className="relative p-2 rounded-full hover:bg-bg-2 transition-colors"
                aria-label="Уведомления"
                aria-expanded={isPanelOpen}
            >
                <Icon name="bell" size="md" className="text-ink-3" />
                {unreadCount > 0 && (
                    <span
                        aria-label={`${unreadCount} непрочитанных`}
                        className="absolute -top-0.5 -right-0.5 min-w-4 h-4 flex items-center justify-center rounded-full bg-bad text-white text-[10px] font-bold px-1"
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
