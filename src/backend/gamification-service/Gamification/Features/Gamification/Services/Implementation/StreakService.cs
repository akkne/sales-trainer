using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Common.Extensions;
using Sellevate.Gamification.Eventing;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.Gamification.Services.Implementation;

/// <summary>
/// Owns a user's consecutive-activity day count and the milestone bonuses it earns.
///
/// <para>
/// <b>Idempotent per day.</b> "Today" comes from <see cref="IStreakClock"/>, never from
/// <c>DateTime.UtcNow</c> directly, and a second registration on a day already recorded returns
/// without touching anything — so the caller may report every completed exercise without counting a
/// busy day twice. A gap of more than one day restarts the count at one; the longest-ever count is
/// never lowered.
/// </para>
///
/// <para>
/// Phase 40.13: each read gets its own short transaction rather than the whole method sharing one.
/// The insert recovers from a unique violation and re-reads the winner; inside a single long
/// transaction that violation would poison the transaction and the re-read could not run. Writes
/// need no scope — EF opens an implicit transaction per <c>SaveChangesAsync</c>, which is what
/// triggers <c>SET LOCAL</c>.
/// </para>
/// </summary>
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

        UserStreak? streak;
        await using (await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken))
        {
            streak = await databaseContext.UserStreaks
                .FirstOrDefaultAsync(record => record.UserId == userId, cancellationToken);
        }

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
            catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
            {
                var failedEntry = databaseContext.ChangeTracker.Entries<UserStreak>()
                    .FirstOrDefault(entry => entry.Entity.UserId == userId);
                if (failedEntry is not null)
                {
                    failedEntry.State = EntityState.Detached;
                }

                await using (await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken))
                {
                    streak = await databaseContext.UserStreaks
                        .FirstOrDefaultAsync(record => record.UserId == userId, cancellationToken);
                }

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
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);
        return await databaseContext.UserStreaks
            .Where(record => record.UserId == userId)
            .Select(record => (int?)record.CurrentStreakDayCount)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;
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
