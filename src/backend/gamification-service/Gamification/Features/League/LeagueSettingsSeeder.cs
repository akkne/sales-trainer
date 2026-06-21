using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Features.League.Models;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.League;

/// <summary>
/// GA6(a): Seeds the singleton LeagueSettings row and GamificationSettings row at startup
/// so that read-path getters never need to write. Idempotent — no-ops if the row already
/// exists. Also seeds the GamificationSettings singleton.
/// </summary>
public sealed class LeagueSettingsSeeder(GamificationDbContext databaseContext)
{
    private const int DefaultPeriodLengthDays = 7;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedLeagueSettingsAsync(cancellationToken);
        await SeedGamificationSettingsAsync(cancellationToken);
    }

    private async Task SeedLeagueSettingsAsync(CancellationToken cancellationToken)
    {
        var exists = await databaseContext.LeagueSettings.AnyAsync(cancellationToken);
        if (exists)
        {
            return;
        }

        var settings = new LeagueSettings();
        if (settings.CurrentPeriodStartDate is null || settings.CurrentPeriodEndsAt is null)
        {
            var start = GetCurrentWeekStart();
            var end = start.AddDays(DefaultPeriodLengthDays - 1);
            settings.CurrentPeriodStartDate = start;
            settings.CurrentPeriodEndsAt = EndOfDay(end);
        }

        databaseContext.LeagueSettings.Add(settings);

        try
        {
            await databaseContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another startup instance seeded first — that is fine.
        }
    }

    private async Task SeedGamificationSettingsAsync(CancellationToken cancellationToken)
    {
        var exists = await databaseContext.GamificationSettings.AnyAsync(cancellationToken);
        if (exists)
        {
            return;
        }

        databaseContext.GamificationSettings.Add(new Features.Gamification.Models.GamificationSettings());

        try
        {
            await databaseContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another startup instance seeded first — that is fine.
        }
    }

    private static DateTimeOffset EndOfDay(DateOnly date) =>
        new(date.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc));

    private static DateOnly GetCurrentWeekStart()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-daysFromMonday);
    }
}
