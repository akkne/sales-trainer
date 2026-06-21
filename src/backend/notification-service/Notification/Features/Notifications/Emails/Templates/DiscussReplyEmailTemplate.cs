using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Emails.Templates;

/// <summary>Email for a new reply to a discussion thread the recipient authored.</summary>
public sealed class DiscussReplyEmailTemplate : NotificationEmailTemplate
{
    public override NotificationType NotificationType => NotificationType.DiscussReplyReceived;

    protected override string? ActionLabel => "View discussion";

    protected override string BuildSubject(NotificationEmailContext context) =>
        "New reply to your discussion";

    protected override string BuildHeadline(NotificationEmailContext context) =>
        "You have a new reply";

    protected override string BuildContentHtml(NotificationEmailContext context) =>
        GreetingHtml(context)
        + Paragraph(NotificationEmailLayout.Encode(context.Body))
        + Paragraph("Jump back into the discussion to keep the conversation going.");

    protected override string BuildTextBody(NotificationEmailContext context) =>
        GreetingText(context)
        + "\n\n" + context.Body
        + "\n\nJump back into the discussion to keep the conversation going.";
}
