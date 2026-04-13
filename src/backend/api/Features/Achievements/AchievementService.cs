using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Achievements;

internal sealed class AchievementService(AppDbContext databaseContext) : IAchievementService
{
    public async Task<IReadOnlyList<AchievementDto>> GetAchievementsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var allAchievements = await databaseContext.Achievements
            .OrderBy(achievement => achievement.SortOrder)
            .ToListAsync(cancellationToken);

        var unlockedByAchievementId = await databaseContext.UserAchievements
            .Where(userAchievement => userAchievement.UserId == userId)
            .ToDictionaryAsync(userAchievement => userAchievement.AchievementId, cancellationToken);

        return allAchievements.Select(achievement =>
        {
            unlockedByAchievementId.TryGetValue(achievement.Id, out var userAchievement);
            return new AchievementDto(
                achievement.Id,
                achievement.Key,
                achievement.Title,
                achievement.Description,
                achievement.IconEmoji,
                userAchievement is not null,
                userAchievement?.UnlockedAt);
        }).ToList();
    }

    public async Task<IReadOnlyList<string>> EvaluateAchievementsAfterSubmitAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var allAchievements = await databaseContext.Achievements.ToListAsync(cancellationToken);

        var alreadyUnlockedIds = await databaseContext.UserAchievements
            .Where(userAchievement => userAchievement.UserId == userId)
            .Select(userAchievement => userAchievement.AchievementId)
            .ToHashSetAsync(cancellationToken);

        var lockedAchievements = allAchievements
            .Where(achievement => !alreadyUnlockedIds.Contains(achievement.Id))
            .ToList();

        if (lockedAchievements.Count == 0) return Array.Empty<string>();

        var completedLessonCount = await databaseContext.UserLessonProgressRecords
            .CountAsync(progressRecord => progressRecord.UserId == userId && progressRecord.Status == "completed", cancellationToken);

        var totalExperiencePointsAmount = await databaseContext.UserXpRecords
            .Where(experiencePointRecord => experiencePointRecord.UserId == userId)
            .SumAsync(experiencePointRecord => (int?)experiencePointRecord.Amount, cancellationToken) ?? 0;

        var currentStreakDayCount = await databaseContext.UserStreaks
            .Where(streak => streak.UserId == userId)
            .Select(streak => (int?)streak.CurrentStreakDayCount)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        var hasCompletedAnySkill = await databaseContext.UserSkillProgressRecords
            .AnyAsync(progressRecord => progressRecord.UserId == userId && progressRecord.Status == "completed", cancellationToken);

        var newlyUnlockedKeys = new List<string>();
        var currentTimestamp = DateTime.UtcNow;

        foreach (var achievement in lockedAchievements)
        {
            var shouldUnlock = achievement.ConditionType switch
            {
                "first_lesson"    => completedLessonCount >= 1,
                "lesson_count"    => completedLessonCount >= achievement.ConditionThreshold,
                "xp_total"        => totalExperiencePointsAmount >= achievement.ConditionThreshold,
                "streak_days"     => currentStreakDayCount >= achievement.ConditionThreshold,
                "skill_completed" => hasCompletedAnySkill,
                _                 => false
            };

            if (!shouldUnlock) continue;

            databaseContext.UserAchievements.Add(new UserAchievement
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AchievementId = achievement.Id,
                UnlockedAt = currentTimestamp
            });
            newlyUnlockedKeys.Add(achievement.Key);
        }

        return newlyUnlockedKeys;
    }
}
