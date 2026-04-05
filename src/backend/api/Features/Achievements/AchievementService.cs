using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Achievements;

public class AchievementService(AppDbContext databaseContext)
{
    public async Task<IReadOnlyList<AchievementDto>> GetAchievementsForUserAsync(Guid userId)
    {
        var allAchievements = await databaseContext.Achievements
            .OrderBy(achievement => achievement.SortOrder)
            .ToListAsync();

        var unlockedByAchievementId = await databaseContext.UserAchievements
            .Where(userAchievement => userAchievement.UserId == userId)
            .ToDictionaryAsync(userAchievement => userAchievement.AchievementId);

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

    /// <summary>
    /// Evaluates all achievement conditions for the user after an exercise submission.
    /// Returns a list of newly unlocked achievement keys (for toast notifications).
    /// </summary>
    public async Task<IReadOnlyList<string>> EvaluateAchievementsAfterSubmitAsync(Guid userId)
    {
        var allAchievements = await databaseContext.Achievements.ToListAsync();

        var alreadyUnlockedIds = await databaseContext.UserAchievements
            .Where(userAchievement => userAchievement.UserId == userId)
            .Select(userAchievement => userAchievement.AchievementId)
            .ToHashSetAsync();

        var lockedAchievements = allAchievements
            .Where(achievement => !alreadyUnlockedIds.Contains(achievement.Id))
            .ToList();

        if (lockedAchievements.Count == 0) return Array.Empty<string>();

        var completedLessonCount = await databaseContext.UserLessonProgressRecords
            .CountAsync(progress => progress.UserId == userId && progress.Status == "completed");

        var totalXpAmount = await databaseContext.UserXpRecords
            .Where(xp => xp.UserId == userId)
            .SumAsync(xp => (int?)xp.Amount) ?? 0;

        var currentStreakDayCount = await databaseContext.UserStreaks
            .Where(streak => streak.UserId == userId)
            .Select(streak => (int?)streak.CurrentStreakDayCount)
            .FirstOrDefaultAsync() ?? 0;

        var hasCompletedAnySkill = await databaseContext.UserSkillProgressRecords
            .AnyAsync(progress => progress.UserId == userId && progress.Status == "completed");

        var newlyUnlockedKeys = new List<string>();
        var now = DateTime.UtcNow;

        foreach (var achievement in lockedAchievements)
        {
            var shouldUnlock = achievement.ConditionType switch
            {
                "first_lesson"    => completedLessonCount >= 1,
                "lesson_count"    => completedLessonCount >= achievement.ConditionThreshold,
                "xp_total"        => totalXpAmount >= achievement.ConditionThreshold,
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
                UnlockedAt = now
            });
            newlyUnlockedKeys.Add(achievement.Key);
        }

        return newlyUnlockedKeys;
    }
}
