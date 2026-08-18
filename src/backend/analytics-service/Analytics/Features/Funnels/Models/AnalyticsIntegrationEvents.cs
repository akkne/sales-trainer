namespace Sellevate.Analytics.Features.Funnels.Models;

public sealed record UserRegisteredEvent(Guid UserId, string Email, string DisplayName, string? AvatarKey);

public sealed record ExerciseCompletedEvent(Guid UserId, string ExerciseType, int Score, bool IsCorrect);

public sealed record ExperiencePointsGrantedEvent(Guid UserId, int Amount, string Source);

/// <summary>Phase 40.25. Published by learning-service once per recipient when an assignment is
/// issued. Only its existence is counted — no field of it is stored.</summary>
public sealed record AssignmentIssuedEvent(
    Guid AssignmentId,
    Guid UserId,
    string Title,
    string? Goal,
    DateTime? Deadline);

/// <summary>Phase 40.25. Published by learning-service's threshold evaluator when one person's
/// standing on one assignment changes state. Only <see cref="Status"/> is read, as a bounded
/// Prometheus label; the rest of the payload exists for whoever is reading a log.</summary>
public sealed record AssignmentProgressChangedEvent(
    Guid AssignmentId,
    Guid UserId,
    string PreviousStatus,
    string Status,
    int? BestScore,
    int AttemptCount);
