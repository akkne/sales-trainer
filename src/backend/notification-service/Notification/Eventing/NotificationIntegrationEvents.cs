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

/// <summary>Published by learning-service's deadline sweep, a day before the deadline, to each
/// administrator of the organization (Phase 40.26). <see cref="NotStartedNames"/> is the readable
/// prefix of <see cref="NotStartedCount"/>, not the whole of it: the body has to open with names to
/// be a thing to act on rather than a report, and it has to stay one sentence to be read at all.
/// <see cref="Deadline"/> is part of the dedupe key for the same reason it is on
/// <see cref="AssignmentDeadlineApproachingEvent"/>.</summary>
public sealed record AssignmentDeadlineDigestEvent(
    Guid AssignmentId,
    Guid AdministratorUserId,
    string Title,
    DateTime Deadline,
    int NotStartedCount,
    IReadOnlyList<string>? NotStartedNames);

/// <summary>Published by learning-service when a manager disputes an AI score (Phase 40.26),
/// addressed to each administrator of the organization. The counterpart of
/// <see cref="DialogReviewResolvedEvent"/>, which travels the other way.</summary>
public sealed record DialogReviewDisputedEvent(
    Guid NoteId,
    Guid AdministratorUserId,
    Guid SubjectUserId,
    string? SubjectDisplayName,
    string SessionId,
    int? DisputedScore,
    string Comment);

/// <summary>Published by learning-service when the РОП comments on a fragment of somebody's
/// practice conversation (Phase 40.25). The quoted lines travel with the event rather than being
/// fetched, because this service has no database beyond its inbox and because a notice reading
/// "you have a comment" is one more thing to ignore.</summary>
public sealed record DialogReviewCommentedEvent(
    Guid NoteId,
    Guid UserId,
    string SessionId,
    string? QuotedText,
    string Comment);

/// <summary>Published by learning-service when the РОП rules on a disputed AI score (Phase 40.25).
/// <see cref="Outcome"/> travels because "upheld" and "rejected" read completely differently to the
/// person who filed it, and a notice that says only "reviewed" recreates the black box the dispute
/// mechanism exists to open.</summary>
public sealed record DialogReviewResolvedEvent(
    Guid NoteId,
    Guid UserId,
    string SessionId,
    string Outcome,
    int? DisputedScore,
    int? AdjustedScore,
    string? Resolution);

// User-profile replica events — consumed to resolve a recipient's email/display name locally
// (the notification service has no database, so the replica is held in Redis).
public sealed record UserRegisteredEvent(Guid UserId, string Email, string DisplayName, string? AvatarKey);

public sealed record UserUpdatedEvent(Guid UserId, string DisplayName, string? AvatarKey);

public sealed record UserDeletedEvent(Guid UserId);
