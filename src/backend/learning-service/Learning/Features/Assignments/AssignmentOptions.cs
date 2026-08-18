namespace Sellevate.Learning.Features.Assignments;

/// <summary>Phase 40.23. Knobs for the deadline sweep, and (40.24) for the repeat sweep.</summary>
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

    /// <summary>
    /// Phase 40.24. How often the repeat sweep runs. An hour: a wave is scheduled in whole days, so
    /// the granularity that matters is the day, and the tick only decides how far into that day the
    /// refresher lands.
    /// </summary>
    public int RepeatSweepIntervalMinutes { get; init; } = 60;

    /// <summary>
    /// Phase 40.24. How late a repeat wave may be issued after its day passed. Three days: long
    /// enough that a weekend of downtime does not silently cancel a refresher, short enough that a
    /// service restarted after a fortnight does not hand somebody a "+7 day" repeat of a training
    /// they have already forgotten — or fire both waves of a series in the same minute.
    ///
    /// <para>
    /// A wave older than this is skipped permanently and logged. That is deliberate rather than a
    /// missing retry: the value of spaced repetition is in the spacing, and a wave delivered at the
    /// wrong distance from the training is not the same product feature arriving late.
    /// </para>
    /// </summary>
    public int RepeatCatchUpDays { get; init; } = 3;
}
