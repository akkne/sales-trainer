namespace SalesTrainer.Api.Features.Notifications.Models;

public sealed record NotificationDto(
    Guid Id,
    string NotificationType,
    string Title,
    string Body,
    string? ActionUrl,
    string? RelatedEntityId,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt);
