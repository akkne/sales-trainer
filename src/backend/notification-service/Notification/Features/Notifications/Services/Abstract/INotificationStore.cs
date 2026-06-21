using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Services.Abstract;

public interface INotificationStore
{
    Task PrependAsync(
        Guid recipientUserId,
        NotificationRecord notification,
        int inboxCapacity,
        TimeSpan retention,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationRecord>> GetAllAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the recipient's inbox already contains a notification with the
    /// given type and relatedEntityId — used to prevent duplicate notifications when
    /// the same domain event is replayed.
    /// </summary>
    Task<bool> ExistsAsync(
        Guid recipientUserId,
        NotificationType notificationType,
        string? relatedEntityId,
        CancellationToken cancellationToken = default);

    Task<bool> ReplaceAsync(
        Guid recipientUserId,
        NotificationRecord updatedNotification,
        TimeSpan retention,
        CancellationToken cancellationToken = default);

    Task ReplaceAllAsync(
        Guid recipientUserId,
        IReadOnlyList<NotificationRecord> notifications,
        TimeSpan retention,
        CancellationToken cancellationToken = default);
}
