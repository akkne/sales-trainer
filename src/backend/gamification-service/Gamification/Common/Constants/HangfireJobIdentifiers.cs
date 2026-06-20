namespace Sellevate.Gamification.Common.Constants;

public static class HangfireJobIdentifiers
{
    public const string DailyStreakReset = "daily-streak-reset";
    public const string DailyStreakResetCron = "5 0 * * *";

    public const string WeeklyLeagueClosure = "weekly-league-closure";
    public const string WeeklyLeagueClosureCron = "*/15 * * * *";
}
