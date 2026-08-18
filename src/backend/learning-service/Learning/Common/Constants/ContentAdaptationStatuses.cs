namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.32. The states of one batch of per-exercise proposals.
///
/// <para>
/// <b>Every one of these is derived from the batch's items and never counted.</b> A batch is
/// <see cref="Preparing"/> while an item still has no proposal, <see cref="AwaitingReview"/> the
/// moment the last proposal lands and while any of them is still unanswered, and
/// <see cref="Completed"/> when a person has answered all of them. The column exists so the worker's
/// enumeration and the admin list can be indexed, not so anything can disagree with the items — it
/// is recomputed from them after every write (<c>ContentAdaptationStatusCalculator</c>). That is
/// 40.22's rule, and 40.27 applied it to the half of a run denominated in money; here it is applied
/// to the half denominated in a person's attention.
/// </para>
/// </summary>
public static class ContentAdaptationStatuses
{
    /// <summary>
    /// At least one item still has no proposal. The only worker-owned state, and the only one in
    /// which the batch costs money.
    /// </summary>
    public const string Preparing = "preparing";

    /// <summary>
    /// Every item has been answered by the model and at least one is still waiting for a person.
    /// <b>This is where the block lives.</b> Nothing has been applied, nothing will be applied on
    /// its own, and the batch will sit here indefinitely rather than time out into an auto-apply.
    /// </summary>
    public const string AwaitingReview = "awaiting_review";

    /// <summary>A person has accepted or rejected every item. Terminal.</summary>
    public const string Completed = "completed";

    /// <summary>
    /// Every item ran out of attempts. Terminal until a person retries, and a retry re-queues only
    /// the failed items — a batch that produced fifty good proposals and lost ten must not re-pay
    /// for the fifty.
    /// </summary>
    public const string Failed = "failed";

    public static readonly string[] All = [Preparing, AwaitingReview, Completed, Failed];

    /// <summary>
    /// The states in which a batch holds a person's attention, and therefore the states in which a
    /// second batch over the same stage would be noise rather than help. The partial unique index
    /// <c>UX_ContentAdaptationJobs_Live</c> is defined over exactly this list.
    /// </summary>
    public static readonly string[] Live = [Preparing, AwaitingReview];

    /// <summary>The one state a background worker acts on. Every other state waits for a person.</summary>
    public static readonly string[] WorkerOwned = [Preparing];

    public static bool IsKnown(string? status) => status is not null && All.Contains(status);
}

/// <summary>
/// Phase 40.32. The states of one item — one exercise's proposal — inside a batch.
///
/// <para>
/// <b><see cref="Accepted"/> and <see cref="Rejected"/> are the only two states no code path other
/// than an HTTP request from an organization administrator can write</b>, and that is the roadmap's
/// «никогда не автоприменение» expressed as a state machine rather than as a comment. The worker
/// moves an item from <see cref="Pending"/> to <see cref="Proposed"/>, <see cref="Unchanged"/> or
/// <see cref="Failed"/>, and has no branch that reaches the other two.
/// </para>
/// </summary>
public static class ContentAdaptationItemStatuses
{
    /// <summary>Queued. No LLM call has been paid for on this exercise yet.</summary>
    public const string Pending = "pending";

    /// <summary>
    /// The model answered and there is something for a person to look at. In rewrite mode that is a
    /// proposed body plus a change list; in review mode, a list of findings.
    /// </summary>
    public const string Proposed = "proposed";

    /// <summary>
    /// The model answered and there is nothing to look at — the rewrite came back byte-identical to
    /// the current body, or the review found nothing. Resolved without a person, because an empty
    /// diff in a review queue trains people to click through the queue without reading it.
    /// </summary>
    public const string Unchanged = "unchanged";

    /// <summary>A person applied the proposal. Only reachable through the accept route.</summary>
    public const string Accepted = "accepted";

    /// <summary>A person said no. Only reachable through the reject route.</summary>
    public const string Rejected = "rejected";

    /// <summary>Out of attempts on this one exercise. The rest of the batch is unaffected.</summary>
    public const string Failed = "failed";

    public static readonly string[] All =
    [
        Pending,
        Proposed,
        Unchanged,
        Accepted,
        Rejected,
        Failed
    ];

    /// <summary>Items still waiting for a person — the review queue, and the reason a batch stays open.</summary>
    public static readonly string[] Unresolved = [Proposed];

    public static bool IsKnown(string? status) => status is not null && All.Contains(status);
}
