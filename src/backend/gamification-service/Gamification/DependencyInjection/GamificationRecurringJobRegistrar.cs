using Hangfire;
using Microsoft.Extensions.Options;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Features.Gamification;
using Sellevate.Gamification.Features.League;
using Sellevate.Gamification.Infrastructure.Configuration;

namespace Sellevate.Gamification.DependencyInjection;

/// <summary>
/// Installs the service's two Hangfire cron entries.
///
/// <para>
/// Uses the DI-resolved <see cref="IRecurringJobManager"/>, <b>not</b> the static
/// <c>RecurringJob</c> facade: the static API reads <c>JobStorage.Current</c>, which is only set once
/// the Hangfire server has started — calling it before <c>application.Run()</c> throws
/// "Current JobStorage instance has not been initialized yet".
/// </para>
///
/// <para>
/// <c>AddOrUpdate</c> is idempotent per identifier, so a restart retimes an existing schedule instead
/// of adding a second one. That only holds while the identifiers stay fixed, which is why they are
/// constants and only the cron expressions are configurable.
/// </para>
/// </summary>
internal static class GamificationRecurringJobRegistrar
{
    public static void Register(WebApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        using var recurringJobScope = application.Services.CreateScope();

        var recurringJobManager = recurringJobScope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        var schedules = recurringJobScope.ServiceProvider
            .GetRequiredService<IOptions<RecurringJobConfiguration>>().Value;

        recurringJobManager.AddOrUpdate<WeeklyLeagueClosureJob>(
            HangfireJobIdentifiers.WeeklyLeagueClosure,
            weeklyLeagueClosureJob => weeklyLeagueClosureJob.ExecuteAsync(),
            schedules.WeeklyLeagueClosureCron);

        recurringJobManager.AddOrUpdate<StreakResetJob>(
            HangfireJobIdentifiers.DailyStreakReset,
            streakResetJob => streakResetJob.ExecuteAsync(),
            schedules.DailyStreakResetCron);
    }
}
