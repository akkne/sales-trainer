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
            skill.Title,
            skill.Description,
            skill.OrderInTree))
            .ToList();
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
        var allSkills = await GetAllSkillsAsync(cancellationToken);

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
