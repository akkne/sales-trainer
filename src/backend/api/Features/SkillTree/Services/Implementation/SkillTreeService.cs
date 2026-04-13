using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Onboarding.Services.Abstract;
using SalesTrainer.Api.Features.SkillTree.Models;
using SalesTrainer.Api.Features.SkillTree.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.SkillTree.Services.Implementation;

internal sealed class SkillTreeService(
    AppDbContext databaseContext,
    IOnboardingService onboardingService) : ISkillTreeService
{
    private const string AlwaysEnrolledSlug = "sales-basics";

    public async Task UpdateEnrolledSkillsAsync(
        Guid userId,
        List<string> skillSlugs,
        CancellationToken cancellationToken = default)
    {
        var desiredSkillSlugs = skillSlugs
            .Select(slug => slug.Trim())
            .Where(slug => slug.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        desiredSkillSlugs.Add(AlwaysEnrolledSlug);

        await onboardingService.EnrollSkillsAsync(userId, desiredSkillSlugs, cancellationToken);

        var allSkills = await databaseContext.Skills.ToListAsync(cancellationToken);
        var existingProgress = await databaseContext.UserSkillProgressRecords
            .Where(progressRecord => progressRecord.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var progressRecord in existingProgress)
        {
            if (progressRecord.Status == "locked") continue;

            var skill = allSkills.FirstOrDefault(skillRecord => skillRecord.Id == progressRecord.SkillId);
            if (skill is null) continue;
            if (skill.Slug.Equals(AlwaysEnrolledSlug, StringComparison.OrdinalIgnoreCase)) continue;
            if (desiredSkillSlugs.Contains(skill.Slug)) continue;

            progressRecord.Status = "locked";
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SkillTreeNodeDto>> GetAllSkillsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var skillProgressBySkillId = await databaseContext.UserSkillProgressRecords
            .Where(progressRecord => progressRecord.UserId == userId)
            .ToDictionaryAsync(progressRecord => progressRecord.SkillId, cancellationToken);

        var allSkills = await databaseContext.Skills
            .OrderBy(skill => skill.SortOrder)
            .ThenBy(skill => skill.Id)
            .ToListAsync(cancellationToken);

        var lessonCountsBySkillId = await databaseContext.Lessons
            .GroupBy(lesson => lesson.SkillId)
            .Select(group => new { SkillId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.SkillId, entry => entry.Count, cancellationToken);

        return allSkills.Select(skill =>
        {
            skillProgressBySkillId.TryGetValue(skill.Id, out var progressRecord);
            lessonCountsBySkillId.TryGetValue(skill.Id, out var lessonCount);
            var status = progressRecord?.Status ?? "locked";
            return new SkillTreeNodeDto(
                skill.Id,
                skill.Slug,
                skill.Title,
                skill.IconName,
                skill.SortOrder,
                status,
                progressRecord?.CompletedLessonCount ?? 0,
                progressRecord?.TotalLessonCount ?? lessonCount,
                status == "locked");
        }).ToList();
    }

    public async Task<SkillTreeResponseDto> GetSkillTreeForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var skillProgressRecords = await databaseContext.UserSkillProgressRecords
            .Where(progressRecord => progressRecord.UserId == userId)
            .Join(
                databaseContext.Skills,
                progressRecord => progressRecord.SkillId,
                skill => skill.Id,
                (progressRecord, skill) => new { progressRecord, skill })
            .OrderBy(pair => pair.skill.SortOrder)
            .Select(pair => new SkillTreeNodeDto(
                pair.skill.Id,
                pair.skill.Slug,
                pair.skill.Title,
                pair.skill.IconName,
                pair.skill.SortOrder,
                pair.progressRecord.Status,
                pair.progressRecord.CompletedLessonCount,
                pair.progressRecord.TotalLessonCount,
                pair.progressRecord.Status == "locked"))
            .ToListAsync(cancellationToken);

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
            skillProgressRecords,
            currentStreakDayCount,
            totalExperiencePointsAmount,
            weeklyExperiencePointsAmount);
    }
}
