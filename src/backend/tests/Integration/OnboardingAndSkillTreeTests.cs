using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Features.Gamification;
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
    public async Task Onboarding_CreatesSkillProgressForApplicableTypes()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"ob_ap_{Guid.NewGuid()}@test.com");

        var enterpriseSkill = await TestDbSeeder.SeedSkillAsync(_db,
            slug: $"ent-{Guid.NewGuid()}",
            sortOrder: 1,
            applicableSalesTypes: ["enterprise"]);
        var smbSkill = await TestDbSeeder.SeedSkillAsync(_db,
            slug: $"smb-{Guid.NewGuid()}",
            sortOrder: 2,
            applicableSalesTypes: ["smb"]);

        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        await client.PostAsJsonAsync("/onboarding", new
        {
            salesType = "enterprise",
            experienceLevel = "beginner",
            goal = "close deals"
        });

        var enterpriseProgress = _db.UserSkillProgressRecords
            .FirstOrDefault(p => p.UserId == user.Id && p.SkillId == enterpriseSkill.Id);
        var smbProgress = _db.UserSkillProgressRecords
            .FirstOrDefault(p => p.UserId == user.Id && p.SkillId == smbSkill.Id);

        enterpriseProgress.Should().NotBeNull();
        smbProgress.Should().BeNull();
    }

    [Test]
    public async Task Onboarding_FirstSkillIsAvailable_RestLocked()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"ob_fs_{Guid.NewGuid()}@test.com");

        // Use a unique sales type so no other seeds interfere
        var uniqueType = $"type-{Guid.NewGuid():N}";

        var skill1 = await TestDbSeeder.SeedSkillAsync(_db,
            slug: $"first-{Guid.NewGuid()}",
            sortOrder: 10,
            applicableSalesTypes: [uniqueType]);
        var skill2 = await TestDbSeeder.SeedSkillAsync(_db,
            slug: $"second-{Guid.NewGuid()}",
            sortOrder: 11,
            applicableSalesTypes: [uniqueType]);
        var skill3 = await TestDbSeeder.SeedSkillAsync(_db,
            slug: $"third-{Guid.NewGuid()}",
            sortOrder: 12,
            applicableSalesTypes: [uniqueType]);

        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        await client.PostAsJsonAsync("/onboarding", new
        {
            salesType = uniqueType,
            experienceLevel = "intermediate",
            goal = "upsell"
        });

        var p1 = _db.UserSkillProgressRecords
            .First(p => p.UserId == user.Id && p.SkillId == skill1.Id);
        var p2 = _db.UserSkillProgressRecords
            .First(p => p.UserId == user.Id && p.SkillId == skill2.Id);
        var p3 = _db.UserSkillProgressRecords
            .First(p => p.UserId == user.Id && p.SkillId == skill3.Id);

        p1.Status.Should().Be("available");
        p2.Status.Should().Be("locked");
        p3.Status.Should().Be("locked");
    }

    [Test]
    public async Task Onboarding_AlreadyCompleted_IsIdempotent()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"ob_id_{Guid.NewGuid()}@test.com");
        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        await client.PostAsJsonAsync("/onboarding", new
        {
            salesType = "enterprise",
            experienceLevel = "beginner",
            goal = "first goal"
        });

        var response = await client.PostAsJsonAsync("/onboarding", new
        {
            salesType = "smb",
            experienceLevel = "expert",
            goal = "second goal"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var profile = _db.UserProfiles.First(p => p.UserId == user.Id);
        profile.SalesType.Should().Be("enterprise");
    }

    [Test]
    public async Task SkillTree_ValidToken_ReturnsNodes()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"st_{Guid.NewGuid()}@test.com");
        var skill = await TestDbSeeder.SeedSkillAsync(_db, slug: $"st-skill-{Guid.NewGuid()}");
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
