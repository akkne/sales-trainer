using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Sellevate.Gamification.Features.League.Models;
using Sellevate.Gamification.Features.League.Services.Implementation;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Tests.Unit;

/// <summary>
/// Concurrent first-hits on <c>GET /league</c> must produce exactly one league and one membership per
/// user, not duplicates.
///
/// <para>
/// The in-memory provider enforces no unique indexes, so what these tests cover is the
/// <em>application-level</em> half of the guard — the check-before-insert in
/// <c>GetOrCreateLeagueForWeekAsync</c> and <c>GetOrJoinLeagueAsync</c>. The catch-unique-violation
/// half needs a real Postgres and belongs to the integration suite. Each test therefore drives
/// sequential calls on one context, which is what a repeated request actually looks like once the
/// first one has committed.
/// </para>
/// </summary>
[TestFixture]
public sealed class LeagueConcurrencyTests
{
    private const string BronzeTier = "bronze";

    [Test]
    public async Task GetCurrentLeagueForUser_CalledTwiceSequentially_ReturnsSingleLeagueAndMembership()
    {
        using var databaseContext = GamificationDbContextFactory.CreateInMemory();
        SeedSharedState(databaseContext);
        await databaseContext.SaveChangesAsync();

        var service = new LeagueService(databaseContext);
        var userId = Guid.NewGuid();

        var firstResponse = await service.GetCurrentLeagueForUserAsync(userId);
        var secondResponse = await service.GetCurrentLeagueForUserAsync(userId);

        firstResponse.LeagueId.Should().Be(secondResponse.LeagueId, "same league should be returned both times");

        var leagueCount = await databaseContext.Leagues.CountAsync();
        var membershipCount = await databaseContext.LeagueMemberships
            .Where(membership => membership.UserId == userId)
            .CountAsync();

        leagueCount.Should().Be(1, "only one league should exist for this week+tier");
        membershipCount.Should().Be(1, "only one membership should exist for this user");
    }

    [Test]
    public async Task GetCurrentLeagueForUser_TwoUsersOnSameTier_ShareSingleLeague()
    {
        using var databaseContext = GamificationDbContextFactory.CreateInMemory();
        SeedSharedState(databaseContext);
        await databaseContext.SaveChangesAsync();

        var service = new LeagueService(databaseContext);
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        await service.GetCurrentLeagueForUserAsync(userId1);
        await service.GetCurrentLeagueForUserAsync(userId2);

        var leagueCount = await databaseContext.Leagues.CountAsync();
        leagueCount.Should().Be(1, "both users in bronze should share one league");

        var membership1LeagueId = await databaseContext.LeagueMemberships
            .Where(membership => membership.UserId == userId1)
            .Select(membership => membership.LeagueId)
            .FirstAsync();
        var membership2LeagueId = await databaseContext.LeagueMemberships
            .Where(membership => membership.UserId == userId2)
            .Select(membership => membership.LeagueId)
            .FirstAsync();

        membership1LeagueId.Should().Be(membership2LeagueId, "both users should be in the same league");
    }

    [Test]
    public async Task GetOrJoinLeague_WhenMembershipAlreadyExists_DoesNotCreateDuplicate()
    {
        using var databaseContext = GamificationDbContextFactory.CreateInMemory();
        SeedSharedState(databaseContext);
        await databaseContext.SaveChangesAsync();

        var service = new LeagueService(databaseContext);
        var userId = Guid.NewGuid();

        await service.GetCurrentLeagueForUserAsync(userId);
        await service.GetCurrentLeagueForUserAsync(userId);

        var membershipCount = await databaseContext.LeagueMemberships
            .Where(membership => membership.UserId == userId)
            .CountAsync();

        membershipCount.Should().Be(1, "duplicate membership must not be created");
    }

    /// <summary>
    /// Seeds a LeagueSettings row and a bronze tier so the service has a valid current period and at
    /// least one tier to work with.
    /// </summary>
    private static void SeedSharedState(GamificationDbContext databaseContext)
    {
        var weekStart = GetCurrentWeekStart();
        var weekEnd = weekStart.AddDays(6);

        databaseContext.LeagueSettings.Add(new LeagueSettings
        {
            Id = Guid.NewGuid(),
            MaximumLeagueParticipantCount = 30,
            PromotionZoneSize = 10,
            DemotionZoneSize = 5,
            PeriodLengthDays = 7,
            CurrentPeriodStartDate = weekStart,
            CurrentPeriodEndsAt = new DateTimeOffset(weekEnd.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc)),
        });

        databaseContext.LeagueTiers.Add(new LeagueTier
        {
            Id = Guid.NewGuid(),
            Key = BronzeTier,
            Name = "Bronze",
            Color = "#c47b3f",
            Order = 1,
        });
    }

    private static DateOnly GetCurrentWeekStart()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-daysFromMonday);
    }
}
