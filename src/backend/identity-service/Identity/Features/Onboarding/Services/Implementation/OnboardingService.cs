using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Features.Onboarding.Models;
using Sellevate.Identity.Features.Onboarding.Services.Abstract;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Onboarding.Services.Implementation;

/// <summary>
/// Writes the onboarding answers onto the caller's profile row, creating that row if invite acceptance
/// did not. Returns without touching anything once onboarding is marked complete — the first set of
/// answers is the one kept.
/// </summary>
internal sealed class OnboardingService(IdentityDbContext databaseContext) : IOnboardingService
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
        {
            return;
        }

        if (existingProfile is null)
        {
            existingProfile = new UserProfile { Id = Guid.NewGuid(), UserId = userId };
            databaseContext.UserProfiles.Add(existingProfile);
        }

        existingProfile.SalesType = salesType;
        existingProfile.ExperienceLevel = experienceLevel;
        existingProfile.IsOnboardingCompleted = true;
        if (persona is not null)
        {
            existingProfile.Persona = persona;
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
    }
}
