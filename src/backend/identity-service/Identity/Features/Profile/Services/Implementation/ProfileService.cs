using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Features.Avatars;
using Sellevate.Identity.Features.Onboarding.Models;
using Sellevate.Identity.Features.Profile.Models;
using Sellevate.Identity.Features.Profile.Services.Abstract;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Profile.Services.Implementation;

/// <summary>
/// Reads and writes the caller's own profile. Persona lives on the one-to-one
/// <see cref="UserProfile"/> row, which may not exist yet — every write path upserts it rather than
/// assuming onboarding created it. A display-name change is published to the replica-holding
/// services (ai, notification, …) so their copies do not drift.
/// </summary>
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
        await UpsertPersonaAsync(userId, persona, cancellationToken);

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

        if (!string.IsNullOrWhiteSpace(persona))
        {
            await UpsertPersonaAsync(userId, persona, cancellationToken);
        }

        await userEventPublisher.PublishUpdatedAsync(
            new UserUpdatedEvent(userId, displayName, user.AvatarKey), cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Stages the persona on the caller's profile row, creating that row when onboarding has not
    /// produced one yet. Does not save: the caller decides the transaction boundary.
    /// </summary>
    private async Task UpsertPersonaAsync(
        Guid userId,
        string persona,
        CancellationToken cancellationToken)
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
            return;
        }

        userProfile.Persona = persona;
    }
}
