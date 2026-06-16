using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.SkillTree.Models;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Integration;

[TestFixture]
public class AdminSkillStagesTests
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
            email: $"admin_{Guid.NewGuid()}@test.com", role: UserRole.Admin);
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"user_{Guid.NewGuid()}@test.com", role: UserRole.User);

        _adminClient = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            admin.Id, admin.Email, admin.DisplayName, UserRole.Admin);
        _userClient = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName, UserRole.User);
    }

    [TearDown]
    public void TearDown() => _scope.Dispose();

    private async Task<SkillStage> SeedStageAsync(string key, int order = 1)
    {
        var stage = new SkillStage
        {
            Id = Guid.NewGuid(),
            Key = key,
            Label = $"Label {key}",
            Accent = "#123456",
            Order = order
        };
        _db.SkillStages.Add(stage);
        await _db.SaveChangesAsync();
        return stage;
    }

    [Test]
    public async Task GetAll_AsAdmin_Returns200WithList()
    {
        await SeedStageAsync($"stage-{Guid.NewGuid():N}");

        var response = await _adminClient.GetAsync("/admin/skill-stages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        list.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task GetAll_AsRegularUser_Returns403()
    {
        var response = await _userClient.GetAsync("/admin/skill-stages");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Create_AsAdmin_PersistsStageAndLowercasesKey()
    {
        var key = $"NEW-{Guid.NewGuid():N}";
        var response = await _adminClient.PostAsJsonAsync("/admin/skill-stages",
            new { key, label = "Переговоры", accent = "#FF0000", order = 7 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("key").GetString().Should().Be(key.ToLowerInvariant());
        body.GetProperty("label").GetString().Should().Be("Переговоры");
        body.GetProperty("order").GetInt32().Should().Be(7);

        var stored = await _db.SkillStages.FirstOrDefaultAsync(s => s.Key == key.ToLowerInvariant());
        stored.Should().NotBeNull();
    }

    [Test]
    public async Task Create_DuplicateKey_Returns400()
    {
        var key = $"dup-{Guid.NewGuid():N}";
        await SeedStageAsync(key);

        var response = await _adminClient.PostAsJsonAsync("/admin/skill-stages",
            new { key, label = "x", accent = "#000000", order = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Create_MissingLabel_Returns400()
    {
        var response = await _adminClient.PostAsJsonAsync("/admin/skill-stages",
            new { key = $"k-{Guid.NewGuid():N}", label = "", accent = "#000000", order = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Update_AsAdmin_ChangesLabelAccentOrderButNotKey()
    {
        var stage = await SeedStageAsync($"upd-{Guid.NewGuid():N}", order: 2);

        var response = await _adminClient.PutAsJsonAsync($"/admin/skill-stages/{stage.Id}",
            new { label = "Updated", accent = "#abcdef", order = 9 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("key").GetString().Should().Be(stage.Key);
        body.GetProperty("label").GetString().Should().Be("Updated");
        body.GetProperty("accent").GetString().Should().Be("#abcdef");
        body.GetProperty("order").GetInt32().Should().Be(9);
    }

    [Test]
    public async Task Update_UnknownId_Returns404()
    {
        var response = await _adminClient.PutAsJsonAsync($"/admin/skill-stages/{Guid.NewGuid()}",
            new { label = "x", accent = "#000000", order = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Delete_StageWithAssignedSkills_Returns400()
    {
        var key = $"used-{Guid.NewGuid():N}";
        var stage = await SeedStageAsync(key);
        var skill = await TestDbSeeder.SeedSkillAsync(_db, iconicName: $"sk-{Guid.NewGuid():N}");
        skill.Stage = key;
        await _db.SaveChangesAsync();

        var response = await _adminClient.DeleteAsync($"/admin/skill-stages/{stage.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Delete_UnusedStage_Returns204()
    {
        var stage = await SeedStageAsync($"free-{Guid.NewGuid():N}");

        var response = await _adminClient.DeleteAsync($"/admin/skill-stages/{stage.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task PublicStages_AsUser_Returns200OrderedByOrder()
    {
        await SeedStageAsync($"pub-b-{Guid.NewGuid():N}", order: 5);
        await SeedStageAsync($"pub-a-{Guid.NewGuid():N}", order: 1);

        var response = await _userClient.GetAsync("/skills/stages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<SkillStageDto>>();
        list.Should().NotBeNull();
        list!.Select(s => s.Order).Should().BeInAscendingOrder();
    }
}
