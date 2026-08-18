namespace Sellevate.Notification.Features.Notifications.Models;

/// <summary>
/// A notification to store. <c>RelatedEntityId</c> is the deduplication key — see
/// <c>NotificationEventMapper</c> for how coarse each type's key deliberately is — and
/// <c>SendEmail</c> opts the notification into the email side channel at creation time. Chat
/// messages keep <c>SendEmail</c> false even though they are emailed, because their email is
/// dispatched on the delayed "still unread after N minutes" path instead.
/// </summary>
public sealed record CreateNotificationRequest(
    Guid RecipientUserId,
    NotificationType NotificationType,
    string Title,
    string Body,
    string? ActionUrl,
    string? RelatedEntityId,
    bool SendEmail = false);
