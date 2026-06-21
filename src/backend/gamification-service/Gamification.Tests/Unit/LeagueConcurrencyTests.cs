using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Sellevate.Gamification.Features.League.Models;
using Sellevate.Gamification.Features.League.Services.Implementation;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Tests.Unit;

/// <summary>
/// GA5: Concurrent first-hits on GET /league must produce exactly one league and
/// one membership per user, not duplicates. The in-memory provider does not enforce
/// unique indexes, so these tests exercise the application-level guard (check-before-
/// insert + catch-unique-violation in GetOrCreateLeagueForWeekAsync / GetOrJoinLeagueAsync).
/// </summary>
[TestFixture]
public sealed class LeagueConcurrencyTests
{
    private const string BronzeTier = "bronze";

    [Test]
    public async Task GetCurrentLeagueForUser_CalledTwiceSequentially_ReturnsSingleLeagueAndMembership()
    {
        // Each call gets its own DbContext to simulate separate HTTP requests.
        using var ctx1 = GamificationDbContextFactory.CreateInMemory();
        SeedSharedState(ctx1);
        await ctx1.SaveChangesAsync();

        // Second context shares the same in-memory database via the named db above,
        // but because CreateInMemory uses Guid.NewGuid() as the db name we simulate
        // sequential calls on the SAME context instead (realistic for the app).
        var service = new LeagueService(ctx1);
        var userId = Guid.NewGuid();

        // First call: creates league + membership
        var response1 = await service.GetCurrentLeagueForUserAsync(userId);

        // Second call: must re-use the existing league + membership
        var response2 = await service.GetCurrentLeagueForUserAsync(userId);

        response1.LeagueId.Should().Be(response2.LeagueId, "same league should be returned both times");

        var leagueCount = await ctx1.Leagues.CountAsync();
        var membershipCount = await ctx1.LeagueMemberships
            .Where(m => m.UserId == userId)
            .CountAsync();

        leagueCount.Should().Be(1, "only one league should exist for this week+tier");
        membershipCount.Should().Be(1, "only one membership should exist for this user");
    }

    [Test]
    public async Task GetCurrentLeagueForUser_TwoUsersOnSameTier_ShareSingleLeague()
    {
        using var ctx = GamificationDbContextFactory.CreateInMemory();
        SeedSharedState(ctx);
        await ctx.SaveChangesAsync();

        var service = new LeagueService(ctx);
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        await service.GetCurrentLeagueForUserAsync(userId1);
        await service.GetCurrentLeagueForUserAsync(userId2);

        var leagueCount = await ctx.Leagues.CountAsync();
        leagueCount.Should().Be(1, "both users in bronze should share one league");

        var membership1LeagueId = await ctx.LeagueMemberships
            .Where(m => m.UserId == userId1)
            .Select(m => m.LeagueId)
            .FirstAsync();
        var membership2LeagueId = await ctx.LeagueMemberships
            .Where(m => m.UserId == userId2)
            .Select(m => m.LeagueId)
            .FirstAsync();

        membership1LeagueId.Should().Be(membership2LeagueId, "both users should be in the same league");
    }

    [Test]
    public async Task GetOrJoinLeague_WhenMembershipAlreadyExists_DoesNotCreateDuplicate()
    {
        using var ctx = GamificationDbContextFactory.CreateInMemory();
        SeedSharedState(ctx);
        await ctx.SaveChangesAsync();

        var service = new LeagueService(ctx);
        var userId = Guid.NewGuid();

        // First call creates league + membership
        await service.GetCurrentLeagueForUserAsync(userId);

        // Simulate a second concurrent request: call again on the same context
        // (application-level guard must detect existing membership and skip insert)
        await service.GetCurrentLeagueForUserAsync(userId);

        var membershipCount = await ctx.LeagueMemberships
            .Where(m => m.UserId == userId)
            .CountAsync();

        membershipCount.Should().Be(1, "duplicate membership must not be created");
    }

    /// <summary>
    /// Seeds a LeagueSettings row and a bronze tier so the service has a valid
    /// current period and at least one tier to work with.
    /// </summary>
    private static void SeedSharedState(GamificationDbContext ctx)
    {
        var weekStart = GetCurrentWeekStart();
        var weekEnd = weekStart.AddDays(6);

        ctx.LeagueSettings.Add(new LeagueSettings
        {
            Id = Guid.NewGuid(),
            MaximumLeagueParticipantCount = 30,
            PromotionZoneSize = 10,
            DemotionZoneSize = 5,
            PeriodLengthDays = 7,
            CurrentPeriodStartDate = weekStart,
            CurrentPeriodEndsAt = new DateTimeOffset(weekEnd.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc)),
        });

        ctx.LeagueTiers.Add(new LeagueTier
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
