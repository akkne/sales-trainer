using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.Gamification.Services.Implementation;

internal sealed class GamificationProgressService(
    GamificationDbContext databaseContext,
    IGamificationSettingsService settingsService) : IGamificationProgressService
{
    public async Task<GamificationProgressDto> GetProgressForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);

        var streak = await databaseContext.UserStreaks
            .AsNoTracking()
            .FirstOrDefaultAsync(record => record.UserId == userId, cancellationToken);

        var totalExperiencePointsAmount = await databaseContext.UserExperiencePointsRecords
            .Where(record => record.UserId == userId)
            .SumAsync(record => (int?)record.Amount, cancellationToken) ?? 0;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = GetCurrentWeekStart(today);

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

    private static DateOnly GetCurrentWeekStart(DateOnly today)
    {
        var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-daysFromMonday);
    }
}
