namespace Sellevate.Learning.Eventing;

public sealed record ExerciseCompletedEvent(Guid UserId, string ExerciseType, int Score, bool IsCorrect);

public sealed record LessonCompletedEvent(Guid UserId, Guid LessonId, int BestScore);

public sealed record SkillCompletedEvent(Guid UserId, Guid SkillId);

/// <summary>
/// Phase 40.23. An assignment was issued to one named person — one event per resolved recipient,
/// staged in the same transaction as their <c>AssignmentProgressRecords</c> row.
///
/// <para>
/// Per recipient rather than one event carrying the list, for two reasons that both matter. The
/// partition key is the user id everywhere in this system, so per-user ordering only exists if the
/// user is the key; and notification-service's dedupe is per recipient, so a redelivered batch event
/// would have to be unpacked into the same per-person keys anyway — with the fan-out moved into the
/// consumer, where a partial failure loses part of a batch instead of retrying one message.
/// </para>
/// </summary>
public sealed record AssignmentIssuedEvent(
    Guid AssignmentId,
    Guid UserId,
    string Title,
    string? Goal,
    DateTime? Deadline);

/// <summary>
/// Phase 40.23. The deadline of an assignment this person has not finished is close. Published by
/// <c>AssignmentDeadlineNoticeService</c>; <see cref="Deadline"/> travels because it is part of the
/// consumer's dedupe key, so extending a deadline arms a fresh notice instead of being swallowed by
/// the notice for the date that no longer applies.
/// </summary>
public sealed record AssignmentDeadlineApproachingEvent(
    Guid AssignmentId,
    Guid UserId,
    string Title,
    DateTime Deadline);

/// <summary>
/// Phase 40.23. A РОП pressed "remind" on an assignment. <see cref="RequestedAt"/> is the dedupe
/// key's second half: a second press tomorrow has to reach the person, while a Kafka redelivery of
/// today's press must not.
/// </summary>
public sealed record AssignmentReminderEvent(
    Guid AssignmentId,
    Guid UserId,
    string Title,
    DateTime? Deadline,
    DateTime RequestedAt);

/// <summary>
/// Phase 40.25. One person's standing on one assignment moved between the four funnel states
/// (docs/TENANCY/ASSIGNMENTS.md §4).
///
/// <para>
/// <b>It carries the transition, not the funnel.</b> The only consumer is analytics-service, which
/// increments a platform-wide counter labelled by the new state and stores nothing — no user, no
/// assignment, no organization (docs/ANALYTICS_SERVICE.md). The fields beyond
/// <see cref="Status"/> travel because an event that cannot be read in a log is an event nobody can
/// debug, not because anything downstream aggregates on them.
/// </para>
/// </summary>
public sealed record AssignmentProgressChangedEvent(
    Guid AssignmentId,
    Guid UserId,
    string PreviousStatus,
    string Status,
    int? BestScore,
    int AttemptCount);

/// <summary>
/// Phase 40.25. The РОП commented on a fragment of this person's conversation
/// (docs/TENANCY/ASSIGNMENTS.md §4.1).
///
/// <para>
/// The quoted fragment travels with the event rather than being fetched by the consumer.
/// notification-service has no database of its own beyond its inbox and no business reading
/// learning-db, and the whole value of the notice is that it opens with the three lines being talked
/// about — a notification saying "you have a comment" is one more thing to ignore.
/// </para>
/// </summary>
public sealed record DialogReviewCommentedEvent(
    Guid NoteId,
    Guid UserId,
    string SessionId,
    string? QuotedText,
    string Comment);

/// <summary>
/// Phase 40.25. The РОП ruled on a disputed AI score.
///
/// <para>
/// <see cref="Outcome"/> travels because the two outcomes read completely differently to the person
/// who filed the dispute, and a notice that says only "your dispute was reviewed" would recreate the
/// black box §4.1 exists to open.
/// </para>
/// </summary>
public sealed record DialogReviewResolvedEvent(
    Guid NoteId,
    Guid UserId,
    string SessionId,
    string Outcome,
    int? DisputedScore,
    int? AdjustedScore,
    string? Resolution);
