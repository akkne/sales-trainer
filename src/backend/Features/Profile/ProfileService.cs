using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Profile;

public class ProfileService(AppDbContext databaseContext)
{
    public async Task<UserProfileStatsDto> GetProfileStatsForUserAsync(Guid userId)
    {
        var user = await databaseContext.Users
            .FirstOrDefaultAsync(user => user.Id == userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        var streakRecord = await databaseContext.UserStreaks
            .FirstOrDefaultAsync(streak => streak.UserId == userId);

        var totalXpAmount = await databaseContext.UserXpRecords
            .Where(xp => xp.UserId == userId)
            .SumAsync(xp => (int?)xp.Amount) ?? 0;

        var completedSkillCount = await databaseContext.UserSkillProgressRecords
            .CountAsync(progress => progress.UserId == userId && progress.Status == "completed");

        var totalSkillCount = await databaseContext.UserSkillProgressRecords
            .CountAsync(progress => progress.UserId == userId);

        var averageExerciseScore = await databaseContext.UserExerciseAttempts
            .Where(attempt => attempt.UserId == userId)
            .AverageAsync(attempt => (double?)attempt.Score) ?? 0.0;

        return new UserProfileStatsDto(
            user.DisplayName,
            user.Email,
            streakRecord?.CurrentStreakDayCount ?? 0,
            streakRecord?.LongestStreakDayCount ?? 0,
            totalXpAmount,
            completedSkillCount,
            totalSkillCount,
            Math.Round(averageExerciseScore, 1));
    }
}
