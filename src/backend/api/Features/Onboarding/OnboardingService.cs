using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.SkillTree;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Onboarding;

public class OnboardingService(AppDbContext databaseContext)
{
    private const string DefaultSkillSlug = "sales-basics";

    public async Task CompleteOnboardingForUserAsync(
        Guid userId,
        string salesType,
        string experienceLevel,
        List<string> selectedSkillSlugs,
        string? persona = null)
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
        existingProfile.IsOnboardingCompleted = true;
        if (persona is not null)
            existingProfile.Persona = persona;

        // Ensure sales-basics is always enrolled
        var slugs = selectedSkillSlugs
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        slugs.Add(DefaultSkillSlug);

        await EnrollSkillsAsync(userId, slugs);

        await databaseContext.SaveChangesAsync();
    }

    /// <summary>
    /// Creates UserSkillProgress rows for each slug in <paramref name="slugs"/>.
    /// New skills → status "available"; previously locked → restore to "available".
    /// </summary>
    internal async Task EnrollSkillsAsync(Guid userId, IEnumerable<string> slugs)
    {
        var slugList = slugs.ToList();

        var skills = await databaseContext.Skills
            .Where(s => slugList.Contains(s.Slug))
            .ToListAsync();

        var existingProgress = await databaseContext.UserSkillProgressRecords
            .Where(p => p.UserId == userId)
            .ToDictionaryAsync(p => p.SkillId);

        foreach (var skill in skills)
        {
            if (existingProgress.TryGetValue(skill.Id, out var progress))
            {
                if (progress.Status == "locked")
                    progress.Status = "available";
                // If already in_progress / completed — keep as-is
            }
            else
            {
                var lessonCount = await databaseContext.Lessons
                    .CountAsync(l => l.SkillId == skill.Id);

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
