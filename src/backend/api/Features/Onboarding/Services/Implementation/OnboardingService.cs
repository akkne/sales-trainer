using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Onboarding.Models;
using SalesTrainer.Api.Features.Onboarding.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Onboarding.Services.Implementation;

internal sealed class OnboardingService(AppDbContext databaseContext) : IOnboardingService
{
    public async Task CompleteOnboardingForUserAsync(
        Guid userId,
        string salesType,
        string experienceLevel,
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

        await databaseContext.SaveChangesAsync(cancellationToken);
    }
}
