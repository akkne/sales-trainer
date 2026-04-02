using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.SkillTree;

public class SkillTreeService(AppDbContext databaseContext)
{
    public async Task<SkillTreeResponseDto> GetSkillTreeForUserAsync(Guid userId)
    {
        var skillProgressRecords = await databaseContext.UserSkillProgressRecords
            .Where(progress => progress.UserId == userId)
            .Join(
                databaseContext.Skills,
                progress => progress.SkillId,
                skill => skill.Id,
                (progress, skill) => new { progress, skill })
            .OrderBy(x => x.skill.SortOrder)
            .Select(x => new SkillTreeNodeDto(
                x.skill.Id,
                x.skill.Slug,
                x.skill.Title,
                x.skill.IconName,
                x.skill.SortOrder,
                x.progress.Status,
                x.progress.CompletedLessonCount,
                x.progress.TotalLessonCount,
                x.progress.Status == "locked"))
            .ToListAsync();

        var currentStreakDayCount = await databaseContext.UserStreaks
            .Where(streak => streak.UserId == userId)
            .Select(streak => streak.CurrentStreakDayCount)
            .FirstOrDefaultAsync();

        var totalXpAmount = await databaseContext.UserXpRecords
            .Where(xp => xp.UserId == userId)
            .SumAsync(xp => (int?)xp.Amount) ?? 0;

        var weekStart = DateOnly.FromDateTime(
            DateTime.UtcNow.AddDays(-(int)DateTime.UtcNow.DayOfWeek));

        var weeklyXpAmount = await databaseContext.UserXpRecords
            .Where(xp => xp.UserId == userId &&
                         DateOnly.FromDateTime(xp.EarnedAt) >= weekStart)
            .SumAsync(xp => (int?)xp.Amount) ?? 0;

        return new SkillTreeResponseDto(
            skillProgressRecords,
            currentStreakDayCount,
            totalXpAmount,
            weeklyXpAmount);
    }
}
