using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.League.Models;
using SalesTrainer.Api.Features.League.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.League.Services.Implementation;

internal sealed class LeagueService(AppDbContext databaseContext) : ILeagueService
{
    private const int WeekLengthDays = 7;
    private const int WeekEndOffsetDays = 6;

    private static readonly string[] TierOrder = ["bronze", "silver", "gold", "diamond"];

    public async Task<CurrentLeagueResponseDto> GetCurrentLeagueForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var weekStart = GetCurrentWeekStart();
        var weekEnd = weekStart.AddDays(WeekEndOffsetDays);
        var previousWeekStart = weekStart.AddDays(-WeekLengthDays);

        var previousMembershipData = await databaseContext.LeagueMemberships
            .Join(
                databaseContext.Leagues,
                membership => membership.LeagueId,
                league => league.Id,
                (membership, league) => new { membership.PromotionOutcome, league.Tier, league.WeekStartDate })
            .Where(membershipLeague => membershipLeague.WeekStartDate < weekStart)
            .OrderByDescending(membershipLeague => membershipLeague.WeekStartDate)
            .Select(membershipLeague => new { membershipLeague.PromotionOutcome, membershipLeague.Tier, membershipLeague.WeekStartDate })
            .FirstOrDefaultAsync(cancellationToken);

        var previousWeekOutcome = previousMembershipData?.WeekStartDate == previousWeekStart
            ? previousMembershipData.PromotionOutcome
            : null;

        var userTier = previousMembershipData is null
            ? "bronze"
            : GetNextTierForOutcome(previousMembershipData.Tier, previousMembershipData.PromotionOutcome);

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
            currentLeague = await databaseContext.Leagues
                .FirstOrDefaultAsync(league => league.WeekStartDate == weekStart && league.Tier == userTier, cancellationToken)
                ?? await CreateLeagueForWeekAsync(weekStart, weekEnd, userTier, cancellationToken);

            await JoinLeagueAsync(userId, currentLeague.Id, cancellationToken);
        }

        await SyncWeeklyExperiencePointsForLeagueAsync(currentLeague.Id, weekStart, cancellationToken);

        var settings = await LoadSettingsAsync(cancellationToken);

        var allMemberships = await databaseContext.LeagueMemberships
            .Where(membership => membership.LeagueId == currentLeague.Id)
            .Join(
                databaseContext.Users,
                membership => membership.UserId,
                user => user.Id,
                (membership, user) => new { membership, user })
            .OrderByDescending(pair => pair.membership.WeeklyXpAmount)
            .Take(settings.MaximumLeagueParticipantCount)
            .ToListAsync(cancellationToken);

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
            currentUserRank,
            previousWeekOutcome,
            settings.PromotionZoneSize,
            settings.DemotionZoneSize,
            settings.MaximumLeagueParticipantCount);
    }

    public async Task CloseCurrentLeagueAndCreateNextAsync(CancellationToken cancellationToken = default)
    {
        var weekStart = GetCurrentWeekStart();
        var nextWeekStart = weekStart.AddDays(WeekLengthDays);
        var nextWeekEnd = nextWeekStart.AddDays(WeekEndOffsetDays);

        var leaguesToClose = await databaseContext.Leagues
            .Where(league => league.WeekStartDate == weekStart)
            .ToListAsync(cancellationToken);

        if (leaguesToClose.Count == 0) return;

        var settings = await LoadSettingsAsync(cancellationToken);
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
                    ? "promoted"
                    : membershipIndex >= memberships.Count - settings.DemotionZoneSize
                        ? "demoted"
                        : null;
            }

            foreach (var membership in memberships)
            {
                var nextTier = GetNextTierForOutcome(league.Tier, membership.PromotionOutcome);

                if (!nextWeekLeaguesByTier.TryGetValue(nextTier, out var nextLeague))
                {
                    nextLeague = await databaseContext.Leagues
                        .FirstOrDefaultAsync(leagueRecord => leagueRecord.WeekStartDate == nextWeekStart && leagueRecord.Tier == nextTier, cancellationToken)
                        ?? await CreateLeagueForWeekAsync(nextWeekStart, nextWeekEnd, nextTier, cancellationToken);
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

        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SyncLeagueWeeklyXpAsync(Guid leagueId, CancellationToken cancellationToken = default)
    {
        var league = await databaseContext.Leagues
            .FirstOrDefaultAsync(leagueRecord => leagueRecord.Id == leagueId, cancellationToken);
        if (league is null) return;

        await SyncWeeklyExperiencePointsForLeagueAsync(league.Id, league.WeekStartDate, cancellationToken);
    }

    private async Task<LeagueSettings> LoadSettingsAsync(CancellationToken cancellationToken) =>
        await databaseContext.LeagueSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
        ?? new LeagueSettings();

    private async Task<Models.League> CreateLeagueForWeekAsync(
        DateOnly weekStart,
        DateOnly weekEnd,
        string tier = "bronze",
        CancellationToken cancellationToken = default)
    {
        var newLeague = new Models.League
        {
            Id = Guid.NewGuid(),
            Tier = tier,
            WeekStartDate = weekStart,
            WeekEndDate = weekEnd
        };

        databaseContext.Leagues.Add(newLeague);
        await databaseContext.SaveChangesAsync(cancellationToken);
        return newLeague;
    }

    private async Task<LeagueMembership> JoinLeagueAsync(
        Guid userId,
        Guid leagueId,
        CancellationToken cancellationToken = default)
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
        await databaseContext.SaveChangesAsync(cancellationToken);
        return newMembership;
    }

    private async Task SyncWeeklyExperiencePointsForLeagueAsync(
        Guid leagueId,
        DateOnly weekStart,
        CancellationToken cancellationToken = default)
    {
        var membershipUserIds = await databaseContext.LeagueMemberships
            .Where(membership => membership.LeagueId == leagueId)
            .Select(membership => membership.UserId)
            .ToListAsync(cancellationToken);

        var weeklyExperiencePointsByUserId = await databaseContext.UserXpRecords
            .Where(experiencePointRecord =>
                membershipUserIds.Contains(experiencePointRecord.UserId) &&
                DateOnly.FromDateTime(experiencePointRecord.EarnedAt) >= weekStart)
            .GroupBy(experiencePointRecord => experiencePointRecord.UserId)
            .Select(group => new { UserId = group.Key, Total = group.Sum(record => record.Amount) })
            .ToDictionaryAsync(entry => entry.UserId, entry => entry.Total, cancellationToken);

        var membershipsToUpdate = await databaseContext.LeagueMemberships
            .Where(membership => membership.LeagueId == leagueId)
            .ToListAsync(cancellationToken);

        foreach (var membership in membershipsToUpdate)
        {
            if (weeklyExperiencePointsByUserId.TryGetValue(membership.UserId, out var weeklyExperiencePoints))
                membership.WeeklyXpAmount = weeklyExperiencePoints;
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
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
