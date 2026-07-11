using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Features.Avatars;
using Sellevate.Identity.Features.Onboarding.Models;
using Sellevate.Identity.Features.Profile.Models;
using Sellevate.Identity.Features.Profile.Services.Abstract;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Profile.Services.Implementation;

internal sealed class ProfileService(
    IdentityDbContext databaseContext,
    IUserEventPublisher userEventPublisher) : IProfileService
{
    public async Task<UserProfileStatsDto> GetProfileStatsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await databaseContext.Users
            .FirstOrDefaultAsync(userRecord => userRecord.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        var userProfile = await databaseContext.UserProfiles
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

        return new UserProfileStatsDto(
            user.DisplayName,
            user.Email,
            CurrentStreakDayCount: 0,
            LongestStreakDayCount: 0,
            TotalXpAmount: 0,
            CompletedSkillCount: 0,
            TotalSkillCount: 0,
            AverageExerciseScore: 0.0,
            userProfile?.Persona,
            AvatarUrls.For(userId));
    }

    public async Task UpdatePersonaForUserAsync(
        Guid userId,
        string persona,
        CancellationToken cancellationToken = default)
    {
        var userProfile = await databaseContext.UserProfiles
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

        if (userProfile is null)
        {
            databaseContext.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Persona = persona,
                SalesType = "",
                ExperienceLevel = "",
                Goal = "",
                IsOnboardingCompleted = false
            });
        }
        else
        {
            userProfile.Persona = persona;
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateProfileForUserAsync(
        Guid userId,
        string displayName,
        string? persona,
        CancellationToken cancellationToken = default)
    {
        var user = await databaseContext.Users
            .FirstOrDefaultAsync(userRecord => userRecord.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        user.DisplayName = displayName;

        // Persona lives on the one-to-one UserProfile row; upsert only when provided.
        if (!string.IsNullOrWhiteSpace(persona))
        {
            var userProfile = await databaseContext.UserProfiles
                .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

            if (userProfile is null)
            {
                databaseContext.UserProfiles.Add(new UserProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Persona = persona,
                    SalesType = "",
                    ExperienceLevel = "",
                    Goal = "",
                    IsOnboardingCompleted = false
                });
            }
            else
            {
                userProfile.Persona = persona;
            }
        }

        // Propagate the new display name to replica-holding services (ai, notification, …).
        await userEventPublisher.PublishUpdatedAsync(
            new UserUpdatedEvent(userId, displayName, user.AvatarKey), cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);
    }
}
