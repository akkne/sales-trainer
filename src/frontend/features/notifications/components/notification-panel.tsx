"use client";

import { useRouter } from "next/navigation";
import {
    useMarkAllNotificationsAsRead,
    useMarkNotificationAsRead,
    useNotifications,
    type NotificationData,
} from "@/features/notifications/hooks/use-notifications";
import { NotificationCard } from "./notification-card";

interface NotificationPanelProps {
    isOpen: boolean;
    onRequestClose: () => void;
}

export function NotificationPanel({ isOpen, onRequestClose }: NotificationPanelProps) {
    const router = useRouter();
    const { data: notifications, isLoading, isError } = useNotifications(isOpen);
    const markAsReadMutation = useMarkNotificationAsRead();
    const markAllAsReadMutation = useMarkAllNotificationsAsRead();

    if (!isOpen) return null;

    const hasUnread = (notifications ?? []).some((notification) => !notification.isRead);

    function handleActivateNotification(notification: NotificationData) {
        if (!notification.isRead) {
            markAsReadMutation.mutate(notification.id);
        }
        onRequestClose();
        if (notification.actionUrl) {
            router.push(notification.actionUrl);
        }
    }

    function handleMarkAllAsRead() {
        markAllAsReadMutation.mutate();
    }

    return (
        <>
            <div
                aria-hidden
                className="fixed inset-0 z-40 md:hidden bg-black/30"
                onClick={onRequestClose}
            />
            <div
                role="dialog"
                aria-label="Notifications"
                className="fixed top-14 right-0 left-0 md:absolute md:top-full md:right-0 md:left-auto md:mt-2 md:w-96 z-50 bg-surface border border-line rounded-[var(--r-md)] md:rounded-[var(--r-lg)] shadow-lg overflow-hidden max-h-[80vh] flex flex-col"
            >
                <header className="flex items-center justify-between px-4 py-3 border-b border-line bg-bg-2">
                    <h2 className="text-sm font-semibold text-ink">Notifications</h2>
                    <button
                        type="button"
                        onClick={handleMarkAllAsRead}
                        disabled={!hasUnread || markAllAsReadMutation.isPending}
                        className="text-xs font-medium text-indigo disabled:text-ink-3 disabled:cursor-not-allowed hover:underline"
                    >
                        Mark all read
                    </button>
                </header>

                <div className="flex-1 overflow-y-auto" aria-live="polite" aria-atomic="false">
                    {isLoading && (
                        <p className="px-4 py-8 text-center text-sm text-ink-3">
                            Loading...
                        </p>
                    )}
                    {isError && (
                        <p className="px-4 py-8 text-center text-sm text-bad">
                            Couldn't load notifications
                        </p>
                    )}
                    {!isLoading && !isError && (notifications?.length ?? 0) === 0 && (
                        <p className="px-4 py-8 text-center text-sm text-ink-3">
                            No notifications yet
                        </p>
                    )}
                    {notifications?.map((notification) => (
                        <NotificationCard
                            key={notification.id}
                            notification={notification}
                            onActivate={handleActivateNotification}
                        />
                    ))}
                </div>
            </div>
        </>
    );
}
