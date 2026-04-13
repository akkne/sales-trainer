using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Onboarding.Models;
using SalesTrainer.Api.Features.Onboarding.Services.Abstract;
using SalesTrainer.Api.Features.SkillTree.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Onboarding.Services.Implementation;

internal sealed class OnboardingService(AppDbContext databaseContext) : IOnboardingService
{
    private const string DefaultSkillSlug = "sales-basics";

    public async Task CompleteOnboardingForUserAsync(
        Guid userId,
        string salesType,
        string experienceLevel,
        List<string> selectedSkillSlugs,
        string? persona = null,
        CancellationToken cancellationToken = default)
    {
        var existingProfile = await databaseContext.UserProfiles
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

        if (existingProfile is not null && existingProfile.IsOnboardingCompleted)
            return;

        if (existingProfile is null)
        {
            existingProfile = new UserProfile { Id = Guid.NewGuid(), UserId = userId };
            databaseContext.UserProfiles.Add(existingProfile);
        }

        existingProfile.SalesType = salesType;
        existingProfile.ExperienceLevel = experienceLevel;
        existingProfile.IsOnboardingCompleted = true;
        if (persona is not null)
            existingProfile.Persona = persona;

        var skillSlugsToEnroll = selectedSkillSlugs
            .Select(slug => slug.Trim())
            .Where(slug => slug.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        skillSlugsToEnroll.Add(DefaultSkillSlug);

        await EnrollSkillsAsync(userId, skillSlugsToEnroll, cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EnrollSkillsAsync(
        Guid userId,
        IEnumerable<string> skillSlugs,
        CancellationToken cancellationToken = default)
    {
        var slugList = skillSlugs.ToList();

        var skills = await databaseContext.Skills
            .Where(skill => slugList.Contains(skill.Slug))
            .ToListAsync(cancellationToken);

        var existingProgress = await databaseContext.UserSkillProgressRecords
            .Where(progressRecord => progressRecord.UserId == userId)
            .ToDictionaryAsync(progressRecord => progressRecord.SkillId, cancellationToken);

        foreach (var skill in skills)
        {
            if (existingProgress.TryGetValue(skill.Id, out var progressRecord))
            {
                if (progressRecord.Status == "locked")
                    progressRecord.Status = "available";
            }
            else
            {
                var lessonCount = await databaseContext.Lessons
                    .CountAsync(lesson => lesson.SkillId == skill.Id, cancellationToken);

                databaseContext.UserSkillProgressRecords.Add(new UserSkillProgress
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SkillId = skill.Id,
                    Status = "available",
                    CompletedLessonCount = 0,
                    TotalLessonCount = lessonCount
                });
            }
        }
    }
}
