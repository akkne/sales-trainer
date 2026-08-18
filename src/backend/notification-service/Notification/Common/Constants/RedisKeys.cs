namespace Sellevate.Notification.Common.Constants;

/// <summary>
/// Phase 40.13. Every key that holds one organization's data carries the <c>org:{orgId}:</c>
/// prefix 40.11 established for ai-service. A user id is globally unique, so the inbox could not
/// have leaked by collision — but the prefix is what makes "no notification key is shared across
/// organizations" checkable by reading key names instead of reasoning about each one, and it is
/// what makes a per-organization purge (a customer leaving) a single <c>SCAN org:{orgId}:*</c>.
/// </summary>
public static class RedisKeys
{
    public static string Inbox(Guid organizationId, Guid recipientUserId) =>
        $"{OrganizationPrefix(organizationId)}notifications:inbox:{recipientUserId:N}";

    public static string UnreadCount(Guid organizationId, Guid recipientUserId) =>
        $"{OrganizationPrefix(organizationId)}notifications:unread:{recipientUserId:N}";

    /// <summary>
    /// The per-(recipient, conversation) read watermark that suppresses a pending unread-chat
    /// email. Scoped like the inbox: a conversation belongs to one organization, because 40.13 also
    /// stopped chat from crossing the boundary in social-service.
    /// </summary>
    public static string ChatEmailReadWatermark(Guid organizationId, Guid recipientUserId, Guid? conversationId) =>
        $"{OrganizationPrefix(organizationId)}notifications:chat-email:read:" +
        $"{recipientUserId:N}:{conversationId?.ToString("N") ?? "none"}";

    /// <summary>
    /// Fails closed and loudly, with the same wording as <c>TenantSaveChangesInterceptor</c> so
    /// operators grep once. Without this an unset tenant would silently build
    /// <c>org:00000000-...:notifications:inbox:{user}</c> — one shared bucket collecting every
    /// caller whose context was missing, which is worse than the un-prefixed key it replaced.
    /// </summary>
    private static string OrganizationPrefix(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new InvalidOperationException("Organization context is not set.");
        }

        return $"org:{organizationId:N}:";
    }
}
