namespace Sellevate.Notification.Features.Notifications.Emails.Delayed;

/// <summary>
/// A chat message awaiting its "still unread?" check. Scheduled when a message is received and
/// emailed only if, once <see cref="DueAt"/> arrives, no read receipt for the conversation has
/// caught up to <see cref="MessageSentAt"/>.
///
/// <para>
/// Phase 40.13 added <see cref="OrganizationId"/>. The pending queue is a single cross-organization
/// work list — see <c>RedisDelayedChatEmailScheduler</c> for why it stays one key — so the tenant
/// has to travel with the item, exactly as it travels in a Kafka envelope. Without it the
/// dispatcher would have no organization when it goes looking for the read watermark, which is
/// per-organization state.
/// </para>
/// </summary>
public sealed record PendingChatEmail(
    Guid OrganizationId,
    Guid RecipientUserId,
    string Body,
    string? ActionUrl,
    Guid? ConversationId,
    DateTime MessageSentAt,
    DateTime DueAt);
