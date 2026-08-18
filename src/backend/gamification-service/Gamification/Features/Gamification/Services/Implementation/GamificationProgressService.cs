using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Common.Extensions;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.Gamification.Services.Implementation;

/// <summary>
/// Assembles the read model behind the progress endpoint: streak counts, lifetime and windowed
/// experience-point totals, and the goals they are measured against.
///
/// <para>
/// Read-only — every total is summed from the append-only ledger rather than cached, so a corrected
/// grant is reflected immediately and no counter can drift out of agreement with the rows behind it.
/// The day and week windows are UTC and Monday-based, matching the league period.
/// </para>
/// </summary>
internal sealed class GamificationProgressService(
    GamificationDbContext databaseContext,
    IGamificationSettingsService settingsService) : IGamificationProgressService
{
    public async Task<GamificationProgressDto> GetProgressForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);
        var settings = await settingsService.GetSettingsAsync(cancellationToken);

        var streak = await databaseContext.UserStreaks
            .AsNoTracking()
            .FirstOrDefaultAsync(record => record.UserId == userId, cancellationToken);

        var totalExperiencePointsAmount = await databaseContext.UserExperiencePointsRecords
            .Where(record => record.UserId == userId)
            .SumAsync(record => (int?)record.Amount, cancellationToken) ?? 0;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = today.StartOfWeek();

        var dailyExperiencePointsAmount = await databaseContext.UserExperiencePointsRecords
            .Where(record => record.UserId == userId && DateOnly.FromDateTime(record.EarnedAt) == today)
            .SumAsync(record => (int?)record.Amount, cancellationToken) ?? 0;

        var weeklyExperiencePointsAmount = await databaseContext.UserExperiencePointsRecords
            .Where(record => record.UserId == userId && DateOnly.FromDateTime(record.EarnedAt) >= weekStart)
            .SumAsync(record => (int?)record.Amount, cancellationToken) ?? 0;

        return new GamificationProgressDto(
            streak?.CurrentStreakDayCount ?? 0,
            streak?.LongestStreakDayCount ?? 0,
            totalExperiencePointsAmount,
            dailyExperiencePointsAmount,
            weeklyExperiencePointsAmount,
            settings.DailyXpGoal,
            settings.WeeklyXpGoal);
    }
}
