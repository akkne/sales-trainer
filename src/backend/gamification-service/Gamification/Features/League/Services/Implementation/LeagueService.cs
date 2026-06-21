using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Features.League.Models;
using Sellevate.Gamification.Features.League.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.League.Services.Implementation;

internal sealed class LeagueService(GamificationDbContext databaseContext) : ILeagueService
{
    private const int DefaultPeriodLengthDays = 7;

    private static readonly LeagueTier[] DefaultTiers =
    [
        new() { Key = "bronze",  Name = "Бронза",  Color = "#c47b3f", Order = 1 },
        new() { Key = "silver",  Name = "Серебро", Color = "#9aa3ad", Order = 2 },
        new() { Key = "gold",    Name = "Золото",  Color = "#e3b23c", Order = 3 },
        new() { Key = "diamond", Name = "Алмаз",   Color = "#4cc6e8", Order = 4 },
    ];

    public async Task<CurrentLeagueResponseDto> GetCurrentLeagueForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var weekStart = settings.CurrentPeriodStartDate!.Value;
        var periodEndsAt = settings.CurrentPeriodEndsAt!.Value;
        var weekEnd = DateOnly.FromDateTime(periodEndsAt.UtcDateTime);

        var tiers = await LoadTiersAsync(cancellationToken);
        var tierKeys = tiers.Select(tier => tier.Key).ToList();
        var entryTier = tierKeys[0];

        var previousMembershipData = await databaseContext.LeagueMemberships
            .Where(membership => membership.UserId == userId)
            .Join(
                databaseContext.Leagues,
                membership => membership.LeagueId,
                league => league.Id,
                (membership, league) => new { membership.PromotionOutcome, league.Tier, league.WeekStartDate })
            .Where(membershipLeague => membershipLeague.WeekStartDate < weekStart)
            .OrderByDescending(membershipLeague => membershipLeague.WeekStartDate)
            .Select(membershipLeague => new { membershipLeague.PromotionOutcome, membershipLeague.Tier })
            .FirstOrDefaultAsync(cancellationToken);

        var previousWeekOutcome = previousMembershipData?.PromotionOutcome;

        var userTier = previousMembershipData is null
            ? entryTier
            : GetNextTierForOutcome(tierKeys, previousMembershipData.Tier, previousMembershipData.PromotionOutcome);

        var existingThisWeek = await databaseContext.LeagueMemberships
            .Join(
                databaseContext.Leagues,
                membership => membership.LeagueId,
                league => league.Id,
                (membership, league) => new { Membership = membership, League = league })
            .Where(membershipLeague => membershipLeague.Membership.UserId == userId && membershipLeague.League.WeekStartDate == weekStart)
            .FirstOrDefaultAsync(cancellationToken);

        Models.League currentLeague;
        if (existingThisWeek is not null)
        {
            currentLeague = existingThisWeek.League;
        }
        else
        {
            // GA5: use idempotent helpers that tolerate concurrent first-hits via unique-violation catch.
            currentLeague = await GetOrCreateLeagueForWeekAsync(weekStart, weekEnd, userTier, cancellationToken);
            await GetOrJoinLeagueAsync(userId, currentLeague.Id, cancellationToken);
        }

        await SyncWeeklyExperiencePointsForLeagueAsync(
            currentLeague.Id, currentLeague.WeekStartDate, currentLeague.WeekEndDate, cancellationToken);

        var allMemberships = await databaseContext.LeagueMemberships
            .Where(membership => membership.LeagueId == currentLeague.Id)
            .Join(
                databaseContext.UserReplicas,
                membership => membership.UserId,
                user => user.UserId,
                (membership, user) => new { membership, user })
            .OrderByDescending(pair => pair.membership.WeeklyXpAmount)
            .Take(settings.MaximumLeagueParticipantCount)
            .ToListAsync(cancellationToken);

        var participants = allMemberships
            .Select((pair, index) => new LeagueParticipantDto(
                pair.user.UserId.ToString(),
                pair.user.DisplayName,
                pair.membership.WeeklyXpAmount,
                index + 1,
                pair.user.UserId == userId,
                AvatarUrls.For(pair.user.UserId)))
            .ToList();

        var currentUserRank = participants
            .FirstOrDefault(participant => participant.IsCurrentUser)?.Rank ?? 0;

        var tierConfig = tiers.FirstOrDefault(tier => tier.Key == currentLeague.Tier);

        return new CurrentLeagueResponseDto(
            currentLeague.Id,
            currentLeague.Tier,
            tierConfig?.Name ?? currentLeague.Tier,
            tierConfig?.Color ?? string.Empty,
            currentLeague.WeekStartDate,
            currentLeague.WeekEndDate,
            periodEndsAt,
            participants,
            currentUserRank,
            previousWeekOutcome,
            settings.PromotionZoneSize,
            settings.DemotionZoneSize,
            settings.MaximumLeagueParticipantCount);
    }

    public async Task CloseCurrentLeagueAndCreateNextAsync(CancellationToken cancellationToken = default)
    {
        // GA4: re-check inside a transaction so concurrent calls (cron + admin endpoint)
        // cannot both advance the period. The unique index on Leagues(WeekStartDate, Tier)
        // provides the final safety net at the DB level.
        await using var transaction = await databaseContext.Database.BeginTransactionAsync(cancellationToken);

        // Re-read settings with a fresh query inside the transaction to get the current state.
        var settings = await databaseContext.LeagueSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        if (settings.CurrentPeriodEndsAt is null || settings.CurrentPeriodStartDate is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        var weekStart = settings.CurrentPeriodStartDate.Value;
        var currentEnd = DateOnly.FromDateTime(settings.CurrentPeriodEndsAt.Value.UtcDateTime);
        var periodLength = settings.PeriodLengthDays > 0 ? settings.PeriodLengthDays : DefaultPeriodLengthDays;
        var nextWeekStart = currentEnd.AddDays(1);
        var nextWeekEnd = nextWeekStart.AddDays(periodLength - 1);

        // Guard: if the period has already been advanced by a concurrent call, bail out.
        if (settings.CurrentPeriodStartDate.Value >= nextWeekStart)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        var leaguesToClose = await databaseContext.Leagues
            .Where(league => league.WeekStartDate == weekStart)
            .ToListAsync(cancellationToken);

        if (leaguesToClose.Count != 0)
        {
            var tierKeys = (await LoadTiersAsync(cancellationToken)).Select(tier => tier.Key).ToList();
            var nextWeekLeaguesByTier = new Dictionary<string, Models.League>();

            foreach (var league in leaguesToClose)
            {
                var memberships = await databaseContext.LeagueMemberships
                    .Where(membership => membership.LeagueId == league.Id)
                    .OrderByDescending(membership => membership.WeeklyXpAmount)
                    .ToListAsync(cancellationToken);

                for (var membershipIndex = 0; membershipIndex < memberships.Count; membershipIndex++)
                {
                    var membership = memberships[membershipIndex];
                    membership.Rank = membershipIndex + 1;
                    membership.PromotionOutcome = membershipIndex < settings.PromotionZoneSize
                        ? LeaguePromotionOutcomes.Promoted
                        : membershipIndex >= memberships.Count - settings.DemotionZoneSize
                            ? LeaguePromotionOutcomes.Demoted
                            : null;
                }

                foreach (var membership in memberships)
                {
                    var nextTier = GetNextTierForOutcome(tierKeys, league.Tier, membership.PromotionOutcome);

                    if (!nextWeekLeaguesByTier.TryGetValue(nextTier, out var nextLeague))
                    {
                        nextLeague = await GetOrCreateLeagueForWeekAsync(nextWeekStart, nextWeekEnd, nextTier, cancellationToken);
                        nextWeekLeaguesByTier[nextTier] = nextLeague;
                    }

                    await GetOrJoinLeagueAsync(membership.UserId, nextLeague.Id, cancellationToken);
                }
            }
        }

        settings.CurrentPeriodStartDate = nextWeekStart;
        settings.CurrentPeriodEndsAt = EndOfDay(nextWeekEnd);
        await databaseContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RolloverIfDueAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (settings.CurrentPeriodEndsAt is { } endsAt && endsAt <= DateTimeOffset.UtcNow)
        {
            await CloseCurrentLeagueAndCreateNextAsync(cancellationToken);
        }
    }

    public async Task SyncLeagueWeeklyExperiencePointsAsync(Guid leagueId, CancellationToken cancellationToken = default)
    {
        var league = await databaseContext.Leagues
            .FirstOrDefaultAsync(leagueRecord => leagueRecord.Id == leagueId, cancellationToken);
        if (league is null)
        {
            return;
        }

        await SyncWeeklyExperiencePointsForLeagueAsync(
            league.Id, league.WeekStartDate, league.WeekEndDate, cancellationToken);
    }

    /// <summary>
    /// GA6(a): Read-only getter — does not write. Settings are seeded once at startup
    /// via <see cref="LeagueSettingsSeeder"/>. Returns defaults if missing (in-memory/test scenarios).
    /// </summary>
    public async Task<LeagueSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await databaseContext.LeagueSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        // Fallback for in-memory/test scenarios where seeder hasn't run.
        var fallback = new LeagueSettings();
        if (fallback.CurrentPeriodStartDate is null || fallback.CurrentPeriodEndsAt is null)
        {
            var start = GetCurrentWeekStart();
            var end = start.AddDays(fallback.PeriodLengthDays - 1);
            fallback.CurrentPeriodStartDate = start;
            fallback.CurrentPeriodEndsAt = EndOfDay(end);
        }

        return fallback;
    }

    private async Task<IReadOnlyList<LeagueTier>> LoadTiersAsync(CancellationToken cancellationToken)
    {
        var tiers = await databaseContext.LeagueTiers
            .AsNoTracking()
            .OrderBy(tier => tier.Order)
            .ToListAsync(cancellationToken);

        return tiers.Count > 0 ? tiers : DefaultTiers;
    }

    /// <summary>
    /// GA4+GA5: idempotent league creation. If two concurrent callers race, the unique
    /// index on Leagues(WeekStartDate, Tier) causes one to get a unique-violation;
    /// we catch it and re-read the winner.
    /// </summary>
    private async Task<Models.League> GetOrCreateLeagueForWeekAsync(
        DateOnly weekStart,
        DateOnly weekEnd,
        string tier,
        CancellationToken cancellationToken = default)
    {
        var existing = await databaseContext.Leagues
            .FirstOrDefaultAsync(league => league.WeekStartDate == weekStart && league.Tier == tier, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var newLeague = new Models.League
        {
            Id = Guid.NewGuid(),
            Tier = tier,
            WeekStartDate = weekStart,
            WeekEndDate = weekEnd,
        };

        databaseContext.Leagues.Add(newLeague);

        try
        {
            await databaseContext.SaveChangesAsync(cancellationToken);
            return newLeague;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Concurrent caller created the league first; detach and re-read.
            var entry = databaseContext.ChangeTracker.Entries<Models.League>()
                .FirstOrDefault(e => e.Entity.Id == newLeague.Id);
            if (entry is not null)
            {
                entry.State = EntityState.Detached;
            }

            return await databaseContext.Leagues
                .FirstAsync(league => league.WeekStartDate == weekStart && league.Tier == tier, cancellationToken);
        }
    }

    /// <summary>
    /// GA4+GA5: idempotent join. If two concurrent callers race, the unique index on
    /// LeagueMemberships(UserId, LeagueId) causes one to get a unique-violation;
    /// we catch it and re-read the winner.
    /// </summary>
    private async Task<LeagueMembership> GetOrJoinLeagueAsync(
        Guid userId,
        Guid leagueId,
        CancellationToken cancellationToken = default)
    {
        var existing = await databaseContext.LeagueMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.LeagueId == leagueId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var newMembership = new LeagueMembership
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LeagueId = leagueId,
            WeeklyXpAmount = 0,
            Rank = 0,
        };

        databaseContext.LeagueMemberships.Add(newMembership);

        try
        {
            await databaseContext.SaveChangesAsync(cancellationToken);
            return newMembership;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            var entry = databaseContext.ChangeTracker.Entries<LeagueMembership>()
                .FirstOrDefault(e => e.Entity.Id == newMembership.Id);
            if (entry is not null)
            {
                entry.State = EntityState.Detached;
            }

            return await databaseContext.LeagueMemberships
                .FirstAsync(m => m.UserId == userId && m.LeagueId == leagueId, cancellationToken);
        }
    }

    private async Task SyncWeeklyExperiencePointsForLeagueAsync(
        Guid leagueId,
        DateOnly weekStart,
        DateOnly weekEnd,
        CancellationToken cancellationToken = default)
    {
        var membershipUserIds = await databaseContext.LeagueMemberships
            .Where(membership => membership.LeagueId == leagueId)
            .Select(membership => membership.UserId)
            .ToListAsync(cancellationToken);

        var weeklyExperiencePointsByUserId = await databaseContext.UserExperiencePointsRecords
            .Where(record =>
                membershipUserIds.Contains(record.UserId) &&
                DateOnly.FromDateTime(record.EarnedAt) >= weekStart &&
                DateOnly.FromDateTime(record.EarnedAt) <= weekEnd)
            .GroupBy(record => record.UserId)
            .Select(group => new { UserId = group.Key, Total = group.Sum(record => record.Amount) })
            .ToDictionaryAsync(entry => entry.UserId, entry => entry.Total, cancellationToken);

        var membershipsToUpdate = await databaseContext.LeagueMemberships
            .Where(membership => membership.LeagueId == leagueId)
            .ToListAsync(cancellationToken);

        foreach (var membership in membershipsToUpdate)
        {
            if (weeklyExperiencePointsByUserId.TryGetValue(membership.UserId, out var weeklyExperiencePoints))
            {
                membership.WeeklyXpAmount = weeklyExperiencePoints;
            }
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        return inner is not null &&
               (inner.GetType().Name == "PostgresException" || inner.GetType().FullName?.Contains("Npgsql") == true) &&
               inner.Message.Contains("23505");
    }

    private static string GetNextTierForOutcome(List<string> tierOrder, string currentTier, string? outcome)
    {
        var tierIndex = tierOrder.IndexOf(currentTier);
        if (tierIndex < 0)
        {
            tierIndex = 0;
        }

        return outcome switch
        {
            LeaguePromotionOutcomes.Promoted => tierIndex < tierOrder.Count - 1 ? tierOrder[tierIndex + 1] : tierOrder[tierIndex],
            LeaguePromotionOutcomes.Demoted => tierIndex > 0 ? tierOrder[tierIndex - 1] : tierOrder[0],
            _ => tierOrder[tierIndex]
        };
    }

    private static DateTimeOffset EndOfDay(DateOnly date) =>
        new(date.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc));

    private static DateOnly GetCurrentWeekStart()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-daysFromMonday);
    }
}
