using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.Gamification.Services.Implementation;

internal sealed class GamificationSettingsService(GamificationDbContext databaseContext) : IGamificationSettingsService
{
    private const int DefaultExerciseBaseExperiencePoints = 10;

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

    public async Task<int> GetExerciseBaseExperiencePointsAsync(string exerciseType, CancellationToken cancellationToken = default)
    {
        var reward = await databaseContext.ExerciseTypeRewards
            .AsNoTracking()
            .FirstOrDefaultAsync(record => record.ExerciseType == exerciseType, cancellationToken);

        return reward?.BaseXpReward ?? DefaultExerciseBaseExperiencePoints;
    }

    public async Task<int> GetStreakBonusExperiencePointsAsync(int streakDayCount, CancellationToken cancellationToken = default)
    {
        var milestones = await databaseContext.StreakMilestones
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (milestones.Count > 0)
        {
            return milestones.FirstOrDefault(milestone => milestone.DayCount == streakDayCount)?.XpReward ?? 0;
        }

        return DefaultStreakMilestones.GetValueOrDefault(streakDayCount, 0);
    }
}
