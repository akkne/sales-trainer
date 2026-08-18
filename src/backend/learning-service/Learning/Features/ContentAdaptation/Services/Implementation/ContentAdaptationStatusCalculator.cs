using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.ContentAdaptation.Models;

namespace Sellevate.Learning.Features.ContentAdaptation.Services.Implementation;

/// <summary>
/// Phase 40.32. Reads a batch's status off its items, so that nothing has to remember to keep a
/// counter true.
///
/// <para>
/// <b>The column is a projection, not a fact.</b> Every writer that touches an item recomputes the
/// batch's status from the items it can see, inside the same transaction. A tick that dies after
/// answering three items and before updating a counter therefore leaves nothing wrong — there is no
/// counter, and the next read of the items gives the same answer the dead tick would have written.
/// The column exists so the worker's enumeration and the admin list can be indexed; it is never the
/// source of truth about what is done. Same rule as 40.22's «derive state, never increment», and the
/// same rule 40.18 used for staleness and 40.31 for the suggestion panel.
/// </para>
/// </summary>
internal static class ContentAdaptationStatusCalculator
{
    /// <summary>
    /// A batch is <c>preparing</c> while anything still owes a call, <c>awaiting_review</c> while
    /// anything still owes a person an answer, and <c>completed</c> otherwise. <c>failed</c> is
    /// reserved for the one case where neither of the first two is true and no proposal was ever
    /// produced — every item burned its attempts — because a batch that produced fifty good
    /// proposals and lost ten is a batch that succeeded and has ten failures on it.
    /// </summary>
    public static string Compute(IReadOnlyCollection<ContentAdaptationItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            return ContentAdaptationStatuses.Completed;
        }

        if (items.Any(item => item.Status == ContentAdaptationItemStatuses.Pending))
        {
            return ContentAdaptationStatuses.Preparing;
        }

        if (items.Any(item => ContentAdaptationItemStatuses.Unresolved.Contains(item.Status)))
        {
            return ContentAdaptationStatuses.AwaitingReview;
        }

        return items.All(item => item.Status == ContentAdaptationItemStatuses.Failed)
            ? ContentAdaptationStatuses.Failed
            : ContentAdaptationStatuses.Completed;
    }

    /// <summary>
    /// Applies the computed status to the batch, together with the two fields that follow from it.
    /// Returns true when anything changed, so a caller can skip a pointless <c>UpdatedAt</c> bump.
    /// </summary>
    public static bool Apply(
        ContentAdaptationJob job,
        IReadOnlyCollection<ContentAdaptationItem> items,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(job);

        var status = Compute(items);
        var completedAt = status is ContentAdaptationStatuses.Completed or ContentAdaptationStatuses.Failed
            ? job.CompletedAt ?? now
            : (DateTime?)null;

        if (job.Status == status && job.CompletedAt == completedAt)
        {
            return false;
        }

        job.Status = status;
        job.CompletedAt = completedAt;
        job.UpdatedAt = now;

        return true;
    }
}
