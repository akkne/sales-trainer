using System.Text;
using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Emails;

/// <summary>
/// Base class for notification email templates. It owns the rendering algorithm — assemble the
/// HTML via <see cref="NotificationEmailLayout"/>, build the plain-text fallback, attach the CTA —
/// and exposes focused extension points (<see cref="BuildSubject"/>, <see cref="BuildHeadline"/>,
/// <see cref="BuildContentHtml"/>, <see cref="BuildTextBody"/>, <see cref="ActionLabel"/>) for each
/// concrete type to fill in. This keeps every template small and consistent (template-method pattern).
/// </summary>
public abstract class NotificationEmailTemplate : INotificationEmailTemplate
{
    public abstract NotificationType NotificationType { get; }

    /// <summary>Label for the call-to-action button; null hides the button.</summary>
    protected abstract string? ActionLabel { get; }

    protected abstract string BuildSubject(NotificationEmailContext context);

    protected abstract string BuildHeadline(NotificationEmailContext context);

    /// <summary>Inner HTML for the card. Implementations MUST encode untrusted text via
    /// <see cref="NotificationEmailLayout.Encode"/>.</summary>
    protected abstract string BuildContentHtml(NotificationEmailContext context);

    /// <summary>Plain-text body (no markup) used as the email's text fallback.</summary>
    protected abstract string BuildTextBody(NotificationEmailContext context);

    public EmailContent Render(NotificationEmailContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var actionLabel = ActionLabel;
        var html = NotificationEmailLayout.Build(
            BuildHeadline(context),
            BuildContentHtml(context),
            context.ActionUrl,
            actionLabel);

        var text = new StringBuilder(BuildTextBody(context));
        if (!string.IsNullOrWhiteSpace(context.ActionUrl) && !string.IsNullOrWhiteSpace(actionLabel))
        {
            text.Append("\n\n").Append(actionLabel).Append(": ").Append(context.ActionUrl);
        }

        return new EmailContent(BuildSubject(context), html, text.ToString());
    }

    /// <summary>Greeting line shared by templates, e.g. "Hi Alex,". Falls back gracefully when the
    /// recipient name is unknown.</summary>
    protected static string GreetingHtml(NotificationEmailContext context) =>
        string.IsNullOrWhiteSpace(context.RecipientName)
            ? "<p style=\"margin:0 0 16px;\">Hi there,</p>"
            : $"<p style=\"margin:0 0 16px;\">Hi {NotificationEmailLayout.Encode(context.RecipientName)},</p>";

    protected static string GreetingText(NotificationEmailContext context) =>
        string.IsNullOrWhiteSpace(context.RecipientName) ? "Hi there," : $"Hi {context.RecipientName},";

    /// <summary>Wraps a body string in an HTML paragraph with the standard spacing.</summary>
    protected static string Paragraph(string encodedHtml) =>
        $"<p style=\"margin:0 0 16px;\">{encodedHtml}</p>";
}
