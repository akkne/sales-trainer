using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Achievements.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Achievements;

public sealed class AchievementSeeder(AppDbContext databaseContext, ILogger<AchievementSeeder> logger)
{
    private static readonly IReadOnlyList<Achievement> DefaultAchievements =
    [
        new Achievement { Key = "first_lesson",    Title = "Первый шаг",      Description = "Пройди свой первый урок",               IconEmoji = "🎯", ConditionType = "first_lesson",    ConditionThreshold = 0,    SortOrder = 1  },
        new Achievement { Key = "lessons_5",       Title = "На старте",       Description = "Пройди 5 уроков",                       IconEmoji = "🚀", ConditionType = "lesson_count",    ConditionThreshold = 5,    SortOrder = 2  },
        new Achievement { Key = "lessons_20",      Title = "Практик",         Description = "Пройди 20 уроков",                      IconEmoji = "💪", ConditionType = "lesson_count",    ConditionThreshold = 20,   SortOrder = 3  },
        new Achievement { Key = "lessons_50",      Title = "Ветеран продаж",  Description = "Пройди 50 уроков",                      IconEmoji = "🏅", ConditionType = "lesson_count",    ConditionThreshold = 50,   SortOrder = 4  },
        new Achievement { Key = "xp_100",          Title = "Первые 100 XP",   Description = "Набери 100 XP",                         IconEmoji = "⚡", ConditionType = "xp_total",        ConditionThreshold = 100,  SortOrder = 5  },
        new Achievement { Key = "xp_500",          Title = "500 XP",          Description = "Набери 500 XP",                         IconEmoji = "🔥", ConditionType = "xp_total",        ConditionThreshold = 500,  SortOrder = 6  },
        new Achievement { Key = "xp_1000",         Title = "1000 XP",         Description = "Набери 1000 XP",                        IconEmoji = "💎", ConditionType = "xp_total",        ConditionThreshold = 1000, SortOrder = 7  },
        new Achievement { Key = "streak_7",        Title = "Неделя огня",     Description = "Занимайся 7 дней подряд",               IconEmoji = "🗓️", ConditionType = "streak_days",     ConditionThreshold = 7,    SortOrder = 8  },
        new Achievement { Key = "streak_30",       Title = "Месяц без пропусков", Description = "Занимайся 30 дней подряд",          IconEmoji = "🏆", ConditionType = "streak_days",     ConditionThreshold = 30,   SortOrder = 9  },
        new Achievement { Key = "skill_completed", Title = "Мастер навыка",   Description = "Полностью пройди один навык",           IconEmoji = "🎓", ConditionType = "skill_completed", ConditionThreshold = 0,    SortOrder = 10 }
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existingKeys = await databaseContext.Achievements
            .Select(achievement => achievement.Key)
            .ToHashSetAsync(cancellationToken);

        var newAchievements = DefaultAchievements
            .Where(achievement => !existingKeys.Contains(achievement.Key))
            .Select(achievement => new Achievement
            {
                Id = Guid.NewGuid(),
                Key = achievement.Key,
                Title = achievement.Title,
                Description = achievement.Description,
                IconEmoji = achievement.IconEmoji,
                ConditionType = achievement.ConditionType,
                ConditionThreshold = achievement.ConditionThreshold,
                SortOrder = achievement.SortOrder
            })
            .ToList();

        if (newAchievements.Count == 0)
        {
            logger.LogInformation("Achievement seed: all {Count} achievements already present", existingKeys.Count);
            return;
        }

        databaseContext.Achievements.AddRange(newAchievements);
        await databaseContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Achievement seed: inserted {Count} new achievements", newAchievements.Count);
    }
}
