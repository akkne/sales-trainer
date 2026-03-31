using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.League;

public class LeagueService(AppDbContext databaseContext)
{
    private const int MaxLeagueParticipantCount = 30;
    private const int PromotionZoneSize = 10;
    private const int DemotionZoneSize = 5;

    public async Task<CurrentLeagueResponseDto> GetCurrentLeagueForUserAsync(Guid userId)
    {
        var weekStart = GetCurrentWeekStart();
        var weekEnd = weekStart.AddDays(6);

        var currentLeague = await databaseContext.Leagues
            .FirstOrDefaultAsync(league =>
                league.WeekStartDate == weekStart && league.Tier == "bronze");

        if (currentLeague is null)
        {
            currentLeague = await CreateLeagueForWeekAsync(weekStart, weekEnd);
        }

        var currentMembership = await databaseContext.LeagueMemberships
            .FirstOrDefaultAsync(membership =>
                membership.UserId == userId && membership.LeagueId == currentLeague.Id);

        if (currentMembership is null)
        {
            currentMembership = await JoinLeagueAsync(userId, currentLeague.Id);
        }

        await SyncWeeklyXpForLeagueAsync(currentLeague.Id, weekStart);

        var allMemberships = await databaseContext.LeagueMemberships
            .Where(membership => membership.LeagueId == currentLeague.Id)
            .Join(
                databaseContext.Users,
                membership => membership.UserId,
                user => user.Id,
                (membership, user) => new { membership, user })
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
            .FirstOrDefault(participant => participant.IsCurrentUser)?.Rank ?? 0;

        return new CurrentLeagueResponseDto(
            currentLeague.Id,
            currentLeague.Tier,
            currentLeague.WeekStartDate,
            currentLeague.WeekEndDate,
            participants,
            currentUserRank);
    }

    public async Task CloseCurrentLeagueAndCreateNextAsync()
    {
        var weekStart = GetCurrentWeekStart();

        var leagueToClose = await databaseContext.Leagues
            .FirstOrDefaultAsync(league => league.WeekStartDate == weekStart);

        if (leagueToClose is null) return;

        var allMemberships = await databaseContext.LeagueMemberships
            .Where(membership => membership.LeagueId == leagueToClose.Id)
            .OrderByDescending(membership => membership.WeeklyXpAmount)
            .ToListAsync();

        for (var memberIndex = 0; memberIndex < allMemberships.Count; memberIndex++)
        {
            var membership = allMemberships[memberIndex];
            membership.Rank = memberIndex + 1;
            membership.PromotionOutcome = memberIndex < PromotionZoneSize
                ? "promoted"
                : memberIndex >= allMemberships.Count - DemotionZoneSize
                    ? "demoted"
                    : null;
        }

        var nextWeekStart = weekStart.AddDays(7);
        var nextWeekEnd = nextWeekStart.AddDays(6);
        await CreateLeagueForWeekAsync(nextWeekStart, nextWeekEnd);

        await databaseContext.SaveChangesAsync();
    }

    private async Task<League> CreateLeagueForWeekAsync(DateOnly weekStart, DateOnly weekEnd)
    {
        var newLeague = new League
        {
            Id = Guid.NewGuid(),
            Tier = "bronze",
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
            .Where(membership => membership.LeagueId == leagueId)
            .Select(membership => membership.UserId)
            .ToListAsync();

        var weeklyXpByUserId = await databaseContext.UserXpRecords
            .Where(xp =>
                membershipUserIds.Contains(xp.UserId) &&
                DateOnly.FromDateTime(xp.EarnedAt) >= weekStart)
            .GroupBy(xp => xp.UserId)
            .Select(grouping => new
            {
                UserId = grouping.Key,
                TotalWeeklyXp = grouping.Sum(xp => xp.Amount)
            })
            .ToDictionaryAsync(entry => entry.UserId, entry => entry.TotalWeeklyXp);

        var membershipsToUpdate = await databaseContext.LeagueMemberships
            .Where(membership => membership.LeagueId == leagueId)
            .ToListAsync();

        foreach (var membership in membershipsToUpdate)
        {
            if (weeklyXpByUserId.TryGetValue(membership.UserId, out var weeklyXp))
                membership.WeeklyXpAmount = weeklyXp;
        }

        await databaseContext.SaveChangesAsync();
    }

    private static DateOnly GetCurrentWeekStart()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-daysFromMonday);
    }
}
