using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Integration;

[TestFixture]
public class AdminSkillsTests
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

    [Test]
    public async Task GetAll_AsAdmin_Returns200WithList()
    {
        await TestDbSeeder.SeedSkillAsync(_db, slug: $"slug-{Guid.NewGuid()}");

        var response = await _adminClient.GetAsync("/admin/skills");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        list.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task GetAll_AsRegularUser_Returns403()
    {
        var response = await _userClient.GetAsync("/admin/skills");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Create_AsAdmin_Returns200WithCreatedSkill()
    {
        var slug = $"new-skill-{Guid.NewGuid()}";

        var response = await _adminClient.PostAsJsonAsync("/admin/skills", new
        {
            title = "New Skill",
            slug,
            iconName = "star",
            sortOrder = 99,
            prerequisiteSkillId = (Guid?)null,
            applicableSalesTypes = new[] { "enterprise" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("slug").GetString().Should().Be(slug);
        body.GetProperty("title").GetString().Should().Be("New Skill");
    }

    [Test]
    public async Task Update_AsAdmin_Returns200WithUpdatedData()
    {
        var skill = await TestDbSeeder.SeedSkillAsync(_db, slug: $"upd-{Guid.NewGuid()}");

        var response = await _adminClient.PutAsJsonAsync($"/admin/skills/{skill.Id}", new
        {
            title = "Updated Title",
            slug = skill.Slug,
            iconName = "pencil",
            sortOrder = skill.SortOrder,
            prerequisiteSkillId = (Guid?)null,
            applicableSalesTypes = new[] { "smb" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("Updated Title");
    }

    [Test]
    public async Task Update_NonExistentSkill_Returns404()
    {
        var response = await _adminClient.PutAsJsonAsync($"/admin/skills/{Guid.NewGuid()}", new
        {
            title = "X",
            slug = "x",
            iconName = "x",
            sortOrder = 1,
            prerequisiteSkillId = (Guid?)null,
            applicableSalesTypes = new[] { "enterprise" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Delete_AsAdmin_Returns204()
    {
        var skill = await TestDbSeeder.SeedSkillAsync(_db, slug: $"del-{Guid.NewGuid()}");

        var response = await _adminClient.DeleteAsync($"/admin/skills/{skill.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Delete_NonExistentSkill_Returns404()
    {
        var response = await _adminClient.DeleteAsync($"/admin/skills/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
