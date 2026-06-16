using FluentAssertions;
using NUnit.Framework;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Gamification.Models;
using SalesTrainer.Api.Features.League.Models;
using SalesTrainer.Api.Features.League.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class LeagueServiceTests
{
    private AppDbContext _db = null!;
    private LeagueService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _db = InMemoryDbContextFactory.Create();
        _service = new LeagueService(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private DateOnly CurrentWeekStart()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-daysFromMonday);
    }

    private async Task<(League league, List<LeagueMembership> memberships)>
        SeedLeagueWithMembersAsync(int memberCount)
    {
        var weekStart = CurrentWeekStart();
        var league = new League
        {
            Id = Guid.NewGuid(),
            Tier = "bronze",
            WeekStartDate = weekStart,
            WeekEndDate = weekStart.AddDays(6)
        };
        _db.Leagues.Add(league);
        await _db.SaveChangesAsync();

        var memberships = new List<LeagueMembership>();
        for (var i = 0; i < memberCount; i++)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = $"u{i}@test.com",
                DisplayName = $"User {i}",
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(user);

            var membership = new LeagueMembership
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                LeagueId = league.Id,
                WeeklyXpAmount = (memberCount - i) * 10,
                Rank = 0
            };
            _db.LeagueMemberships.Add(membership);
            memberships.Add(membership);
        }
        await _db.SaveChangesAsync();

        return (league, memberships);
    }

    [Test]
    public async Task Close_RanksInDescendingXpOrder()
    {
        var (_, memberships) = await SeedLeagueWithMembersAsync(5);

        await _service.CloseCurrentLeagueAndCreateNextAsync();
        await _db.Entry(memberships[0]).ReloadAsync();

        var sorted = memberships
            .OrderByDescending(m => m.WeeklyXpAmount)
            .ToList();

        sorted[0].Rank.Should().Be(1);
        sorted[4].Rank.Should().Be(5);
    }

    [Test]
    public async Task Close_Top10_GetPromotedOutcome()
    {
        var (_, memberships) = await SeedLeagueWithMembersAsync(20);

        await _service.CloseCurrentLeagueAndCreateNextAsync();
        foreach (var m in memberships) await _db.Entry(m).ReloadAsync();

        var top10 = memberships.OrderByDescending(m => m.WeeklyXpAmount).Take(10).ToList();
        top10.Should().AllSatisfy(m => m.PromotionOutcome.Should().Be("promoted"));
    }

    [Test]
    public async Task Close_Bottom5_GetDemotedOutcome()
    {
        var (_, memberships) = await SeedLeagueWithMembersAsync(20);

        await _service.CloseCurrentLeagueAndCreateNextAsync();
        foreach (var m in memberships) await _db.Entry(m).ReloadAsync();

        var bottom5 = memberships.OrderBy(m => m.WeeklyXpAmount).Take(5).ToList();
        bottom5.Should().AllSatisfy(m => m.PromotionOutcome.Should().Be("demoted"));
    }

    [Test]
    public async Task Close_MiddleMembers_HaveNullPromotionOutcome()
    {
        var (_, memberships) = await SeedLeagueWithMembersAsync(20);

        await _service.CloseCurrentLeagueAndCreateNextAsync();
        foreach (var m in memberships) await _db.Entry(m).ReloadAsync();

        var middle = memberships
            .OrderByDescending(m => m.WeeklyXpAmount)
            .Skip(10)
            .Take(5)
            .ToList();

        middle.Should().AllSatisfy(m => m.PromotionOutcome.Should().BeNull());
    }

    [Test]
    public async Task Close_CreatesNewLeagueForNextWeek()
    {
        await SeedLeagueWithMembersAsync(5);
        var weekStart = CurrentWeekStart();

        await _service.CloseCurrentLeagueAndCreateNextAsync();

        var nextLeague = _db.Leagues.FirstOrDefault(
            l => l.WeekStartDate == weekStart.AddDays(7));
        nextLeague.Should().NotBeNull();
    }

    [Test]
    public async Task Close_RespectsConfiguredPromotionAndDemotionZones()
    {
        _db.LeagueSettings.Add(new LeagueSettings
        {
            MaximumLeagueParticipantCount = 30,
            PromotionZoneSize = 3,
            DemotionZoneSize = 2
        });
        await _db.SaveChangesAsync();

        var (_, memberships) = await SeedLeagueWithMembersAsync(10);

        await _service.CloseCurrentLeagueAndCreateNextAsync();
        foreach (var m in memberships) await _db.Entry(m).ReloadAsync();

        var sorted = memberships.OrderByDescending(m => m.WeeklyXpAmount).ToList();

        sorted.Take(3).Should().AllSatisfy(m => m.PromotionOutcome.Should().Be("promoted"));
        sorted.Skip(3).Take(5).Should().AllSatisfy(m => m.PromotionOutcome.Should().BeNull());
        sorted.Skip(8).Should().AllSatisfy(m => m.PromotionOutcome.Should().Be("demoted"));
    }

    [Test]
    public async Task SyncLeagueWeeklyXp_RecomputesFromXpRecords()
    {
        var (league, memberships) = await SeedLeagueWithMembersAsync(2);
        var member = memberships[0];

        _db.UserXpRecords.Add(new UserXp
        {
            Id = Guid.NewGuid(),
            UserId = member.UserId,
            Amount = 125,
            Source = "admin_correction",
            EarnedAt = league.WeekStartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        });
        await _db.SaveChangesAsync();

        await _service.SyncLeagueWeeklyXpAsync(league.Id);
        await _db.Entry(member).ReloadAsync();

        member.WeeklyXpAmount.Should().Be(125);
    }

    [Test]
    public async Task Close_RunTwice_DoesNotDuplicateNextWeekMemberships()
    {
        await SeedLeagueWithMembersAsync(3);
        var nextWeekStart = CurrentWeekStart().AddDays(7);

        await _service.CloseCurrentLeagueAndCreateNextAsync();
        await _service.CloseCurrentLeagueAndCreateNextAsync();

        var nextWeekMembershipCount = _db.LeagueMemberships
            .Join(_db.Leagues, m => m.LeagueId, l => l.Id, (m, l) => l.WeekStartDate)
            .Count(weekStart => weekStart == nextWeekStart);

        nextWeekMembershipCount.Should().Be(3);
    }

    [Test]
    public async Task GetCurrent_UserWithNoHistory_StartsBronze_IgnoringOtherUsers()
    {
        // A different user has a previous-week "promoted" bronze membership. The
        // tier of the user we query must not be derived from someone else's history.
        var previousWeekStart = CurrentWeekStart().AddDays(-7);
        var otherLeague = new League
        {
            Id = Guid.NewGuid(),
            Tier = "bronze",
            WeekStartDate = previousWeekStart,
            WeekEndDate = previousWeekStart.AddDays(6)
        };
        _db.Leagues.Add(otherLeague);
        var otherUser = new User { Id = Guid.NewGuid(), Email = "other@test.com", DisplayName = "Other", CreatedAt = DateTime.UtcNow };
        _db.Users.Add(otherUser);
        _db.LeagueMemberships.Add(new LeagueMembership
        {
            Id = Guid.NewGuid(),
            UserId = otherUser.Id,
            LeagueId = otherLeague.Id,
            WeeklyXpAmount = 100,
            Rank = 1,
            PromotionOutcome = "promoted"
        });

        var freshUserId = Guid.NewGuid();
        _db.Users.Add(new User { Id = freshUserId, Email = "fresh@test.com", DisplayName = "Fresh", CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var result = await _service.GetCurrentLeagueForUserAsync(freshUserId);

        result.Tier.Should().Be("bronze");
    }

    [Test]
    public async Task GetSettings_InitializesCurrentPeriod()
    {
        var settings = await _service.GetSettingsAsync();

        settings.CurrentPeriodStartDate.Should().Be(CurrentWeekStart());
        settings.CurrentPeriodEndsAt.Should().NotBeNull();
        settings.PeriodLengthDays.Should().Be(7);
    }

    [Test]
    public async Task RolloverIfDue_PeriodNotEnded_DoesNotAdvance()
    {
        var settings = await _service.GetSettingsAsync();
        settings.CurrentPeriodEndsAt = DateTimeOffset.UtcNow.AddDays(3);
        await _db.SaveChangesAsync();
        var startBefore = settings.CurrentPeriodStartDate;

        await _service.RolloverIfDueAsync();
        await _db.Entry(settings).ReloadAsync();

        settings.CurrentPeriodStartDate.Should().Be(startBefore);
    }

    [Test]
    public async Task RolloverIfDue_PeriodEnded_RanksMembersAndAdvancesPeriod()
    {
        var (_, memberships) = await SeedLeagueWithMembersAsync(5);
        var settings = await _service.GetSettingsAsync();
        var startBefore = settings.CurrentPeriodStartDate!.Value;
        settings.CurrentPeriodEndsAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await _db.SaveChangesAsync();

        await _service.RolloverIfDueAsync();
        await _db.Entry(settings).ReloadAsync();
        foreach (var m in memberships) await _db.Entry(m).ReloadAsync();

        settings.CurrentPeriodStartDate!.Value.Should().BeAfter(startBefore);
        memberships.OrderByDescending(m => m.WeeklyXpAmount).First().Rank.Should().Be(1);
    }

    [Test]
    public async Task GetCurrent_ReturnsConfiguredTierNameColorAndPeriodEnd()
    {
        _db.LeagueTiers.Add(new LeagueTier { Key = "bronze", Name = "Бронза", Color = "#c47b3f", Order = 1 });
        var userId = Guid.NewGuid();
        _db.Users.Add(new User { Id = userId, Email = "tier@test.com", DisplayName = "Tier", CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var result = await _service.GetCurrentLeagueForUserAsync(userId);

        result.Tier.Should().Be("bronze");
        result.TierName.Should().Be("Бронза");
        result.TierColor.Should().Be("#c47b3f");
        result.PeriodEndsAt.Should().NotBe(default);
    }

    [Test]
    public async Task Close_UsesConfiguredTierLadderForPromotion()
    {
        _db.LeagueTiers.AddRange(
            new LeagueTier { Key = "rookie", Name = "Rookie", Color = "#111111", Order = 1 },
            new LeagueTier { Key = "pro", Name = "Pro", Color = "#222222", Order = 2 });
        _db.LeagueSettings.Add(new LeagueSettings
        {
            MaximumLeagueParticipantCount = 30,
            PromotionZoneSize = 1,
            DemotionZoneSize = 1
        });
        await _db.SaveChangesAsync();

        var weekStart = CurrentWeekStart();
        var league = new League
        {
            Id = Guid.NewGuid(),
            Tier = "rookie",
            WeekStartDate = weekStart,
            WeekEndDate = weekStart.AddDays(6)
        };
        _db.Leagues.Add(league);
        for (var i = 0; i < 3; i++)
        {
            var user = new User { Id = Guid.NewGuid(), Email = $"l{i}@test.com", DisplayName = $"L{i}", CreatedAt = DateTime.UtcNow };
            _db.Users.Add(user);
            _db.LeagueMemberships.Add(new LeagueMembership
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                LeagueId = league.Id,
                WeeklyXpAmount = (3 - i) * 10,
                Rank = 0
            });
        }
        await _db.SaveChangesAsync();

        await _service.CloseCurrentLeagueAndCreateNextAsync();

        var nextStart = weekStart.AddDays(7);
        _db.Leagues.FirstOrDefault(l => l.WeekStartDate == nextStart && l.Tier == "pro")
            .Should().NotBeNull("the top member is promoted to the next-order tier from the DB ladder");
    }

    [Test]
    public async Task GetCurrent_NoLeagueExists_CreatesAndJoinsUser()
    {
        var userId = Guid.NewGuid();
        _db.Users.Add(new User
        {
            Id = userId,
            Email = "new@test.com",
            DisplayName = "New",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetCurrentLeagueForUserAsync(userId);

        result.Should().NotBeNull();
        result.Tier.Should().Be("bronze");
        result.ParticipantsByRank.Should().Contain(p => p.IsCurrentUser);

        var league = _db.Leagues.FirstOrDefault(l => l.WeekStartDate == CurrentWeekStart());
        league.Should().NotBeNull();
    }
}
