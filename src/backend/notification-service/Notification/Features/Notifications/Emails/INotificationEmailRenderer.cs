namespace Sellevate.Notification.Features.Notifications.Emails;

/// <summary>
/// Selects the right <see cref="INotificationEmailTemplate"/> for a notification and renders it,
/// after resolving the notification's relative action path to an absolute frontend URL.
/// </summary>
public interface INotificationEmailRenderer
{
    EmailContent Render(NotificationEmailContext context);
}
