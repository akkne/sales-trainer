namespace Sellevate.Learning.Eventing;

public sealed record UserRegisteredEvent(Guid UserId, string Email, string DisplayName, string? AvatarKey);

public sealed record UserUpdatedEvent(Guid UserId, string DisplayName, string? AvatarKey);

public sealed record UserDeletedEvent(Guid UserId);

public sealed record UserAvatarChangedEvent(Guid UserId, string? AvatarKey);

/// <summary>
/// Phase 40.19. organization-service's <c>organization.profile.updated</c>, as this service needs
/// to read it. A local copy of the contract rather than a shared assembly, matching the four events
/// above: services agree on the wire shape, not on a type.
/// </summary>
/// <summary>
/// Phase 40.22. ai-service's <c>dialog.evaluated</c>, as this service needs to read it. Only three
/// of the eight fields are used — <c>UserId</c>, <c>ModeKey</c> and <c>QualityScore</c> — but the
/// record mirrors the producer's shape rather than trimming it, the way every other incoming
/// contract here does.
///
/// <para>
/// <c>RawScore</c> is deliberately ignored: despite the name it carries the pre-multiplier XP
/// reward, bounded by four configurable weights rather than by 100, and it says nothing about how
/// well the conversation went. <see cref="QualityScore"/> is the grade the learner was shown,
/// normalized to 0–100 by ai-service; it exists because 40.22 needed it and was added in this block.
/// </para>
/// </summary>
public sealed record DialogEvaluatedEvent(
    Guid UserId,
    string SessionId,
    Guid BundleId,
    Guid ModeId,
    int RawScore,
    int XpEarned,
    string ModeKey,
    int QualityScore);

/// <summary>
/// Phase 40.22. This service's own <c>exercise.completed</c>, consumed back off Kafka purely as a
/// trigger to re-judge assignment thresholds.
///
/// <para>
/// It carries no assignment, no lesson and no version, and it does not need to: the evaluator
/// recomputes everything from the <c>UserExerciseAttempts</c> rows the submit path already wrote.
/// The event's only job is to say "this person did something", which is why a redelivery costs a
/// recomputation and changes nothing.
/// </para>
/// </summary>
public sealed record ExerciseCompletedIntegrationEvent(Guid UserId, string ExerciseType, int Score, bool IsCorrect);

public sealed record OrganizationProfileUpdatedEvent(
    Guid OrganizationId,
    string? Product,
    string? Icp,
    string? Tone,
    string ObjectionsJson,
    string ScriptJson,
    string GlossaryJson,
    string BannedClaimsJson,
    DateTime UpdatedAt);
