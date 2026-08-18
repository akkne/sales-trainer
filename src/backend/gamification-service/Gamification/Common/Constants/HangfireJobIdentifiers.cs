namespace Sellevate.Gamification.Common.Constants;

/// <summary>
/// The recurring-job identifiers Hangfire stores a schedule under. They are a persisted primary key,
/// not a label: renaming one does not retime a job, it orphans the old schedule and adds a second.
/// The schedules themselves are configuration — see <c>RecurringJobConfiguration</c>.
/// </summary>
public static class HangfireJobIdentifiers
{
    public const string DailyStreakReset = "daily-streak-reset";

    public const string WeeklyLeagueClosure = "weekly-league-closure";
}
