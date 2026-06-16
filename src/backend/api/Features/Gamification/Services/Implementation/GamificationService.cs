using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Gamification.Models;
using SalesTrainer.Api.Features.Gamification.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Gamification.Services.Implementation;

internal sealed class GamificationService(AppDbContext databaseContext) : IGamificationService
{
    // Defaults used only when the corresponding rows are not present in the database
    // (e.g. in-memory unit tests that don't run migrations/seeds). The canonical values
    // live in the DB and are admin-editable.
    private const int DefaultExerciseBaseXp = 10;

    private static readonly IReadOnlyDictionary<int, int> DefaultStreakMilestones =
        new Dictionary<int, int> { [7] = 50, [30] = 200 };

    public async Task<GamificationSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await databaseContext.GamificationSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new GamificationSettings();
            databaseContext.GamificationSettings.Add(settings);
            await databaseContext.SaveChangesAsync(cancellationToken);
        }

        return settings;
    }

    public async Task<int> GetExerciseBaseXpAsync(string exerciseType, CancellationToken cancellationToken = default)
    {
        var reward = await databaseContext.ExerciseTypeRewards
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ExerciseType == exerciseType, cancellationToken);

        return reward?.BaseXpReward ?? DefaultExerciseBaseXp;
    }

    public async Task<int> GetStreakBonusXpAsync(int streakDayCount, CancellationToken cancellationToken = default)
    {
        var milestones = await databaseContext.StreakMilestones
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // When milestones are configured in the DB they are authoritative (an admin may
        // have intentionally removed one). Only when the table is empty do we fall back
        // to the historic hardcoded ladder so unseeded contexts keep working.
        if (milestones.Count > 0)
            return milestones.FirstOrDefault(m => m.DayCount == streakDayCount)?.XpReward ?? 0;

        return DefaultStreakMilestones.GetValueOrDefault(streakDayCount, 0);
    }
}
