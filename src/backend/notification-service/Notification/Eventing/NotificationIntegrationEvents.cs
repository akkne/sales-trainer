namespace Sellevate.Notification.Eventing;

public sealed record AchievementUnlockedEvent(
    Guid UserId,
    string AchievementKey,
    string Title);

public sealed record StreakMilestoneEvent(
    Guid UserId,
    int DayCount,
    int BonusXp);

public sealed record FriendRequestReceivedEvent(
    Guid RecipientId,
    string RequesterName,
    Guid? RequesterId,
    Guid? FriendshipId);

public sealed record FriendRequestAcceptedEvent(
    Guid RecipientId,
    string AccepterName,
    Guid? AccepterId);

public sealed record ChatMessageSentEvent(
    Guid RecipientId,
    string SenderName,
    string Preview,
    Guid? ConversationId);
