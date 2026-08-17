namespace Sellevate.Learning.Features.Assignments.Services.Abstract;

/// <summary>
/// Phase 40.22. Decides whether one person has met the completion rule of the assignments they are
/// currently carrying (docs/TENANCY/ASSIGNMENTS.md §1.1).
/// </summary>
internal interface IAssignmentThresholdEvaluator
{
    /// <summary>
    /// Re-judges every open assignment progress row belonging to <paramref name="userId"/> in the
    /// organization currently in tenant context, and returns how many rows changed.
    ///
    /// <para>
    /// <b>Recomputes; never increments.</b> Both numbers the РОП reads — how many times somebody
    /// tried and how close they got — are derived from the attempt rows that already exist, so
    /// calling this twice for the same event produces the same result as calling it once. That is
    /// the whole idempotency story: there is no counter to inflate.
    /// </para>
    ///
    /// <para>
    /// <b>It updates rows; it never creates one.</b> A progress row means "this person was asked to
    /// do this", which is a fact about issue time and belongs to 40.23's fan-out. Creating one here
    /// would make somebody who happened to practise a referenced lesson appear on the РОП's screen
    /// as though they had been assigned it.
    /// </para>
    /// </summary>
    Task<int> EvaluateForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
