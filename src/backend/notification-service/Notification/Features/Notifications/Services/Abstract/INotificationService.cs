using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Services.Abstract;

public interface INotificationService
{
    Task CreateAsync(
        CreateNotificationRequest request,
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
}
