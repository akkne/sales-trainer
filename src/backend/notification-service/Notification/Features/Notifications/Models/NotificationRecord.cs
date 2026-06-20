namespace Sellevate.Notification.Features.Notifications.Models;

public sealed record NotificationRecord
{
    public required Guid Id { get; init; }
    public required Guid RecipientUserId { get; init; }
    public required NotificationType NotificationType { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public string? ActionUrl { get; init; }
    public string? RelatedEntityId { get; init; }
    public required bool IsRead { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? ReadAt { get; init; }
}
