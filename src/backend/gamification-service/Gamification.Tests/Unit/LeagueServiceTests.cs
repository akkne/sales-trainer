using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Features.League.Models;
using Sellevate.Gamification.Features.League.Services.Implementation;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Tests.Unit;

[TestFixture]
public sealed class LeagueServiceTests
{
    private GamificationDbContext _databaseContext = null!;
    private LeagueService _leagueService = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseContext = GamificationDbContextFactory.CreateInMemory();
        _leagueService = new LeagueService(_databaseContext);
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    private async Task SeedTiersAsync()
    {
        _databaseContext.LeagueTiers.AddRange(
            new LeagueTier { Id = Guid.NewGuid(), Key = "bronze", Name = "Bronze", Color = "#1", Order = 1 },
            new LeagueTier { Id = Guid.NewGuid(), Key = "silver", Name = "Silver", Color = "#2", Order = 2 },
            new LeagueTier { Id = Guid.NewGuid(), Key = "gold", Name = "Gold", Color = "#3", Order = 3 });
        await _databaseContext.SaveChangesAsync();
    }

    [Test]
    public async Task GetSettingsAsync_InitializesPeriodWhenMissing()
    {
        var settings = await _leagueService.GetSettingsAsync();

        settings.CurrentPeriodStartDate.Should().NotBeNull();
        settings.CurrentPeriodEndsAt.Should().NotBeNull();
        settings.PeriodLengthDays.Should().Be(7);
    }

    [Test]
    public async Task CloseCurrentLeagueAndCreateNextAsync_PromotesTopAndRelegatesBottom()
    {
        await SeedTiersAsync();

        var weekStart = new DateOnly(2026, 6, 1);
        var weekEnd = new DateOnly(2026, 6, 7);
        _databaseContext.LeagueSettings.Add(new LeagueSettings
        {
            Id = Guid.NewGuid(),
            MaximumLeagueParticipantCount = 10,
            PromotionZoneSize = 1,
            DemotionZoneSize = 1,
            CurrentPeriodStartDate = weekStart,
            CurrentPeriodEndsAt = new DateTimeOffset(weekEnd.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc)),
            PeriodLengthDays = 7,
        });

        var silverLeagueId = Guid.NewGuid();
        _databaseContext.Leagues.Add(new League
        {
            Id = silverLeagueId,
            Tier = "silver",
            WeekStartDate = weekStart,
            WeekEndDate = weekEnd,
        });

        var topUserId = Guid.NewGuid();
        var middleUserId = Guid.NewGuid();
        var bottomUserId = Guid.NewGuid();
        _databaseContext.LeagueMemberships.AddRange(
            new LeagueMembership { Id = Guid.NewGuid(), UserId = topUserId, LeagueId = silverLeagueId, WeeklyXpAmount = 300 },
            new LeagueMembership { Id = Guid.NewGuid(), UserId = middleUserId, LeagueId = silverLeagueId, WeeklyXpAmount = 200 },
            new LeagueMembership { Id = Guid.NewGuid(), UserId = bottomUserId, LeagueId = silverLeagueId, WeeklyXpAmount = 100 });
        await _databaseContext.SaveChangesAsync();

        await _leagueService.CloseCurrentLeagueAndCreateNextAsync();

        var nextWeekStart = weekEnd.AddDays(1);
        var nextLeagues = await _databaseContext.Leagues
            .Where(league => league.WeekStartDate == nextWeekStart)
            .ToListAsync();

        var goldLeague = nextLeagues.SingleOrDefault(league => league.Tier == "gold");
        var bronzeLeague = nextLeagues.SingleOrDefault(league => league.Tier == "bronze");
        var silverLeague = nextLeagues.SingleOrDefault(league => league.Tier == "silver");

        goldLeague.Should().NotBeNull();
        bronzeLeague.Should().NotBeNull();
        silverLeague.Should().NotBeNull();

        var topMembership = await _databaseContext.LeagueMemberships
            .FirstAsync(membership => membership.UserId == topUserId && membership.LeagueId == goldLeague!.Id);
        var bottomMembership = await _databaseContext.LeagueMemberships
            .FirstAsync(membership => membership.UserId == bottomUserId && membership.LeagueId == bronzeLeague!.Id);
        var middleMembership = await _databaseContext.LeagueMemberships
            .FirstAsync(membership => membership.UserId == middleUserId && membership.LeagueId == silverLeague!.Id);

        topMembership.Should().NotBeNull();
        bottomMembership.Should().NotBeNull();
        middleMembership.Should().NotBeNull();
    }

    [Test]
    public async Task CloseCurrentLeagueAndCreateNextAsync_AdvancesPeriodSchedule()
    {
        await SeedTiersAsync();
        var weekStart = new DateOnly(2026, 6, 1);
        var weekEnd = new DateOnly(2026, 6, 7);
        _databaseContext.LeagueSettings.Add(new LeagueSettings
        {
            Id = Guid.NewGuid(),
            CurrentPeriodStartDate = weekStart,
            CurrentPeriodEndsAt = new DateTimeOffset(weekEnd.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc)),
            PeriodLengthDays = 7,
        });
        await _databaseContext.SaveChangesAsync();

        await _leagueService.CloseCurrentLeagueAndCreateNextAsync();

        var settings = await _databaseContext.LeagueSettings.FirstAsync();
        settings.CurrentPeriodStartDate.Should().Be(weekEnd.AddDays(1));
    }
}
