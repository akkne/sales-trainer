using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Emails.Templates;

/// <summary>Email telling the requester that their friend request was accepted.</summary>
public sealed class FriendRequestAcceptedEmailTemplate : NotificationEmailTemplate
{
    public override NotificationType NotificationType => NotificationType.FriendRequestAccepted;

    protected override string? ActionLabel => "View profile";

    protected override string BuildSubject(NotificationEmailContext context) =>
        "Your friend request was accepted";

    protected override string BuildHeadline(NotificationEmailContext context) =>
        "Friend request accepted";

    protected override string BuildContentHtml(NotificationEmailContext context) =>
        GreetingHtml(context)
        + Paragraph(NotificationEmailLayout.Encode(context.Body))
        + Paragraph("You're now connected — say hello and start a conversation.");

    protected override string BuildTextBody(NotificationEmailContext context) =>
        GreetingText(context)
        + "\n\n" + context.Body
        + "\n\nYou're now connected — say hello and start a conversation.";
}
