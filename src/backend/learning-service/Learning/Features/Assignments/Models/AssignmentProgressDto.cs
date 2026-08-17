namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.21. One person's standing on one assignment. Empty for every assignment until 40.23
/// issues them.
/// </summary>
public sealed record AssignmentProgressDto(
    Guid UserId,
    string Status,
    int? BestScore,
    int AttemptCount,
    DateTime? FirstOpenedAt,
    DateTime? CompletedAt);
