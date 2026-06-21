using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Eventing;
using Sellevate.Gamification.Features.Achievements.Models;
using Sellevate.Gamification.Features.Achievements.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.Achievements.Services.Implementation;

internal sealed class AchievementService(
    GamificationDbContext databaseContext,
    IGamificationEventPublisher eventPublisher) : IAchievementService
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

    public async Task<IReadOnlyList<string>> EvaluateAchievementsAsync(
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

        if (lockedAchievements.Count == 0)
        {
            return Array.Empty<string>();
        }

        var learningProgress = await databaseContext.UserLearningProgressRecords
            .FirstOrDefaultAsync(record => record.UserId == userId, cancellationToken);

        var completedLessonCount = learningProgress?.CompletedLessonCount ?? 0;
        var hasCompletedAnySkill = learningProgress?.HasCompletedAnySkill ?? false;

        var totalExperiencePointsAmount = await databaseContext.UserExperiencePointsRecords
            .Where(record => record.UserId == userId)
            .SumAsync(record => (int?)record.Amount, cancellationToken) ?? 0;

        var currentStreakDayCount = await databaseContext.UserStreaks
            .Where(record => record.UserId == userId)
            .Select(record => (int?)record.CurrentStreakDayCount)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        var newlyUnlockedKeys = new List<string>();
        var newlyUnlockedAchievements = new List<Achievement>();
        var currentTimestamp = DateTime.UtcNow;

        foreach (var achievement in lockedAchievements)
        {
            var shouldUnlock = achievement.ConditionType switch
            {
                AchievementConditionTypes.FirstLesson => completedLessonCount >= 1,
                AchievementConditionTypes.LessonCount => completedLessonCount >= achievement.ConditionThreshold,
                AchievementConditionTypes.ExperiencePointsTotal => totalExperiencePointsAmount >= achievement.ConditionThreshold,
                AchievementConditionTypes.StreakDays => currentStreakDayCount >= achievement.ConditionThreshold,
                AchievementConditionTypes.SkillCompleted => hasCompletedAnySkill,
                _ => false
            };

            if (!shouldUnlock)
            {
                continue;
            }

            databaseContext.UserAchievements.Add(new UserAchievement
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AchievementId = achievement.Id,
                UnlockedAt = currentTimestamp,
            });
            newlyUnlockedKeys.Add(achievement.Key);
            newlyUnlockedAchievements.Add(achievement);
        }

        if (newlyUnlockedAchievements.Count == 0)
        {
            return Array.Empty<string>();
        }

        foreach (var unlockedAchievement in newlyUnlockedAchievements)
        {
            await eventPublisher.PublishAchievementUnlockedAsync(
                new AchievementUnlockedEvent(userId, unlockedAchievement.Key, unlockedAchievement.Title),
                cancellationToken);
        }

        await databaseContext.SaveChangesAsync(cancellationToken);

        return newlyUnlockedKeys;
    }
}
