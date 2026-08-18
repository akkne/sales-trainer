namespace Sellevate.Learning.Features.Assignments;

/// <summary>Phase 40.23. Knobs for the deadline sweep.</summary>
public sealed class AssignmentOptions
{
    public const string SectionName = "Assignments";

    /// <summary>
    /// How far ahead of a deadline the notice goes out. A day by default, which is the window
    /// docs/TENANCY/ASSIGNMENTS.md §5 and roadmap 40.26 both name: long enough that somebody can
    /// still do the work, short enough that it reads as urgent rather than as filing.
    /// </summary>
    public int DeadlineNoticeLeadHours { get; init; } = 24;

    /// <summary>
    /// How often the sweep runs. Half an hour: the notice names a date, not a minute, so a finer
    /// tick buys nothing and a coarser one risks skipping a deadline set inside the lead window.
    /// </summary>
    public int SweepIntervalMinutes { get; init; } = 30;
}
