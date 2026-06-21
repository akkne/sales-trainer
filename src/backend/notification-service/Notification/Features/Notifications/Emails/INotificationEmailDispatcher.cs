using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Emails;

/// <summary>
/// Resolves a recipient's email address and delivers a rendered notification email. Failures are
/// handled internally (logged, never thrown) so that an email problem never breaks in-app
/// notification storage or triggers a Kafka redelivery loop.
/// </summary>
public interface INotificationEmailDispatcher
{
    Task DispatchAsync(
        Guid recipientUserId,
        NotificationType notificationType,
        string title,
        string body,
        string? actionUrl,
        CancellationToken cancellationToken = default);
}
