using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Features.Avatars;
using Sellevate.Identity.Features.Onboarding.Models;
using Sellevate.Identity.Features.Profile.Models;
using Sellevate.Identity.Features.Profile.Services.Abstract;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Profile.Services.Implementation;

internal sealed class ProfileService(IdentityDbContext databaseContext) : IProfileService
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

        // The streak / XP / skill-progress / exercise-score aggregates are owned by the
        // Gamification and Learning services, which are not extracted yet (roadmap phases
        // 7 & 8). Identity owns only the identity-side fields below, so those numbers are
        // returned as zero here. Once Gamification/Learning exist, this endpoint composes
        // them (via their read APIs or a local replica) — the DTO shape stays identical so
        // the frontend never changes. See docs/API_CONTRACTS.md (Identity service section).
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
}
