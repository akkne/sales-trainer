using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Eventing;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.Gamification.Services.Implementation;

internal sealed class StreakService(
    GamificationDbContext databaseContext,
    IGamificationSettingsService settingsService,
    IExperiencePointsGrantService experiencePointsGrantService,
    IGamificationEventPublisher eventPublisher,
    IStreakClock streakClock) : IStreakService
{
    public async Task RegisterActivityAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var today = streakClock.Today();

        var streak = await databaseContext.UserStreaks
            .FirstOrDefaultAsync(record => record.UserId == userId, cancellationToken);

        if (streak is null)
        {
            streak = new UserStreak
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CurrentStreakDayCount = 1,
                LongestStreakDayCount = 1,
                LastActivityDate = today,
            };
            databaseContext.UserStreaks.Add(streak);

            try
            {
                await databaseContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Another process concurrently inserted a streak for this user.
                // Detach the failed entry and re-read the winner.
                var entry = databaseContext.ChangeTracker.Entries<UserStreak>()
                    .FirstOrDefault(e => e.Entity.UserId == userId);
                if (entry is not null)
                {
                    entry.State = EntityState.Detached;
                }

                streak = await databaseContext.UserStreaks
                    .FirstOrDefaultAsync(record => record.UserId == userId, cancellationToken);

                if (streak is null)
                {
                    return;
                }
            }

            await AwardMilestoneIfReachedAsync(userId, streak.CurrentStreakDayCount, cancellationToken);
            return;
        }

        if (streak.LastActivityDate == today)
        {
            return;
        }

        streak.CurrentStreakDayCount = streak.LastActivityDate == today.AddDays(-1)
            ? streak.CurrentStreakDayCount + 1
            : 1;

        if (streak.CurrentStreakDayCount > streak.LongestStreakDayCount)
        {
            streak.LongestStreakDayCount = streak.CurrentStreakDayCount;
        }

        streak.LastActivityDate = today;
        await databaseContext.SaveChangesAsync(cancellationToken);

        await AwardMilestoneIfReachedAsync(userId, streak.CurrentStreakDayCount, cancellationToken);
    }

    public async Task<int> GetCurrentStreakDayCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await databaseContext.UserStreaks
            .Where(record => record.UserId == userId)
            .Select(record => (int?)record.CurrentStreakDayCount)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        return inner is not null &&
               (inner.GetType().Name == "PostgresException" || inner.GetType().FullName?.Contains("Npgsql") == true) &&
               inner.Message.Contains("23505");
    }

    private async Task AwardMilestoneIfReachedAsync(Guid userId, int streakDayCount, CancellationToken cancellationToken)
    {
        var bonusExperiencePoints = await settingsService.GetStreakBonusExperiencePointsAsync(streakDayCount, cancellationToken);
        if (bonusExperiencePoints <= 0)
        {
            return;
        }

        await experiencePointsGrantService.GrantAsync(
            userId, bonusExperiencePoints, ExperiencePointsSources.StreakBonus, cancellationToken: cancellationToken);

        await eventPublisher.PublishStreakMilestoneAsync(
            new StreakMilestoneEvent(userId, streakDayCount, bonusExperiencePoints), cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);
    }
}
