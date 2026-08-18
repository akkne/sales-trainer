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

    /// <summary>Phase 40.25. The РОП commented on a fragment of the recipient's practice call.</summary>
    public const string DialogReviewCommented = "РОП прокомментировал ваш разговор";

    /// <summary>Phase 40.25. The РОП ruled on a disputed AI score.</summary>
    public const string DialogReviewResolved = "Ваше оспаривание оценки рассмотрено";

    /// <summary>
    /// Phase 40.26. The day-before digest, addressed to the РОП. The title names the problem rather
    /// than the object — «Дедлайн задания приближается» is what the manager gets, and a РОП who reads
    /// the same words assumes it is the same notice and stops opening it.
    /// </summary>
    public const string AssignmentDeadlineDigest = "Завтра дедлайн, а команда не начала";

    /// <summary>Phase 40.26. A manager disputed an AI score and it is waiting for the РОП.</summary>
    public const string DialogReviewDisputed = "Менеджер оспорил оценку";
}
