using Sellevate.Learning.Features.Assignments.Models;

namespace Sellevate.Learning.Features.Assignments.Services.Abstract;

/// <summary>
/// Phase 40.23. What the person on the receiving end of an assignment can ask about it
/// (docs/TENANCY/ASSIGNMENTS.md §1, roadmap 40.23: "активное задание — первым экраном у менеджера,
/// пока не выполнено").
///
/// <para>
/// Deliberately separate from <c>IAssignmentService</c>, which is the РОП's lifecycle surface. The
/// two answer different questions about the same rows — "who did I ask and where are they" versus
/// "what am I supposed to do" — and one interface answering both is how a learner-facing route ends
/// up one refactor away from returning the whole team's scores.
/// </para>
/// </summary>
public interface IMyAssignmentService
{
    /// <summary>
    /// The caller's own unfinished assignments, soonest deadline first. Never takes a user id from
    /// the request: the caller is the token, the same rule <c>ProgramController</c> follows.
    /// </summary>
    Task<IReadOnlyList<ActiveAssignmentDto>> GetActiveForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 40.23. The persona and framing for a practice conversation this person owes on the
    /// named dialog mode, or <see langword="null"/> when they owe none.
    ///
    /// <para>
    /// Called by ai-service at session start, not by a browser. When several open assignments name
    /// the same mode — a repeat from 40.24 is exactly that — the one with the nearest deadline wins,
    /// because that is the one the person is about to be late for.
    /// </para>
    /// </summary>
    Task<AssignmentPracticeContextDto?> GetPracticeContextAsync(
        Guid userId,
        string dialogModeKey,
        CancellationToken cancellationToken = default);
}
