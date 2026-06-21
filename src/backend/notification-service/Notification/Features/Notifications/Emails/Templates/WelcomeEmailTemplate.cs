using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Emails.Templates;

/// <summary>Onboarding email sent to a user right after they register.</summary>
public sealed class WelcomeEmailTemplate : NotificationEmailTemplate
{
    public override NotificationType NotificationType => NotificationType.UserWelcome;

    protected override string? ActionLabel => "Start training";

    protected override string BuildSubject(NotificationEmailContext context) =>
        "Welcome to Sellevate";

    protected override string BuildHeadline(NotificationEmailContext context) =>
        "Welcome aboard";

    protected override string BuildContentHtml(NotificationEmailContext context) =>
        GreetingHtml(context)
        + Paragraph("Your Sellevate account is ready. Sharpen your sales skills with realistic "
            + "practice scenarios, track your progress, and climb the leaderboard with friends.")
        + Paragraph("Jump in and run your first training session whenever you're ready.");

    protected override string BuildTextBody(NotificationEmailContext context) =>
        GreetingText(context)
        + "\n\nYour Sellevate account is ready. Sharpen your sales skills with realistic practice "
        + "scenarios, track your progress, and climb the leaderboard with friends."
        + "\n\nJump in and run your first training session whenever you're ready.";
}
