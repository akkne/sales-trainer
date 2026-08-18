namespace Sellevate.Notification.Common.Constants;

/// <summary>
/// Every user-visible notification title in the service, so two notices can never disagree about
/// the same event. The set is deliberately bilingual: the early social titles are English, while
/// everything from Phase 39 onwards is Russian because the product's interface language is Russian
/// and those notices go to a working manager rather than to a platform notion. They live here
/// rather than inline so the wording is in one place when somebody finally reconciles the two
/// conventions.
/// </summary>
public static class NotificationTitles
{
    public const string FriendRequestReceived = "New friend request";
    public const string FriendRequestAccepted = "Friend request accepted";
    public const string ChatMessageReceived = "New message";
    public const string AchievementUnlocked = "Achievement unlocked";
    public const string StreakMilestone = "Streak milestone reached";
    public const string DiscussReplyReceived = "New reply to your discussion";
    public const string Welcome = "Welcome to Sellevate";

    /// <summary>
    /// Phase 39. The only title that names its subject, so it is a method rather than a constant.
    /// </summary>
    public static string CompanyFollowUpDue(string companyName) => $"Пора связаться с {companyName}";

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
