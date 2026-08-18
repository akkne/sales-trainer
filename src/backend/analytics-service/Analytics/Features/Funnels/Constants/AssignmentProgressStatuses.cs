namespace Sellevate.Analytics.Features.Funnels.Constants;

/// <summary>
/// Phase 40.25. The four values of learning-service's own <c>AssignmentProgressStatuses</c>, copied
/// rather than referenced: analytics-service does not depend on learning-service and must not start
/// to for a list of four strings.
///
/// <para>
/// These become the <c>state</c> label of <c>app_assignment_progress_total</c>, so the set is also
/// the label's cardinality bound — it is safe precisely because it is compiled in. A copied list can
/// drift from the producer, which is why <see cref="Known"/> exists and an unrecognised status is
/// dropped rather than folded into a catch-all bucket: a fifth status added upstream must not open a
/// new Prometheus series, and a number pooled under "other" is one nobody can act on.
/// </para>
/// </summary>
public static class AssignmentProgressStatuses
{
    public const string NotStarted = "not_started";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string FailedThreshold = "failed_threshold";

    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        NotStarted,
        InProgress,
        Completed,
        FailedThreshold,
    };
}
