using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Emails.Templates;

/// <summary>Email for the weekly league rollover (promotion, demotion or a new week).</summary>
public sealed class LeagueUpdatedEmailTemplate : NotificationEmailTemplate
{
    public override NotificationType NotificationType => NotificationType.LeagueUpdated;

    protected override string? ActionLabel => "View league";

    protected override string BuildSubject(NotificationEmailContext context) =>
        "Your Sellevate league was updated";

    protected override string BuildHeadline(NotificationEmailContext context) =>
        "League update";

    protected override string BuildContentHtml(NotificationEmailContext context) =>
        GreetingHtml(context)
        + Paragraph(NotificationEmailLayout.Encode(context.Body))
        + Paragraph("Check the leaderboard to see where you stand this week.");

    protected override string BuildTextBody(NotificationEmailContext context) =>
        GreetingText(context)
        + "\n\n" + context.Body
        + "\n\nCheck the leaderboard to see where you stand this week.";
}
