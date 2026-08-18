namespace Sellevate.Notification.Features.Notifications.Models;

public enum NotificationType
{
    FriendRequestReceived = 1,
    FriendRequestAccepted = 2,
    ChatMessageReceived = 3,
    AchievementUnlocked = 4,
    StreakMilestone = 5,
    DiscussReplyReceived = 6,
    // 7 was LeagueUpdated — league notifications were removed; the value is retired
    // (never reused) so any pre-existing stored notifications still deserialize.
    UserWelcome = 8,
    CompanyFollowUpDue = 9,

    // Phase 40.23 — the assignment family. Three values rather than one with a sub-kind, because
    // the three are read differently by the person receiving them: "you have new work", "the clock
    // is running out", "your РОП is asking". A single type with a discriminator inside the body
    // would make the third indistinguishable from the second in the inbox, and the third is the one
    // that exists precisely because the first two were ignored.
    AssignmentIssued = 10,
    AssignmentDeadlineApproaching = 11,
    AssignmentReminder = 12,

    // Phase 40.25 — the feedback loop (docs/TENANCY/ASSIGNMENTS.md §4.1). Two values for the same
    // reason the assignment family has three: "your РОП commented on your call" and "your dispute
    // was ruled on" are read completely differently, and the second only exists because somebody
    // asked a question they are waiting on an answer to.
    DialogReviewCommented = 13,
    DialogReviewResolved = 14
}
