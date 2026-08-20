import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { clientLogger } from "@/shared/utils/client-logger";
import { toast } from "@/features/notifications/store/toast-store";

export type NotificationTypeKey =
    | "FriendRequestReceived"
    | "FriendRequestAccepted"
    | "ChatMessageReceived";

/**
 * Notification types the product no longer has. The notification service can still hold older
 * ones, and a service that was never told about the removal could still emit them — an
 * "achievement unlocked" toast in a product with no achievements is pure confusion, so they are
 * dropped on arrival instead of rendered with a fallback icon.
 */
const RETIRED_NOTIFICATION_TYPES = ["AchievementUnlocked", "StreakMilestone"];

function withoutRetiredTypes(notifications: NotificationData[]): NotificationData[] {
    return notifications.filter(
        (notification) => !RETIRED_NOTIFICATION_TYPES.includes(notification.notificationType));
}

export interface NotificationData {
    id: string;
    notificationType: NotificationTypeKey;
    title: string;
    body: string;
    actionUrl: string | null;
    relatedEntityId: string | null;
    isRead: boolean;
    createdAt: string;
    readAt: string | null;
}

export interface UnreadNotificationCountData {
    count: number;
}

const NOTIFICATIONS_LIST_QUERY_KEY = ["notifications"] as const;
const NOTIFICATIONS_UNREAD_COUNT_QUERY_KEY = ["notificationsUnreadCount"] as const;
const UNREAD_COUNT_POLLING_INTERVAL_MILLISECONDS = 20000;
const LIST_POLLING_INTERVAL_MILLISECONDS = 30000;

export function useNotifications(enabled = true) {
    return useQuery({
        queryKey: NOTIFICATIONS_LIST_QUERY_KEY,
        queryFn: async () =>
            withoutRetiredTypes(
                await apiClient.get<NotificationData[]>("/notifications?limit=20&includeRead=true")),
        enabled,
        refetchInterval: LIST_POLLING_INTERVAL_MILLISECONDS,
    });
}

export function useUnreadNotificationCount() {
    return useQuery({
        queryKey: NOTIFICATIONS_UNREAD_COUNT_QUERY_KEY,
        queryFn: () => apiClient.get<UnreadNotificationCountData>("/notifications/unread-count"),
        refetchInterval: UNREAD_COUNT_POLLING_INTERVAL_MILLISECONDS,
    });
}

export function useMarkNotificationAsRead() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (notificationId: string) =>
            apiClient.put<void>(`/notifications/${notificationId}/read`, {}),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: NOTIFICATIONS_LIST_QUERY_KEY });
            queryClient.invalidateQueries({ queryKey: NOTIFICATIONS_UNREAD_COUNT_QUERY_KEY });
        },
        onError: (error, notificationId) => {
            clientLogger.warn("Failed to mark notification as read", {
                notificationId,
                error: (error as Error).message,
            });
            toast.error(`Не удалось отметить уведомление прочитанным: ${(error as Error).message}`);
        },
    });
}

export function useMarkAllNotificationsAsRead() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: () => apiClient.put<void>("/notifications/read-all", {}),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: NOTIFICATIONS_LIST_QUERY_KEY });
            queryClient.invalidateQueries({ queryKey: NOTIFICATIONS_UNREAD_COUNT_QUERY_KEY });
        },
        onError: (error) => {
            clientLogger.warn("Failed to mark all notifications as read", {
                error: (error as Error).message,
            });
            toast.error(`Не удалось отметить все уведомления прочитанными: ${(error as Error).message}`);
        },
    });
}
