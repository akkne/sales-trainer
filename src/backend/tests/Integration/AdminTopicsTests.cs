using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Integration;

[TestFixture]
public class AdminTopicsTests
{
    private AppDbContext _db = null!;
    private IServiceScope _scope = null!;
    private HttpClient _adminClient = null!;

    [SetUp]
    public async Task SetUp()
    {
        _scope = IntegrationTestSetup.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var admin = await TestDbSeeder.SeedUserAsync(_db,
            email: $"admin_{Guid.NewGuid()}@test.com", role: UserRole.Admin);
        _adminClient = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            admin.Id, admin.Email, admin.DisplayName, UserRole.Admin);
    }

    [TearDown]
    public void TearDown() => _scope.Dispose();

    // Regression: the frontend updates topics via PUT /admin/topics/{id:guid}.
    // That route was missing on the backend, so every topic edit returned 404.
    [Test]
    public async Task UpdateById_AsAdmin_PersistsChanges()
    {
        var skill = await TestDbSeeder.SeedSkillAsync(_db, iconicName: $"sk-{Guid.NewGuid()}");
        var topic = await TestDbSeeder.SeedTopicAsync(_db, skill.Id,
            iconicName: $"tp-{Guid.NewGuid()}", title: "Old", orderInSkill: 1);

        var response = await _adminClient.PutAsJsonAsync($"/admin/topics/{topic.Id}", new
        {
            title = "New Title",
            orderInSkill = 5
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("New Title");
        body.GetProperty("orderInSkill").GetInt32().Should().Be(5);

        var stored = await _db.Topics.AsNoTracking().FirstAsync(t => t.Id == topic.Id);
        stored.Title.Should().Be("New Title");
        stored.OrderInSkill.Should().Be(5);
    }

    [Test]
    public async Task UpdateById_NonExistent_Returns404()
    {
        var response = await _adminClient.PutAsJsonAsync($"/admin/topics/{Guid.NewGuid()}", new
        {
            title = "X"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateById_DuplicateIconicName_Returns409()
    {
        var skill = await TestDbSeeder.SeedSkillAsync(_db, iconicName: $"sk-{Guid.NewGuid()}");
        var taken = $"taken-{Guid.NewGuid()}";
        await TestDbSeeder.SeedTopicAsync(_db, skill.Id, iconicName: taken);
        var topic = await TestDbSeeder.SeedTopicAsync(_db, skill.Id,
            iconicName: $"tp-{Guid.NewGuid()}");

        var response = await _adminClient.PutAsJsonAsync($"/admin/topics/{topic.Id}", new
        {
            iconicName = taken
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
