using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Services.Abstract;

/// <summary>
/// The inbox as the rest of the service sees it. Every member is implicitly scoped to the ambient
/// organization — no member takes one, and none has a platform-global reading — so a caller with no
/// tenant context gets an exception rather than another organization's notifications.
/// </summary>
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
