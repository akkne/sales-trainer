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
    CompanyFollowUpDue = 9
}
