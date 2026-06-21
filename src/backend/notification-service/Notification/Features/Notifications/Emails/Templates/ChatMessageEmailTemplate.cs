using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Emails.Templates;

/// <summary>Email for an unread direct message (dispatched after the unread grace period).</summary>
public sealed class ChatMessageEmailTemplate : NotificationEmailTemplate
{
    public override NotificationType NotificationType => NotificationType.ChatMessageReceived;

    protected override string? ActionLabel => "View message";

    protected override string BuildSubject(NotificationEmailContext context) =>
        "You have a new message on Sellevate";

    protected override string BuildHeadline(NotificationEmailContext context) =>
        "You have a new message";

    protected override string BuildContentHtml(NotificationEmailContext context) =>
        GreetingHtml(context)
        + Paragraph(NotificationEmailLayout.Encode(context.Body))
        + Paragraph("Open the conversation to read it and reply.");

    protected override string BuildTextBody(NotificationEmailContext context) =>
        GreetingText(context)
        + "\n\n" + context.Body
        + "\n\nOpen the conversation to read it and reply.";
}
