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
