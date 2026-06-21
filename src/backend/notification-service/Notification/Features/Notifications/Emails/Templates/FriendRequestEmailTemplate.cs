using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Emails.Templates;

/// <summary>Email for a new friend request awaiting the recipient's response.</summary>
public sealed class FriendRequestEmailTemplate : NotificationEmailTemplate
{
    public override NotificationType NotificationType => NotificationType.FriendRequestReceived;

    protected override string? ActionLabel => "View request";

    protected override string BuildSubject(NotificationEmailContext context) =>
        "You have a new friend request";

    protected override string BuildHeadline(NotificationEmailContext context) =>
        "New friend request";

    protected override string BuildContentHtml(NotificationEmailContext context) =>
        GreetingHtml(context)
        + Paragraph(NotificationEmailLayout.Encode(context.Body))
        + Paragraph("Accept or decline the request to manage your network.");

    protected override string BuildTextBody(NotificationEmailContext context) =>
        GreetingText(context)
        + "\n\n" + context.Body
        + "\n\nAccept or decline the request to manage your network.";
}
