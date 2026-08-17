using Sellevate.Ai.Features.Dialog.Models;

namespace Sellevate.Ai.Infrastructure.Learning;

/// <summary>
/// Phase 40.23. Asks learning-service whether the conversation about to start is a piece of work
/// this person was assigned, and who the AI should be playing if so.
/// </summary>
public interface IAssignmentPracticeContextClient
{
    /// <summary>
    /// The context, or <see langword="null"/> when this person owes no conversation on this mode —
    /// which is the ordinary answer, because most practice is not assigned.
    ///
    /// <para>
    /// Never throws. A failure to reach learning-service degrades to <see langword="null"/> on
    /// purpose: the alternative is a practice screen that will not open because a service unrelated
    /// to practising is down, and the cost of the degradation is a conversation without the
    /// assignment's persona whose score still counts towards the threshold. That trade is recorded
    /// in docs/DECISIONS.md and in docs/DONT_FORGET.md, because it is a real, if rare, way for an
    /// assignment to be practised against the wrong character.
    /// </para>
    /// </summary>
    Task<AssignmentPracticeContext?> GetPracticeContextAsync(
        Guid userId,
        string dialogModeKey,
        CancellationToken cancellationToken = default);
}
