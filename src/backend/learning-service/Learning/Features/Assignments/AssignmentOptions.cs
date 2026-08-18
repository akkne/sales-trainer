namespace Sellevate.Learning.Features.Assignments;

/// <summary>
/// Phase 40.23. Knobs for the deadline sweep, and (40.24) for the repeat sweep.
///
/// <para>
/// <b>Every knob is clamped, and the clamp lives here rather than at the call site.</b> Each value is
/// read in two places — the sweep that enumerates organizations and the per-organization service that
/// acts on them — and the two must agree exactly: a horizon computed from a wider bound than the
/// enumeration used would visit organizations that have nothing to do, and a narrower one would mark a
/// deadline announced without announcing it. The <c>Effective*</c> members below are the only sanctioned
/// readers of the raw properties for that reason. A misconfigured value is clamped rather than refused
/// because a background job that will not start is a job whose silence nobody can distinguish from
/// "nothing was due".
/// </para>
/// </summary>
public sealed class AssignmentOptions
{
    public const string SectionName = "Assignments";

    /// <summary>An hour. Below this the notice cannot outrun the sweep interval that has to deliver it.</summary>
    public const int MinimumDeadlineNoticeLeadHours = 1;

    /// <summary>Thirty days. Past that "approaching" stops meaning anything to the person reading it.</summary>
    public const int MaximumDeadlineNoticeLeadHours = 24 * 30;

    /// <summary>A minute, for either sweep. A tick faster than this is a busy loop over the whole estate.</summary>
    public const int MinimumSweepIntervalMinutes = 1;

    /// <summary>A day, for either sweep. A tick slower than this can step over a whole lead window.</summary>
    public const int MaximumSweepIntervalMinutes = 24 * 60;

    /// <summary>A day. A zero-day catch-up window would drop every wave the sweep did not meet on the minute.</summary>
    public const int MinimumRepeatCatchUpDays = 1;

    /// <summary>
    /// Ninety days, which is also the heat map's window. Past that a "late" wave is a refresher of a
    /// training nobody remembers attending.
    /// </summary>
    public const int MaximumRepeatCatchUpDays = 90;

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

    /// <summary>
    /// <see cref="DeadlineNoticeLeadHours"/> inside its bounds. Both the sweep's enumeration horizon
    /// and the notice service's own horizon are computed from this, so the two cannot disagree about
    /// which deadlines are due.
    /// </summary>
    public int EffectiveDeadlineNoticeLeadHours => Math.Clamp(
        DeadlineNoticeLeadHours, MinimumDeadlineNoticeLeadHours, MaximumDeadlineNoticeLeadHours);

    /// <summary><see cref="SweepIntervalMinutes"/> inside its bounds, as the timer wants it.</summary>
    public TimeSpan EffectiveSweepInterval => TimeSpan.FromMinutes(
        Math.Clamp(SweepIntervalMinutes, MinimumSweepIntervalMinutes, MaximumSweepIntervalMinutes));

    /// <summary><see cref="RepeatSweepIntervalMinutes"/> inside its bounds, as the timer wants it.</summary>
    public TimeSpan EffectiveRepeatSweepInterval => TimeSpan.FromMinutes(
        Math.Clamp(RepeatSweepIntervalMinutes, MinimumSweepIntervalMinutes, MaximumSweepIntervalMinutes));

    /// <summary>
    /// <see cref="RepeatCatchUpDays"/> inside its bounds. The repeat sweep's enumeration bound and the
    /// per-organization late-window test are both computed from this, for the reason the class remarks
    /// give.
    /// </summary>
    public int EffectiveRepeatCatchUpDays => Math.Clamp(
        RepeatCatchUpDays, MinimumRepeatCatchUpDays, MaximumRepeatCatchUpDays);
}
