using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Integration;

[TestFixture]
public class AdminGamificationTests
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

    [Test]
    public async Task GetSettings_AsUser_IsForbidden()
    {
        var response = await _userClient.GetAsync("/admin/gamification/settings");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetSettings_AsAdmin_ReturnsConfiguredValues()
    {
        // The settings row is a shared singleton across the integration DB, so this only
        // asserts the contract shape and sane values (another test may have mutated it).
        var response = await _adminClient.GetAsync("/admin/gamification/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("dailyXpGoal").GetInt32().Should().BeGreaterThan(0);
        body.GetProperty("weeklyXpGoal").GetInt32().Should().BeGreaterThan(0);
        body.GetProperty("dialogXpMultiplier").GetDouble().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task UpdateSettings_AsAdmin_PersistsValues()
    {
        var payload = new
        {
            dailyXpGoal = 150,
            weeklyXpGoal = 700,
            dialogXpMultiplier = 1.5,
            dialogWeightConfidence = 30,
            dialogWeightStructure = 30,
            dialogWeightObjection = 20,
            dialogWeightGoal = 20
        };

        var response = await _adminClient.PutAsJsonAsync("/admin/gamification/settings", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("dailyXpGoal").GetInt32().Should().Be(150);
        body.GetProperty("dialogXpMultiplier").GetDouble().Should().Be(1.5);
    }

    [Test]
    public async Task UpdateSettings_WithZeroWeightSum_IsRejected()
    {
        var payload = new
        {
            dailyXpGoal = 100, weeklyXpGoal = 500, dialogXpMultiplier = 1.0,
            dialogWeightConfidence = 0, dialogWeightStructure = 0,
            dialogWeightObjection = 0, dialogWeightGoal = 0
        };

        var response = await _adminClient.PutAsJsonAsync("/admin/gamification/settings", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateExerciseReward_AsAdmin_UpsertsValue()
    {
        var response = await _adminClient.PutAsJsonAsync(
            "/admin/gamification/exercise-rewards/choose_option", new { baseXpReward = 35 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("baseXpReward").GetInt32().Should().Be(35);
    }

    [Test]
    public async Task StreakMilestones_FullCrudCycle_Works()
    {
        // Create
        var created = await _adminClient.PostAsJsonAsync(
            "/admin/gamification/streak-milestones", new { dayCount = 14, xpReward = 120 });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdBody.GetProperty("id").GetGuid();

        // Duplicate day count rejected
        var duplicate = await _adminClient.PostAsJsonAsync(
            "/admin/gamification/streak-milestones", new { dayCount = 14, xpReward = 99 });
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Update
        var updated = await _adminClient.PutAsJsonAsync(
            $"/admin/gamification/streak-milestones/{id}", new { dayCount = 14, xpReward = 130 });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedBody = await updated.Content.ReadFromJsonAsync<JsonElement>();
        updatedBody.GetProperty("xpReward").GetInt32().Should().Be(130);

        // Delete
        var deleted = await _adminClient.DeleteAsync($"/admin/gamification/streak-milestones/{id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
