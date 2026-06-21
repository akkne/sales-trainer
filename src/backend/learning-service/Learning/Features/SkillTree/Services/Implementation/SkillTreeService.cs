using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Features.SkillTree.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.SkillTree.Services.Implementation;

internal sealed class SkillTreeService(LearningDbContext databaseContext) : ISkillTreeService
{
    public async Task<IReadOnlyList<SkillStageDto>> GetStagesAsync(
        CancellationToken cancellationToken = default)
    {
        return await databaseContext.SkillStages
            .OrderBy(stage => stage.Order)
            .Select(stage => new SkillStageDto(stage.Key, stage.Label, stage.Accent, stage.Order))
            .ToListAsync(cancellationToken);
    }

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
            "school",
            skill.OrderInTree,
            LessonProgressStatuses.Available,
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

        // Single query: lesson count per skill via topic join.
        var lessonCountBySkill = await databaseContext.Topics
            .Join(databaseContext.Lessons,
                topic => topic.Id,
                lesson => lesson.TopicId,
                (topic, lesson) => new { topic.SkillId, lesson.Id })
            .GroupBy(entry => entry.SkillId)
            .Select(group => new { SkillId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.SkillId, entry => entry.Count, cancellationToken);

        // Single query: completed lesson count per skill for this user.
        var completedLessonCountBySkill = await databaseContext.UserLessonProgressRecords
            .Where(progress => progress.UserId == userId && progress.Status == LessonProgressStatuses.Completed)
            .Join(databaseContext.Lessons,
                progress => progress.LessonId,
                lesson => lesson.Id,
                (progress, lesson) => lesson)
            .Join(databaseContext.Topics,
                lesson => lesson.TopicId,
                topic => topic.Id,
                (lesson, topic) => new { topic.SkillId })
            .GroupBy(entry => entry.SkillId)
            .Select(group => new { SkillId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.SkillId, entry => entry.Count, cancellationToken);

        return allSkills.Select(skill =>
        {
            var totalLessons = lessonCountBySkill.GetValueOrDefault(skill.Id, 0);
            var completedLessons = completedLessonCountBySkill.GetValueOrDefault(skill.Id, 0);

            var status = completedLessons == 0 ? LessonProgressStatuses.Available :
                         completedLessons >= totalLessons && totalLessons > 0 ? LessonProgressStatuses.Completed :
                         LessonProgressStatuses.InProgress;

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

        return new SkillTreeResponseDto(
            allSkills,
            CurrentStreakDayCount: 0,
            TotalXpAmount: 0,
            WeeklyXpAmount: 0,
            DailyXpAmount: 0,
            DailyXpGoal: 0,
            WeeklyXpGoal: 0);
    }
}
