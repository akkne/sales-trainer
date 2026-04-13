using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Onboarding.Models;
using SalesTrainer.Api.Features.Profile.Models;
using SalesTrainer.Api.Features.Profile.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Profile.Services.Implementation;

internal sealed class ProfileService(AppDbContext databaseContext) : IProfileService
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

        var streakRecord = await databaseContext.UserStreaks
            .FirstOrDefaultAsync(streak => streak.UserId == userId, cancellationToken);

        var totalExperiencePointsAmount = await databaseContext.UserXpRecords
            .Where(experiencePointRecord => experiencePointRecord.UserId == userId)
            .SumAsync(experiencePointRecord => (int?)experiencePointRecord.Amount, cancellationToken) ?? 0;

        var completedSkillCount = await databaseContext.UserSkillProgressRecords
            .CountAsync(progressRecord => progressRecord.UserId == userId && progressRecord.Status == "completed", cancellationToken);

        var totalSkillCount = await databaseContext.UserSkillProgressRecords
            .CountAsync(progressRecord => progressRecord.UserId == userId, cancellationToken);

        var averageExerciseScore = await databaseContext.UserExerciseAttempts
            .Where(attempt => attempt.UserId == userId)
            .AverageAsync(attempt => (double?)attempt.Score, cancellationToken) ?? 0.0;

        return new UserProfileStatsDto(
            user.DisplayName,
            user.Email,
            streakRecord?.CurrentStreakDayCount ?? 0,
            streakRecord?.LongestStreakDayCount ?? 0,
            totalExperiencePointsAmount,
            completedSkillCount,
            totalSkillCount,
            Math.Round(averageExerciseScore, 1),
            userProfile?.Persona);
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
