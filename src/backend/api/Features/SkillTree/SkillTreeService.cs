using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.SkillTree;

public class SkillTreeService(AppDbContext databaseContext)
{
    /// <summary>
    /// Returns ALL skills in the system with the user's progress status.
    /// Skills without a UserSkillProgress row are returned as "locked".
    /// Used by the profile skill picker.
    /// </summary>
    public async Task<IReadOnlyList<SkillTreeNodeDto>> GetAllSkillsForUserAsync(Guid userId)
    {
        var skillProgressBySkillId = await databaseContext.UserSkillProgressRecords
            .Where(p => p.UserId == userId)
            .ToDictionaryAsync(p => p.SkillId);

        var allSkills = await databaseContext.Skills
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Id)
            .ToListAsync();

        // Lesson counts per skill
        var lessonCountsBySkillId = await databaseContext.Lessons
            .GroupBy(l => l.SkillId)
            .Select(g => new { SkillId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SkillId, x => x.Count);

        return allSkills.Select(skill =>
        {
            skillProgressBySkillId.TryGetValue(skill.Id, out var progress);
            lessonCountsBySkillId.TryGetValue(skill.Id, out var lessonCount);
            var status = progress?.Status ?? "locked";
            return new SkillTreeNodeDto(
                skill.Id,
                skill.Slug,
                skill.Title,
                skill.IconName,
                skill.SortOrder,
                status,
                progress?.CompletedLessonCount ?? 0,
                progress?.TotalLessonCount ?? lessonCount,
                status == "locked");
        }).ToList();
    }

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
