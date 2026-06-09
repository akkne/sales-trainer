using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.League.Models;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Integration;

[TestFixture]
public class AdminLeaguesTests
{
    private AppDbContext _db = null!;
    private IServiceScope _scope = null!;
    private HttpClient _adminClient = null!;
    private HttpClient _userClient = null!;

    [SetUp]
    public async Task SetUp()
    {
        _scope = IntegrationTestSetup.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var admin = await TestDbSeeder.SeedUserAsync(_db,
            email: $"adm_{Guid.NewGuid()}@test.com", role: UserRole.Admin);
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"usr_{Guid.NewGuid()}@test.com", role: UserRole.User);

        _adminClient = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            admin.Id, admin.Email, admin.DisplayName, UserRole.Admin);
        _userClient = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName, UserRole.User);
    }

    [TearDown]
    public void TearDown() => _scope.Dispose();

    private static DateOnly CurrentWeekStart()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-daysFromMonday);
    }

    private async Task<(League league, LeagueMembership membership)> SeedLeagueWithMemberAsync(
        string tier = "bronze")
    {
        var weekStart = CurrentWeekStart();
        var league = new League
        {
            Id = Guid.NewGuid(),
            Tier = tier,
            WeekStartDate = weekStart,
            WeekEndDate = weekStart.AddDays(6)
        };
        _db.Leagues.Add(league);

        var member = await TestDbSeeder.SeedUserAsync(_db,
            email: $"member_{Guid.NewGuid()}@test.com");

        var membership = new LeagueMembership
        {
            Id = Guid.NewGuid(),
            UserId = member.Id,
            LeagueId = league.Id,
            WeeklyXpAmount = 0,
            Rank = 0
        };
        _db.LeagueMemberships.Add(membership);
        await _db.SaveChangesAsync();

        return (league, membership);
    }

    [Test]
    public async Task GetLeagues_AsAdmin_Returns200()
    {
        await SeedLeagueWithMemberAsync();

        var response = await _adminClient.GetAsync("/admin/leagues");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        list.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task GetLeagues_AsUser_Returns403()
    {
        var response = await _userClient.GetAsync("/admin/leagues");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetLeagueDetail_ReturnsMembersWithUserInfo()
    {
        var (league, membership) = await SeedLeagueWithMemberAsync();

        var response = await _adminClient.GetAsync($"/admin/leagues/{league.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var members = body.GetProperty("members");
        members.GetArrayLength().Should().Be(1);
        members[0].GetProperty("membershipId").GetGuid().Should().Be(membership.Id);
        members[0].GetProperty("email").GetString().Should().Contain("@test.com");
    }

    [Test]
    public async Task AdjustXp_CreatesCorrectionRecordAndSurvivesResync()
    {
        var (league, membership) = await SeedLeagueWithMemberAsync();

        var response = await _adminClient.PutAsJsonAsync(
            $"/admin/leagues/memberships/{membership.Id}/xp",
            new { delta = 75 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var correction = await _db.UserXpRecords
            .FirstOrDefaultAsync(r => r.UserId == membership.UserId && r.Source == "admin_correction");
        correction.Should().NotBeNull();
        correction!.Amount.Should().Be(75);

        // Re-sync must not erase the adjustment.
        var resyncResponse = await _adminClient.PostAsync($"/admin/leagues/{league.Id}/resync", null);
        resyncResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resyncResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("members")[0].GetProperty("weeklyXpAmount").GetInt32().Should().Be(75);
    }

    [Test]
    public async Task AdjustXp_NonExistentMembership_Returns404()
    {
        var response = await _adminClient.PutAsJsonAsync(
            $"/admin/leagues/memberships/{Guid.NewGuid()}/xp",
            new { delta = 10 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task MoveTier_MovesToSameWeekLeague_CreatingIfMissing()
    {
        var (_, membership) = await SeedLeagueWithMemberAsync();

        var response = await _adminClient.PutAsJsonAsync(
            $"/admin/leagues/memberships/{membership.Id}/tier",
            new { tier = "gold" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("tier").GetString().Should().Be("gold");
        body.GetProperty("weekStartDate").GetString().Should().Be(
            CurrentWeekStart().ToString("yyyy-MM-dd"));
        body.GetProperty("members").GetArrayLength().Should().Be(1);
    }

    [Test]
    public async Task MoveTier_ClearsStalePromotionOutcome()
    {
        var (_, membership) = await SeedLeagueWithMemberAsync();
        membership.PromotionOutcome = "promoted";
        await _db.SaveChangesAsync();

        var response = await _adminClient.PutAsJsonAsync(
            $"/admin/leagues/memberships/{membership.Id}/tier",
            new { tier = "silver" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var moved = await _db.LeagueMemberships.AsNoTracking()
            .FirstAsync(m => m.Id == membership.Id);
        moved.PromotionOutcome.Should().BeNull();
    }

    [Test]
    public async Task MoveTier_InvalidTier_Returns400()
    {
        var (_, membership) = await SeedLeagueWithMemberAsync();

        var response = await _adminClient.PutAsJsonAsync(
            $"/admin/leagues/memberships/{membership.Id}/tier",
            new { tier = "platinum" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task RemoveMembership_Returns204AndDeletesRow()
    {
        var (_, membership) = await SeedLeagueWithMemberAsync();

        var response = await _adminClient.DeleteAsync(
            $"/admin/leagues/memberships/{membership.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _db.LeagueMemberships.AsNoTracking()
            .AnyAsync(m => m.Id == membership.Id)).Should().BeFalse();
    }

    [Test]
    public async Task CloseCurrent_Returns204()
    {
        await SeedLeagueWithMemberAsync();

        var response = await _adminClient.PostAsync("/admin/leagues/close-current", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task UpdateSettings_PersistsValues()
    {
        var response = await _adminClient.PutAsJsonAsync("/admin/leagues/settings",
            new { maximumLeagueParticipantCount = 40, promotionZoneSize = 8, demotionZoneSize = 4 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _adminClient.GetAsync("/admin/leagues/settings");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("maximumLeagueParticipantCount").GetInt32().Should().Be(40);
        body.GetProperty("promotionZoneSize").GetInt32().Should().Be(8);
        body.GetProperty("demotionZoneSize").GetInt32().Should().Be(4);
    }

    [Test]
    public async Task UpdateSettings_ZonesExceedMax_Returns400()
    {
        var response = await _adminClient.PutAsJsonAsync("/admin/leagues/settings",
            new { maximumLeagueParticipantCount = 10, promotionZoneSize = 8, demotionZoneSize = 4 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
