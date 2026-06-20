namespace Sellevate.Notification.Common.Constants;

public static class RouteConstants
{
    public const string NotificationsBase = "notifications";
    public const string UnreadCount = "unread-count";
    public const string MarkSingleAsRead = "{notificationId:guid}/read";
    public const string MarkAllAsRead = "read-all";
    public const string HealthCheck = "/healthz";
}
