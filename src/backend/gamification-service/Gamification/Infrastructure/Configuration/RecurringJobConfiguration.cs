namespace Sellevate.Gamification.Infrastructure.Configuration;

/// <summary>
/// Cron expressions for the two Hangfire recurring jobs, so an operator can retime them without a
/// rebuild. The job <em>identifiers</em> stay in <c>HangfireJobIdentifiers</c>: they are the primary
/// key Hangfire stores a schedule under, and renaming one at runtime would orphan the old entry
/// rather than move it.
///
/// <para>
/// The defaults here are the schedules the service has always run and must stay byte-identical to
/// the values in <c>appsettings.json</c>. Both jobs are idempotent — the streak reset re-derives
/// staleness from the clock, and the league rollover re-checks the period end inside a transaction —
/// so a tightened schedule costs database work but cannot double-apply anything.
/// </para>
/// </summary>
public sealed class RecurringJobConfiguration
{
    public const string SectionName = "Gamification:RecurringJobs";

    /// <summary>Daily, five minutes after midnight UTC.</summary>
    public string DailyStreakResetCron { get; init; } = DefaultDailyStreakResetCron;

    /// <summary>
    /// Every fifteen minutes. Not weekly despite the job's name: each organization now runs its own
    /// league week, so the job has to wake up often enough to catch whichever organization's period
    /// ended since the last tick.
    /// </summary>
    public string WeeklyLeagueClosureCron { get; init; } = DefaultWeeklyLeagueClosureCron;

    public const string DefaultDailyStreakResetCron = "5 0 * * *";

    public const string DefaultWeeklyLeagueClosureCron = "*/15 * * * *";
}
