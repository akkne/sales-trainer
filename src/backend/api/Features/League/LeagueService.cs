using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.League;

public class LeagueService(AppDbContext databaseContext)
{
    private const int MaxLeagueParticipantCount = 30;
    private const int PromotionZoneSize = 10;
    private const int DemotionZoneSize = 5;

    private static readonly string[] TierOrder = ["bronze", "silver", "gold", "diamond"];

    public async Task<CurrentLeagueResponseDto> GetCurrentLeagueForUserAsync(Guid userId)
    {
        var weekStart = GetCurrentWeekStart();
        var weekEnd = weekStart.AddDays(6);
        var previousWeekStart = weekStart.AddDays(-7);

        // Find user's most recent past membership to determine tier and previous outcome
        var previousMembershipData = await databaseContext.LeagueMemberships
            .Join(
                databaseContext.Leagues,
                m => m.LeagueId,
                l => l.Id,
                (m, l) => new { m.PromotionOutcome, l.Tier, l.WeekStartDate })
            .Where(x => x.WeekStartDate < weekStart)
            .OrderByDescending(x => x.WeekStartDate)
            .Select(x => new { x.PromotionOutcome, x.Tier, x.WeekStartDate })
            .FirstOrDefaultAsync();

        // Only expose outcome if from immediately last week (avoid stale banners)
        var previousWeekOutcome = previousMembershipData?.WeekStartDate == previousWeekStart
            ? previousMembershipData.PromotionOutcome
            : null;

        var userTier = previousMembershipData is null
            ? "bronze"
            : GetNextTierForOutcome(previousMembershipData.Tier, previousMembershipData.PromotionOutcome);

        // Check if user already has a membership for this week (pre-created by closure job)
        var existingThisWeek = await databaseContext.LeagueMemberships
            .Join(
                databaseContext.Leagues,
                m => m.LeagueId,
                l => l.Id,
                (m, l) => new { Membership = m, League = l })
            .Where(x => x.Membership.UserId == userId && x.League.WeekStartDate == weekStart)
            .FirstOrDefaultAsync();

        League currentLeague;
        if (existingThisWeek is not null)
        {
            currentLeague = existingThisWeek.League;
        }
        else
        {
            currentLeague = await databaseContext.Leagues
                .FirstOrDefaultAsync(l => l.WeekStartDate == weekStart && l.Tier == userTier)
                ?? await CreateLeagueForWeekAsync(weekStart, weekEnd, userTier);

            await JoinLeagueAsync(userId, currentLeague.Id);
        }

        await SyncWeeklyXpForLeagueAsync(currentLeague.Id, weekStart);

        var allMemberships = await databaseContext.LeagueMemberships
            .Where(m => m.LeagueId == currentLeague.Id)
            .Join(
                databaseContext.Users,
                m => m.UserId,
                u => u.Id,
                (m, u) => new { membership = m, user = u })
            .OrderByDescending(pair => pair.membership.WeeklyXpAmount)
            .Take(MaxLeagueParticipantCount)
            .ToListAsync();

        var participants = allMemberships
            .Select((pair, index) => new LeagueParticipantDto(
                pair.user.Id.ToString(),
                pair.user.DisplayName,
                pair.membership.WeeklyXpAmount,
                index + 1,
                pair.user.Id == userId))
            .ToList();

        var currentUserRank = participants
            .FirstOrDefault(p => p.IsCurrentUser)?.Rank ?? 0;

        return new CurrentLeagueResponseDto(
            currentLeague.Id,
            currentLeague.Tier,
            currentLeague.WeekStartDate,
            currentLeague.WeekEndDate,
            participants,
            currentUserRank,
            previousWeekOutcome);
    }

    public async Task CloseCurrentLeagueAndCreateNextAsync()
    {
        var weekStart = GetCurrentWeekStart();
        var nextWeekStart = weekStart.AddDays(7);
        var nextWeekEnd = nextWeekStart.AddDays(6);

        var leaguesToClose = await databaseContext.Leagues
            .Where(l => l.WeekStartDate == weekStart)
            .ToListAsync();

        if (leaguesToClose.Count == 0) return;

        // Cache already-created leagues for next week to avoid duplicates
        var nextWeekLeaguesByTier = new Dictionary<string, League>();

        foreach (var league in leaguesToClose)
        {
            var memberships = await databaseContext.LeagueMemberships
                .Where(m => m.LeagueId == league.Id)
                .OrderByDescending(m => m.WeeklyXpAmount)
                .ToListAsync();

            // Assign ranks and promotion outcomes
            for (var i = 0; i < memberships.Count; i++)
            {
                var membership = memberships[i];
                membership.Rank = i + 1;
                membership.PromotionOutcome = i < PromotionZoneSize
                    ? "promoted"
                    : i >= memberships.Count - DemotionZoneSize
                        ? "demoted"
                        : null;
            }

            // Pre-create memberships in next week's leagues
            foreach (var membership in memberships)
            {
                var nextTier = GetNextTierForOutcome(league.Tier, membership.PromotionOutcome);

                if (!nextWeekLeaguesByTier.TryGetValue(nextTier, out var nextLeague))
                {
                    nextLeague = await databaseContext.Leagues
                        .FirstOrDefaultAsync(l => l.WeekStartDate == nextWeekStart && l.Tier == nextTier)
                        ?? await CreateLeagueForWeekAsync(nextWeekStart, nextWeekEnd, nextTier);
                    nextWeekLeaguesByTier[nextTier] = nextLeague;
                }

                databaseContext.LeagueMemberships.Add(new LeagueMembership
                {
                    Id = Guid.NewGuid(),
                    UserId = membership.UserId,
                    LeagueId = nextLeague.Id,
                    WeeklyXpAmount = 0,
                    Rank = 0
                });
            }
        }

        await databaseContext.SaveChangesAsync();
    }

    private async Task<League> CreateLeagueForWeekAsync(DateOnly weekStart, DateOnly weekEnd, string tier = "bronze")
    {
        var newLeague = new League
        {
            Id = Guid.NewGuid(),
            Tier = tier,
            WeekStartDate = weekStart,
            WeekEndDate = weekEnd
        };

        databaseContext.Leagues.Add(newLeague);
        await databaseContext.SaveChangesAsync();
        return newLeague;
    }

    private async Task<LeagueMembership> JoinLeagueAsync(Guid userId, Guid leagueId)
    {
        var newMembership = new LeagueMembership
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LeagueId = leagueId,
            WeeklyXpAmount = 0,
            Rank = 0
        };

        databaseContext.LeagueMemberships.Add(newMembership);
        await databaseContext.SaveChangesAsync();
        return newMembership;
    }

    private async Task SyncWeeklyXpForLeagueAsync(Guid leagueId, DateOnly weekStart)
    {
        var membershipUserIds = await databaseContext.LeagueMemberships
            .Where(m => m.LeagueId == leagueId)
            .Select(m => m.UserId)
            .ToListAsync();

        var weeklyXpByUserId = await databaseContext.UserXpRecords
            .Where(xp =>
                membershipUserIds.Contains(xp.UserId) &&
                DateOnly.FromDateTime(xp.EarnedAt) >= weekStart)
            .GroupBy(xp => xp.UserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(e => e.UserId, e => e.Total);

        var membershipsToUpdate = await databaseContext.LeagueMemberships
            .Where(m => m.LeagueId == leagueId)
            .ToListAsync();

        foreach (var membership in membershipsToUpdate)
        {
            if (weeklyXpByUserId.TryGetValue(membership.UserId, out var weeklyXp))
                membership.WeeklyXpAmount = weeklyXp;
        }

        await databaseContext.SaveChangesAsync();
    }

    private static string GetNextTierForOutcome(string currentTier, string? outcome)
    {
        var index = Array.IndexOf(TierOrder, currentTier);
        if (index < 0) index = 0;

        return outcome switch
        {
            "promoted" => index < TierOrder.Length - 1 ? TierOrder[index + 1] : TierOrder[index],
            "demoted"  => index > 0 ? TierOrder[index - 1] : TierOrder[0],
            _          => TierOrder[index]
        };
    }

    private static DateOnly GetCurrentWeekStart()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-daysFromMonday);
    }
}
