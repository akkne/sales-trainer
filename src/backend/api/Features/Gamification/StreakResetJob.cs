using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Gamification;

public sealed class StreakResetJob(AppDbContext database, ILogger<StreakResetJob> logger)
{
    public async Task ExecuteAsync()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        var staleStreaks = await database.UserStreaks
            .Where(s => s.CurrentStreakDayCount > 0
                        && (s.LastActivityDate == null || s.LastActivityDate < yesterday))
            .ToListAsync();

        if (staleStreaks.Count == 0)
        {
            logger.LogInformation("StreakResetJob: no stale streaks found.");
            return;
        }

        foreach (var streak in staleStreaks)
            streak.CurrentStreakDayCount = 0;

        await database.SaveChangesAsync();

        logger.LogInformation("StreakResetJob: reset {Count} stale streaks.", staleStreaks.Count);
    }
}
