using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Services.Abstract;

/// <summary>
/// Phase 40.13. Every member takes the organization explicitly rather than reading an ambient
/// context: the store is a singleton, so it has no current organization, and the compiler refusing
/// to build a call without one is a stronger guarantee than a convention. The tenant is resolved
/// once, in <c>NotificationService</c>, which is scoped and therefore does have a context.
/// </summary>
public interface INotificationStore
{
    Task PrependAsync(
        Guid organizationId,
        Guid recipientUserId,
        NotificationRecord notification,
        int inboxCapacity,
        TimeSpan retention,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationRecord>> GetAllAsync(
        Guid organizationId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the recipient's inbox already contains a notification with the
    /// given type and relatedEntityId — used to prevent duplicate notifications when
    /// the same domain event is replayed.
    /// </summary>
    Task<bool> ExistsAsync(
        Guid organizationId,
        Guid recipientUserId,
        NotificationType notificationType,
        string? relatedEntityId,
        CancellationToken cancellationToken = default);

    Task<bool> ReplaceAsync(
        Guid organizationId,
        Guid recipientUserId,
        NotificationRecord updatedNotification,
        TimeSpan retention,
        CancellationToken cancellationToken = default);

    Task ReplaceAllAsync(
        Guid organizationId,
        Guid recipientUserId,
        IReadOnlyList<NotificationRecord> notifications,
        TimeSpan retention,
        CancellationToken cancellationToken = default);
}
