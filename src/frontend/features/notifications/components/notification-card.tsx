"use client";

import { Icon } from "@/shared/components/icon";
import type { NotificationData } from "@/features/notifications/hooks/use-notifications";
import { formatRelativeTimestamp, getNotificationVisualMeta } from "./notificationMeta";

interface NotificationCardProps {
    notification: NotificationData;
    onActivate: (notification: NotificationData) => void;
}

export function NotificationCard({ notification, onActivate }: NotificationCardProps) {
    const visualMeta = getNotificationVisualMeta(notification.notificationType);
    const isUnread = !notification.isRead;

    return (
        <button
            type="button"
            onClick={() => onActivate(notification)}
            className={`w-full text-left px-4 py-3 flex items-start gap-3 transition-colors ${
                isUnread
                    ? "bg-indigo-soft/40 hover:bg-indigo-soft/60"
                    : "bg-surface hover:bg-bg-2"
            } border-b border-line`}
        >
            <span
                className={`flex-shrink-0 w-9 h-9 rounded-full bg-bg-2 flex items-center justify-center ${visualMeta.iconColorClassName}`}
            >
                <Icon name={visualMeta.iconName} size="md" />
            </span>
            <span className="flex-1 min-w-0">
                <span className="flex items-center justify-between gap-2">
                    <span className={`text-sm ${isUnread ? "font-semibold text-ink" : "text-ink-3"}`}>
                        {notification.title}
                    </span>
                    <span className="text-[11px] text-ink-3 whitespace-nowrap">
                        {formatRelativeTimestamp(notification.createdAt)}
                    </span>
                </span>
                <span className="block text-xs text-ink-3 mt-0.5 line-clamp-2">
                    {notification.body}
                </span>
            </span>
            {isUnread && (
                <span
                    aria-hidden
                    className="flex-shrink-0 mt-1.5 w-2 h-2 rounded-full bg-ink"
                />
            )}
        </button>
    );
}
