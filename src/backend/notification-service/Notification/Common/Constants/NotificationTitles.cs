namespace Sellevate.Notification.Common.Constants;

public static class NotificationTitles
{
    public const string FriendRequestReceived = "New friend request";
    public const string FriendRequestAccepted = "Friend request accepted";
    public const string ChatMessageReceived = "New message";
    public const string AchievementUnlocked = "Achievement unlocked";
    public const string StreakMilestone = "Streak milestone reached";
    public const string DiscussReplyReceived = "New reply to your discussion";
    public const string Welcome = "Welcome to Sellevate";

    // Phase 40.23. Russian, unlike the titles above and like Phase 39's follow-up reminder, which
    // builds its title inline in Russian: the product's interface language is Russian and these
    // three go to a working manager rather than to a platform notion. They live here rather than
    // inline so the wording is in one place when somebody finally reconciles the two conventions.
    public const string AssignmentIssued = "Вам назначено задание";
    public const string AssignmentDeadlineApproaching = "Дедлайн задания приближается";
    public const string AssignmentReminder = "Напоминание о задании";
}
