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
    /// <summary>
    /// The one deliberately cross-organization key: a single due-time-ordered work list for the
    /// whole service, the same shape as an outbox. Splitting it per organization would force the
    /// dispatcher to poll one sorted set per customer to find out what is due — cost growing with
    /// the customer list to protect data that is not in the key. The organization travels inside
    /// each queued item instead, exactly as it travels in a Kafka envelope. Unchanged since before
    /// Phase 40.13, so nothing has to be migrated; only the member payload gained a field.
    /// </summary>
    public const string ChatEmailPendingQueue = "notifications:chat-email:pending";

    /// <summary>
    /// Also deliberately un-prefixed, and for the opposite reason to
    /// <see cref="ChatEmailPendingQueue"/>: an identity in this product is cross-organization
    /// (docs/TENANCY/TENANCY.md §4.2), so an organization is not a property of the row, and putting
    /// one in the key would mean either duplicating the projection per organization or picking one
    /// arbitrarily. Holds only what identity-service broadcasts platform-wide — an email address and
    /// a display name — read to address an email that some other, org-scoped decision already made.
    /// </summary>
    public static string UserProfile(Guid userId) => $"notifications:user:{userId:N}";

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
            throw new InvalidOperationException(ErrorMessages.OrganizationContextNotSet);
        }

        return $"org:{organizationId:N}:";
    }
}
