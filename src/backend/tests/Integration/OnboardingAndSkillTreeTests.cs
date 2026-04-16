using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Features.Gamification.Models;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Integration;

[TestFixture]
public class OnboardingAndSkillTreeTests
{
    private AppDbContext _db = null!;
    private IServiceScope _scope = null!;

    [SetUp]
    public void SetUp()
    {
        _scope = IntegrationTestSetup.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    [TearDown]
    public void TearDown() => _scope.Dispose();

    [Test]
    public async Task Onboarding_ValidToken_Returns204()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"ob_{Guid.NewGuid()}@test.com");
        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        var response = await client.PostAsJsonAsync("/onboarding", new
        {
            salesType = "enterprise",
            experienceLevel = "beginner",
            goal = "close more deals"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task SkillTree_ValidToken_ReturnsNodes()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"st_{Guid.NewGuid()}@test.com");
        var skill = await TestDbSeeder.SeedSkillAsync(_db, iconicName: $"st-skill-{Guid.NewGuid()}");
        await TestDbSeeder.SeedSkillProgressAsync(_db, user.Id, skill.Id, status: "available");
        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        var response = await client.GetAsync("/skill-tree");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("skillNodes").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task SkillTree_AggregatesXpAndStreak()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"st_xp_{Guid.NewGuid()}@test.com");

        _db.UserXpRecords.Add(new UserXp
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Amount = 150,
            Source = "exercise",
            EarnedAt = DateTime.UtcNow
        });
        _db.UserStreaks.Add(new UserStreak
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CurrentStreakDayCount = 7,
            LongestStreakDayCount = 7,
            LastActivityDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        await _db.SaveChangesAsync();

        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        var response = await client.GetAsync("/skill-tree");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("totalXpAmount").GetInt32().Should().Be(150);
        body.GetProperty("currentStreakDayCount").GetInt32().Should().Be(7);
        body.GetProperty("weeklyXpAmount").GetInt32().Should().Be(150);
    }

    [Test]
    public async Task SkillTree_NoToken_Returns401()
    {
        var client = IntegrationTestSetup.Factory.CreateClient();
        var response = await client.GetAsync("/skill-tree");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
