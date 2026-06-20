namespace Sellevate.Notification.Common.Constants;

public static class RedisKeys
{
    public static string Inbox(Guid recipientUserId) => $"notifications:inbox:{recipientUserId:N}";

    public static string UnreadCount(Guid recipientUserId) => $"notifications:unread:{recipientUserId:N}";
}
