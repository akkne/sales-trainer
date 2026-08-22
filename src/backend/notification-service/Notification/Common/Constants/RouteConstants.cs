namespace Sellevate.Notification.Common.Constants;

public static class RouteConstants
{
    public const string NotificationsBase = "notifications";
    public const string UnreadCount = "unread-count";
    public const string MarkSingleAsRead = "{notificationId:guid}/read";
    public const string MarkAllAsRead = "read-all";

    /// <summary>Q-4. The caller's own notification switches; never another person's.</summary>
    public const string Preferences = "notifications/preferences";
}
