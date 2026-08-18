namespace Sellevate.Notification.Features.Notifications.Models;

/// <summary>
/// The kind of a notification. Persisted as its integer value inside the JSON record in Redis, so
/// <b>a value is never renumbered and a retired value is never reused</b> — 7 was
/// <c>LeagueUpdated</c>, removed with the league feature, and stays vacant so notifications stored
/// before the removal still deserialize.
///
/// <para>
/// The families are split by who reads them and what they are meant to make happen, not by object:
/// 10–12 are three separate values because "you have new work", "the clock is running out" and
/// "your РОП is asking" are read differently by the same person, and the third exists precisely
/// because the first two were ignored; 13–14 likewise; and 15–16 are separate from 10–12 because the
/// recipient changes — an inbox in which "your deadline is close" and "your team has not started"
/// look alike is an inbox where the second one is skipped. See docs/NOTIFICATIONS.md and
/// docs/TENANCY/ASSIGNMENTS.md §4.1, §5.
/// </para>
/// </summary>
public enum NotificationType
{
    FriendRequestReceived = 1,
    FriendRequestAccepted = 2,
    ChatMessageReceived = 3,
    AchievementUnlocked = 4,
    StreakMilestone = 5,
    DiscussReplyReceived = 6,
    UserWelcome = 8,
    CompanyFollowUpDue = 9,

    AssignmentIssued = 10,
    AssignmentDeadlineApproaching = 11,
    AssignmentReminder = 12,

    DialogReviewCommented = 13,
    DialogReviewResolved = 14,

    AssignmentDeadlineDigest = 15,
    DialogReviewDisputed = 16
}
