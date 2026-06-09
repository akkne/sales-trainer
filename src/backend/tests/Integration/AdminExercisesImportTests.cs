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
public class AdminExercisesImportTests
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

    private async Task<Guid> SeedLessonAsync()
    {
        var skill = await TestDbSeeder.SeedSkillAsync(_db, iconicName: $"sk-{Guid.NewGuid()}");
        var topic = await TestDbSeeder.SeedTopicAsync(_db, skill.Id, iconicName: $"tp-{Guid.NewGuid()}");
        var lesson = await TestDbSeeder.SeedLessonAsync(_db, topic.Id);
        return lesson.Id;
    }

    [Test]
    public async Task Import_AcceptsArray_CreatesAndUpdatesByOrder()
    {
        var lessonId = await SeedLessonAsync();
        // Pre-existing exercise at order 1 should be updated, order 2 created.
        await TestDbSeeder.SeedMultipleChoiceExerciseAsync(_db, lessonId, orderInLesson: 1);

        var payload = new object[]
        {
            new
            {
                type = "free_text",
                orderInLesson = 1,
                content = new { situation = "Updated", instruction = "Answer" },
                customAiPrompt = (string?)null
            },
            new
            {
                type = "choose_option",
                orderInLesson = 2,
                content = new { situation = "New one" },
                customAiPrompt = "extra"
            }
        };

        var response = await _adminClient.PostAsJsonAsync(
            $"/admin/lessons/{lessonId}/exercises/import", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("exercisesCreated").GetInt32().Should().Be(1);
        body.GetProperty("exercisesUpdated").GetInt32().Should().Be(1);

        var stored = await _db.Exercises.AsNoTracking()
            .Where(e => e.LessonId == lessonId).OrderBy(e => e.OrderInLesson).ToListAsync();
        stored.Should().HaveCount(2);
        stored[0].Type.Should().Be("free_text");
        stored[1].Type.Should().Be("choose_option");
    }

    [Test]
    public async Task Import_EmptyArray_Returns400()
    {
        var lessonId = await SeedLessonAsync();

        var response = await _adminClient.PostAsJsonAsync(
            $"/admin/lessons/{lessonId}/exercises/import", Array.Empty<object>());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Import_NonExistentLesson_Returns404()
    {
        var payload = new[]
        {
            new { type = "choose_option", orderInLesson = 1, content = new { x = 1 }, customAiPrompt = (string?)null }
        };

        var response = await _adminClient.PostAsJsonAsync(
            $"/admin/lessons/{Guid.NewGuid()}/exercises/import", payload);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
