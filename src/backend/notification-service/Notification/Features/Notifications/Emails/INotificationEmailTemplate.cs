using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Emails;

/// <summary>
/// Renders the email for one <see cref="NotificationType"/>. Each concrete type owns its own
/// subject, headline, copy and call-to-action; the shared chrome lives in
/// <see cref="NotificationEmailLayout"/> and the boilerplate in <see cref="NotificationEmailTemplate"/>.
/// Templates are discovered by DI and dispatched by <see cref="INotificationEmailRenderer"/>.
/// </summary>
public interface INotificationEmailTemplate
{
    /// <summary>The notification type this template renders.</summary>
    NotificationType NotificationType { get; }

    EmailContent Render(NotificationEmailContext context);
}
