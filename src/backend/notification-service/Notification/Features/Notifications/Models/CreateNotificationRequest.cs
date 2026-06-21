namespace Sellevate.Notification.Features.Notifications.Models;

public sealed record CreateNotificationRequest(
    Guid RecipientUserId,
    NotificationType NotificationType,
    string Title,
    string Body,
    string? ActionUrl,
    string? RelatedEntityId,
    // When true, the notification is also delivered to the recipient by email (in addition to
    // being stored in the in-app inbox). Chat messages keep this false because their email is
    // dispatched on a delayed "still unread after N minutes" path, not at creation time.
    bool SendEmail = false);
