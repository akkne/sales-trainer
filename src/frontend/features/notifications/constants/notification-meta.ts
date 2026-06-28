import type { NotificationTypeKey } from "@/features/notifications/hooks/use-notifications";
import type { IconName } from "@/shared/components/icon";

export interface NotificationVisualMeta {
    iconName: IconName;
    iconColorClassName: string;
}

const DEFAULT_VISUAL_META: NotificationVisualMeta = {
    iconName: "bell",
    iconColorClassName: "text-ink-3",
};

const VISUAL_META_BY_TYPE: Record<NotificationTypeKey, NotificationVisualMeta> = {
    FriendRequestReceived: {
        iconName: "user",
        iconColorClassName: "text-indigo",
    },
    FriendRequestAccepted: {
        iconName: "users",
        iconColorClassName: "text-indigo",
    },
    ChatMessageReceived: {
        iconName: "message",
        iconColorClassName: "text-indigo",
    },
    AchievementUnlocked: {
        iconName: "trophy",
        iconColorClassName: "text-amber",
    },
    StreakMilestone: {
        iconName: "flame",
        iconColorClassName: "text-flame",
    },
};

export function getNotificationVisualMeta(notificationType: string): NotificationVisualMeta {
    return VISUAL_META_BY_TYPE[notificationType as NotificationTypeKey] ?? DEFAULT_VISUAL_META;
}

export function formatRelativeTimestamp(isoTimestamp: string, nowDate: Date = new Date()): string {
    const createdAtDate = new Date(isoTimestamp);
    const differenceMilliseconds = nowDate.getTime() - createdAtDate.getTime();
    const differenceMinutes = Math.floor(differenceMilliseconds / 60000);

    if (differenceMinutes < 1) return "just now";
    if (differenceMinutes < 60) return `${differenceMinutes} min ago`;

    const differenceHours = Math.floor(differenceMinutes / 60);
    if (differenceHours < 24) return `${differenceHours} h ago`;

    const differenceDays = Math.floor(differenceHours / 24);
    if (differenceDays < 7) return `${differenceDays} d ago`;

    return createdAtDate.toLocaleDateString("en-US", { day: "numeric", month: "short" });
}
