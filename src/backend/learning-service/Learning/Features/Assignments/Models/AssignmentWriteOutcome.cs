namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.21. Why a write did or did not happen, so the controller can tell 404 from 409 without
/// the service throwing for either. Payload problems are a third case and travel as
/// <c>AssignmentValidationException</c> — they are the caller's mistake, not a state of the row.
/// </summary>
public enum AssignmentWriteOutcome
{
    Applied,

    /// <summary>No such assignment in the caller's organization.</summary>
    NotFound,

    /// <summary>The assignment exists, and its current status does not permit what was asked.</summary>
    RejectedByStatus,
}
