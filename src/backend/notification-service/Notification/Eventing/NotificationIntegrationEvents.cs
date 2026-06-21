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

/// <summary>Published by Social when a recipient opens/reads a conversation. Cancels any
/// pending "unread chat message" email for that recipient + conversation up to <see cref="ReadAt"/>.</summary>
public sealed record ChatMessageReadEvent(
    Guid ReaderUserId,
    Guid? ConversationId,
    DateTime ReadAt);

/// <summary>Published by Social when someone replies to a discussion thread. The thread
/// author (<see cref="RecipientId"/>) is notified, unless they authored the reply themselves.</summary>
public sealed record DiscussReplyCreatedEvent(
    Guid RecipientId,
    Guid ReplyAuthorId,
    string ReplyAuthorName,
    Guid ThreadId,
    string ThreadTitle,
    Guid ReplyId,
    string Preview);

/// <summary>Published by Gamification when the weekly league rolls over. One event per member;
/// <see cref="Outcome"/> is "promoted", "demoted" or "stayed". <see cref="LeagueId"/> identifies
/// the resulting week's league so each weekly rollover yields a fresh (dedupe-distinct) notification.</summary>
public sealed record LeagueUpdatedEvent(
    Guid UserId,
    Guid LeagueId,
    string PreviousTier,
    string NewTier,
    string Outcome,
    int Rank);

// User-profile replica events — consumed to resolve a recipient's email/display name locally
// (the notification service has no database, so the replica is held in Redis).
public sealed record UserRegisteredEvent(Guid UserId, string Email, string DisplayName, string? AvatarKey);

public sealed record UserUpdatedEvent(Guid UserId, string DisplayName, string? AvatarKey);

public sealed record UserDeletedEvent(Guid UserId);
