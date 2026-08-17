namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.21. Where one person stands on one assignment (docs/TENANCY/ASSIGNMENTS.md §1.1).
///
/// <para>
/// <b><see cref="FailedThreshold"/> is the reason this vocabulary is not a copy of
/// <c>LessonProgressStatuses</c>.</b> A lesson is either finished or not; an assignment is finished
/// only when a quality threshold is met, so "started, tried four times, still under threshold" has to
/// be a state the РОП can see rather than an invisible retry loop. The roadmap calls that the most
/// valuable row on the screen, and a status vocabulary that cannot express it turns the dashboard
/// into the compliance theatre 40.22 exists to prevent.
/// </para>
///
/// <para>
/// Nothing writes these values in 40.21. The fan-out that creates the rows is 40.23 and the
/// threshold evaluation that moves them between <see cref="Completed"/> and
/// <see cref="FailedThreshold"/> is 40.22 — see docs/DONT_FORGET.md.
/// </para>
/// </summary>
public static class AssignmentProgressStatuses
{
    /// <summary>Issued to this person, never opened. The row exists so "who has not started" is a query, not an absence.</summary>
    public const string NotStarted = "not_started";

    /// <summary>Opened at least once and not yet judged against the completion rule.</summary>
    public const string InProgress = "in_progress";

    /// <summary>The completion rule was met. Never set by a click — that is the whole argument of 40.22.</summary>
    public const string Completed = "completed";

    /// <summary>
    /// Attempted, judged, and under the threshold. A visible, terminal-looking state rather than a
    /// hidden retry: this person needs coaching and the РОП needs to know their name.
    /// </summary>
    public const string FailedThreshold = "failed_threshold";

    public static bool IsKnown(string status)
        => status is NotStarted or InProgress or Completed or FailedThreshold;
}
