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

/// <summary>Published by company-service's follow-up reminder poll when a scheduled
/// <c>Company.NextActionAt</c> becomes due and has not yet been notified. Field names match the
/// wire contract in <c>company-service</c>'s <c>CompanyIntegrationEvents.CompanyFollowUpDueEvent</c>.</summary>
public sealed record CompanyFollowUpDueEvent(
    Guid CompanyId,
    Guid UserId,
    string CompanyName,
    DateTime NextActionAt,
    string? Note);

/// <summary>Published by learning-service when an assignment is issued to one named person
/// (Phase 40.23) — one event per resolved recipient, staged in the same transaction as their
/// progress row. Field names match learning-service's
/// <c>OutgoingIntegrationEvents.AssignmentIssuedEvent</c>.</summary>
public sealed record AssignmentIssuedEvent(
    Guid AssignmentId,
    Guid UserId,
    string Title,
    string? Goal,
    DateTime? Deadline);

/// <summary>Published by learning-service's deadline sweep when an assignment this person has not
/// finished is close to its due date (Phase 40.23). <see cref="Deadline"/> is part of the dedupe
/// key, so extending a deadline arms a fresh notice rather than being swallowed by the old one —
/// the same trick <see cref="CompanyFollowUpDueEvent"/> uses for a rescheduled follow-up.</summary>
public sealed record AssignmentDeadlineApproachingEvent(
    Guid AssignmentId,
    Guid UserId,
    string Title,
    DateTime Deadline);

/// <summary>Published by learning-service when a РОП presses "remind" on an assignment
/// (Phase 40.23). Deliberately its own type rather than a re-send of
/// <see cref="AssignmentIssuedEvent"/>: this one came from a person, and it is the escalation that
/// exists because the first two were ignored.</summary>
public sealed record AssignmentReminderEvent(
    Guid AssignmentId,
    Guid UserId,
    string Title,
    DateTime? Deadline,
    DateTime RequestedAt);

// User-profile replica events — consumed to resolve a recipient's email/display name locally
// (the notification service has no database, so the replica is held in Redis).
public sealed record UserRegisteredEvent(Guid UserId, string Email, string DisplayName, string? AvatarKey);

public sealed record UserUpdatedEvent(Guid UserId, string DisplayName, string? AvatarKey);

public sealed record UserDeletedEvent(Guid UserId);
