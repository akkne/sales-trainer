using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Emails;

/// <summary>
/// Everything a template needs to render a notification email. <see cref="ActionUrl"/> starts as
/// the notification's relative app path and is rewritten to an absolute URL by the renderer
/// before a template sees it, so templates never concern themselves with the frontend origin.
/// </summary>
public sealed record NotificationEmailContext(
    string RecipientName,
    NotificationType NotificationType,
    string Title,
    string Body,
    string? ActionUrl);
