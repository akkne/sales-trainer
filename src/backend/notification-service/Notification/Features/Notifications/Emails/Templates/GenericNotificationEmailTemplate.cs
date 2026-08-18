using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Emails.Templates;

/// <summary>
/// Fallback template used when no type-specific template is registered for a notification. It
/// renders the notification's own title and body, so a newly added email-enabled type still
/// produces a sensible message before a bespoke template exists. Not keyed into the type map —
/// the renderer holds it explicitly as the default.
/// </summary>
public sealed class GenericNotificationEmailTemplate : NotificationEmailTemplate
{
    /// <summary>
    /// <c>default</c> is 0, which is not a defined <see cref="NotificationType"/> member, so this
    /// template can never collide with a real type in the renderer's index.
    /// </summary>
    public override NotificationType NotificationType => default;

    protected override string? ActionLabel => "Open Sellevate";

    protected override string BuildSubject(NotificationEmailContext context) =>
        string.IsNullOrWhiteSpace(context.Title) ? "You have a new notification" : context.Title;

    protected override string BuildHeadline(NotificationEmailContext context) =>
        string.IsNullOrWhiteSpace(context.Title) ? "You have a new notification" : context.Title;

    protected override string BuildContentHtml(NotificationEmailContext context) =>
        GreetingHtml(context) + Paragraph(NotificationEmailLayout.Encode(context.Body));

    protected override string BuildTextBody(NotificationEmailContext context) =>
        GreetingText(context) + "\n\n" + context.Body;
}
