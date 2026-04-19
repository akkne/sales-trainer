import type { NotificationTypeKey } from "@/lib/hooks/useNotifications";

export interface NotificationVisualMeta {
    iconName: string;
    iconColorClassName: string;
}

const DEFAULT_VISUAL_META: NotificationVisualMeta = {
    iconName: "notifications",
    iconColorClassName: "text-on-surface-variant",
};

const VISUAL_META_BY_TYPE: Record<NotificationTypeKey, NotificationVisualMeta> = {
    FriendRequestReceived: {
        iconName: "person_add",
        iconColorClassName: "text-primary",
    },
    FriendRequestAccepted: {
        iconName: "group",
        iconColorClassName: "text-primary",
    },
    ChatMessageReceived: {
        iconName: "chat_bubble",
        iconColorClassName: "text-primary",
    },
    AchievementUnlocked: {
        iconName: "emoji_events",
        iconColorClassName: "text-amber-500",
    },
    StreakMilestone: {
        iconName: "local_fire_department",
        iconColorClassName: "text-error-container",
    },
};

export function getNotificationVisualMeta(notificationType: string): NotificationVisualMeta {
    return VISUAL_META_BY_TYPE[notificationType as NotificationTypeKey] ?? DEFAULT_VISUAL_META;
}

export function formatRelativeTimestamp(isoTimestamp: string, nowDate: Date = new Date()): string {
    const createdAtDate = new Date(isoTimestamp);
    const differenceMilliseconds = nowDate.getTime() - createdAtDate.getTime();
    const differenceMinutes = Math.floor(differenceMilliseconds / 60000);

    if (differenceMinutes < 1) return "только что";
    if (differenceMinutes < 60) return `${differenceMinutes} мин назад`;

    const differenceHours = Math.floor(differenceMinutes / 60);
    if (differenceHours < 24) return `${differenceHours} ч назад`;

    const differenceDays = Math.floor(differenceHours / 24);
    if (differenceDays < 7) return `${differenceDays} д назад`;

    return createdAtDate.toLocaleDateString("ru-RU", { day: "numeric", month: "short" });
}
