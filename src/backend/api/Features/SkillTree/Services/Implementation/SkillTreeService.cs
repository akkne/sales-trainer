using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.SkillTree.Models;
using SalesTrainer.Api.Features.SkillTree.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.SkillTree.Services.Implementation;

internal sealed class SkillTreeService(AppDbContext databaseContext) : ISkillTreeService
{
    public async Task<IReadOnlyList<SkillTreeNodeDto>> GetAllSkillsAsync(
        CancellationToken cancellationToken = default)
    {
        var allSkills = await databaseContext.Skills
            .OrderBy(skill => skill.OrderInTree)
            .ThenBy(skill => skill.Id)
            .ToListAsync(cancellationToken);

        return allSkills.Select(skill => new SkillTreeNodeDto(
            skill.Id,
            skill.IconicName,
            skill.Title,
            "school", // default icon
            skill.OrderInTree,
            "available",
            0,
            0,
            false,
            skill.Stage))
            .ToList();
    }

    public async Task<IReadOnlyList<SkillTreeNodeDto>> GetAllSkillsWithProgressAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var allSkills = await databaseContext.Skills
            .OrderBy(skill => skill.OrderInTree)
            .ThenBy(skill => skill.Id)
            .ToListAsync(cancellationToken);

        // Get all topics with their lessons count
        var topicsBySkill = await databaseContext.Topics
            .GroupBy(t => t.SkillId)
            .Select(g => new { SkillId = g.Key, TopicIds = g.Select(t => t.Id).ToList() })
            .ToDictionaryAsync(x => x.SkillId, x => x.TopicIds, cancellationToken);

        // Get lesson counts per skill (through topics)
        var lessonCountBySkill = new Dictionary<Guid, int>();
        var completedLessonCountBySkill = new Dictionary<Guid, int>();

        foreach (var skill in allSkills)
        {
            if (!topicsBySkill.TryGetValue(skill.Id, out var topicIds))
            {
                lessonCountBySkill[skill.Id] = 0;
                completedLessonCountBySkill[skill.Id] = 0;
                continue;
            }

            var totalLessons = await databaseContext.Lessons
                .Where(l => topicIds.Contains(l.TopicId))
                .CountAsync(cancellationToken);

            var completedLessons = await databaseContext.UserLessonProgressRecords
                .Where(p => p.UserId == userId && p.Status == "completed")
                .Join(databaseContext.Lessons,
                    progress => progress.LessonId,
                    lesson => lesson.Id,
                    (progress, lesson) => lesson)
                .Where(lesson => topicIds.Contains(lesson.TopicId))
                .CountAsync(cancellationToken);

            lessonCountBySkill[skill.Id] = totalLessons;
            completedLessonCountBySkill[skill.Id] = completedLessons;
        }

        return allSkills.Select(skill =>
        {
            var totalLessons = lessonCountBySkill.GetValueOrDefault(skill.Id, 0);
            var completedLessons = completedLessonCountBySkill.GetValueOrDefault(skill.Id, 0);

            var status = completedLessons == 0 ? "available" :
                         completedLessons >= totalLessons && totalLessons > 0 ? "completed" :
                         "in_progress";

            return new SkillTreeNodeDto(
                skill.Id,
                skill.IconicName,
                skill.Title,
                "school",
                skill.OrderInTree,
                status,
                completedLessons,
                totalLessons,
                false,
                skill.Stage);
        }).ToList();
    }

    public async Task<IReadOnlyList<TopicDto>> GetTopicsForSkillAsync(
        Guid skillId,
        CancellationToken cancellationToken = default)
    {
        var topics = await databaseContext.Topics
            .Where(topic => topic.SkillId == skillId)
            .OrderBy(topic => topic.OrderInSkill)
            .ToListAsync(cancellationToken);

        return topics.Select(topic => new TopicDto(
            topic.Id,
            topic.SkillId,
            topic.Title,
            topic.OrderInSkill))
            .ToList();
    }

    public async Task<SkillTreeResponseDto> GetSkillTreeForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var allSkills = await GetAllSkillsWithProgressAsync(userId, cancellationToken);

        var currentStreakDayCount = await databaseContext.UserStreaks
            .Where(streak => streak.UserId == userId)
            .Select(streak => streak.CurrentStreakDayCount)
            .FirstOrDefaultAsync(cancellationToken);

        var totalExperiencePointsAmount = await databaseContext.UserXpRecords
            .Where(experiencePointRecord => experiencePointRecord.UserId == userId)
            .SumAsync(experiencePointRecord => (int?)experiencePointRecord.Amount, cancellationToken) ?? 0;

        var weekStart = DateOnly.FromDateTime(
            DateTime.UtcNow.AddDays(-(int)DateTime.UtcNow.DayOfWeek));

        var weeklyExperiencePointsAmount = await databaseContext.UserXpRecords
            .Where(experiencePointRecord => experiencePointRecord.UserId == userId &&
                         DateOnly.FromDateTime(experiencePointRecord.EarnedAt) >= weekStart)
            .SumAsync(experiencePointRecord => (int?)experiencePointRecord.Amount, cancellationToken) ?? 0;

        return new SkillTreeResponseDto(
            allSkills,
            currentStreakDayCount,
            totalExperiencePointsAmount,
            weeklyExperiencePointsAmount);
    }
}
