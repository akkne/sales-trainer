using SalesTrainer.Api.Features.Notifications.Models;

namespace SalesTrainer.Api.Features.Notifications.Services.Abstract;

public interface INotificationService
{
    Task CreateAsync(
        Guid recipientUserId,
        NotificationType notificationType,
        string title,
        string body,
        string? actionUrl = null,
        string? relatedEntityId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationDto>> GetRecentAsync(
        Guid recipientUserId,
        int limit,
        bool includeRead,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(
        Guid recipientUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    Task<int> DeleteReadNotificationsOlderThanAsync(
        DateTime thresholdUtc,
        CancellationToken cancellationToken = default);
}
