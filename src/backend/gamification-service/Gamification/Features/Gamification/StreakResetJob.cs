using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.Gamification;

public sealed class StreakResetJob(
    GamificationDbContext databaseContext,
    IStreakClock streakClock,
    ILogger<StreakResetJob> logger)
{
    public async Task ExecuteAsync()
    {
        var yesterday = streakClock.Today().AddDays(-1);

        var staleStreaks = await databaseContext.UserStreaks
            .Where(streak => streak.CurrentStreakDayCount > 0
                        && (streak.LastActivityDate == null || streak.LastActivityDate < yesterday))
            .ToListAsync();

        if (staleStreaks.Count == 0)
        {
            logger.LogInformation("StreakResetJob: no stale streaks found.");
            return;
        }

        foreach (var streak in staleStreaks)
        {
            streak.CurrentStreakDayCount = 0;
        }

        await databaseContext.SaveChangesAsync();

        logger.LogInformation("StreakResetJob: reset {Count} stale streaks.", staleStreaks.Count);
    }
}
