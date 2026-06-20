namespace Sellevate.Notification.Features.Notifications.Models;

public sealed record CreateNotificationRequest(
    Guid RecipientUserId,
    NotificationType NotificationType,
    string Title,
    string Body,
    string? ActionUrl,
    string? RelatedEntityId);
