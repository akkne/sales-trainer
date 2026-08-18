namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.21. The result of an update, a status transition or a deletion: what happened, the row as
/// it now stands when there still is one, and a sentence explaining a refusal.
/// </summary>
public sealed record AssignmentWriteResult(
    AssignmentWriteOutcome Outcome,
    AssignmentDto? Assignment = null,
    string? RefusalReason = null)
{
    public static AssignmentWriteResult NotFound() => new(AssignmentWriteOutcome.NotFound);

    public static AssignmentWriteResult RejectedByStatus(string refusalReason)
        => new(AssignmentWriteOutcome.RejectedByStatus, null, refusalReason);

    public static AssignmentWriteResult Applied(AssignmentDto? assignment = null)
        => new(AssignmentWriteOutcome.Applied, assignment);
}
