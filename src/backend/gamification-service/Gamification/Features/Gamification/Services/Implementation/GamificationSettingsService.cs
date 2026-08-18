using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.Gamification.Services.Implementation;

/// <summary>
/// Resolves gamification tuning from the installation-wide configuration tables, falling back to
/// built-in defaults where an administrator has configured nothing.
///
/// <para>
/// <see cref="GetSettingsAsync"/> is the one getter here that may write: it seeds the singleton
/// settings row if it is missing, so every later read is a plain read. The startup seeder normally
/// gets there first; this covers a database that predates it.
/// </para>
///
/// <para>
/// The streak-milestone fallback applies only while the <c>StreakMilestones</c> table is <em>empty</em>.
/// Once an administrator has defined any milestone, the table is authoritative and a day count absent
/// from it earns nothing — otherwise deleting the built-in seven-day milestone would silently restore
/// it from code.
/// </para>
/// </summary>
internal sealed class GamificationSettingsService(GamificationDbContext databaseContext) : IGamificationSettingsService
{
    private const int DefaultExerciseBaseExperiencePoints = 10;

    private static readonly IReadOnlyDictionary<int, int> DefaultStreakMilestones =
        new Dictionary<int, int> { [7] = 50, [30] = 200 };

    public async Task<GamificationSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);
        var settings = await databaseContext.GamificationSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new GamificationSettings();
            databaseContext.GamificationSettings.Add(settings);
            await databaseContext.SaveChangesAsync(cancellationToken);
            await tenantScope.CommitAsync(cancellationToken);
        }

        return settings;
    }

    public async Task<int> GetExerciseBaseExperiencePointsAsync(string exerciseType, CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);
        var reward = await databaseContext.ExerciseTypeRewards
            .AsNoTracking()
            .FirstOrDefaultAsync(record => record.ExerciseType == exerciseType, cancellationToken);

        return reward?.BaseXpReward ?? DefaultExerciseBaseExperiencePoints;
    }

    public async Task<int> GetStreakBonusExperiencePointsAsync(int streakDayCount, CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);
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
