namespace Sellevate.Notification.Features.Notifications.Emails.Delayed;

/// <summary>
/// A chat message awaiting its "still unread?" check. Scheduled when a message is received and
/// emailed only if, once <see cref="DueAt"/> arrives, no read receipt for the conversation has
/// caught up to <see cref="MessageSentAt"/>.
/// </summary>
public sealed record PendingChatEmail(
    Guid RecipientUserId,
    string Body,
    string? ActionUrl,
    Guid? ConversationId,
    DateTime MessageSentAt,
    DateTime DueAt);
