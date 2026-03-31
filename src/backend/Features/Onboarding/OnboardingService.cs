using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.SkillTree;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Onboarding;

public class OnboardingService(AppDbContext databaseContext)
{
    public async Task CompleteOnboardingForUserAsync(
        Guid userId,
        string salesType,
        string experienceLevel,
        string goal)
    {
        var existingProfile = await databaseContext.UserProfiles
            .FirstOrDefaultAsync(profile => profile.UserId == userId);

        if (existingProfile is not null && existingProfile.IsOnboardingCompleted)
            return;

        if (existingProfile is null)
        {
            existingProfile = new UserProfile { Id = Guid.NewGuid(), UserId = userId };
            databaseContext.UserProfiles.Add(existingProfile);
        }

        existingProfile.SalesType = salesType;
        existingProfile.ExperienceLevel = experienceLevel;
        existingProfile.Goal = goal;
        existingProfile.IsOnboardingCompleted = true;

        await CreatePersonalizedSkillProgressRecordsAsync(userId, salesType);

        await databaseContext.SaveChangesAsync();
    }

    private async Task CreatePersonalizedSkillProgressRecordsAsync(
        Guid userId,
        string salesType)
    {
        var applicableSkillIds = await databaseContext.Skills
            .Where(skill =>
                skill.ApplicableSalesTypes.Length == 0 ||
                skill.ApplicableSalesTypes.Contains(salesType))
            .OrderBy(skill => skill.SortOrder)
            .Select(skill => skill.Id)
            .ToListAsync();

        var existingProgressSkillIds = await databaseContext.UserSkillProgressRecords
            .Where(progress => progress.UserId == userId)
            .Select(progress => progress.SkillId)
            .ToListAsync();

        var skillIdsWithoutProgress = applicableSkillIds
            .Where(skillId => !existingProgressSkillIds.Contains(skillId))
            .ToList();

        for (var skillIndex = 0; skillIndex < skillIdsWithoutProgress.Count; skillIndex++)
        {
            var skillId = skillIdsWithoutProgress[skillIndex];
            var lessonCount = await databaseContext.Lessons
                .CountAsync(lesson => lesson.SkillId == skillId);

            var initialStatus = skillIndex == 0 ? "available" : "locked";

            databaseContext.UserSkillProgressRecords.Add(new UserSkillProgress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SkillId = skillId,
                Status = initialStatus,
                CompletedLessonCount = 0,
                TotalLessonCount = lessonCount
            });
        }
    }
}
