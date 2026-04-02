using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Integration;

[TestFixture]
public class ExerciseSubmitTests
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
    public async Task Submit_CorrectMultipleChoice_Returns200IsCorrectTrueXpEarned()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"ex_{Guid.NewGuid()}@test.com");
        var skill = await TestDbSeeder.SeedSkillAsync(_db, slug: $"skill-{Guid.NewGuid()}");
        var lesson = await TestDbSeeder.SeedLessonAsync(_db, skill.Id, xpReward: 50);
        await TestDbSeeder.SeedSkillProgressAsync(_db, user.Id, skill.Id,
            status: "available", totalLessonCount: 1);
        var exercise = await TestDbSeeder.SeedMultipleChoiceExerciseAsync(_db,
            lesson.Id, correctOptionIndex: 1);

        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        var response = await client.PostAsJsonAsync(
            $"/exercises/{exercise.Id}/submit",
            new { answer = new { selectedOptionIndex = 1 } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isCorrect").GetBoolean().Should().BeTrue();
        body.GetProperty("score").GetInt32().Should().Be(100);
        body.GetProperty("xpEarned").GetInt32().Should().Be(50);
    }

    [Test]
    public async Task Submit_WrongMultipleChoice_Returns200IsCorrectFalseNoXp()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"ex_w_{Guid.NewGuid()}@test.com");
        var skill = await TestDbSeeder.SeedSkillAsync(_db, slug: $"skill-w-{Guid.NewGuid()}");
        var lesson = await TestDbSeeder.SeedLessonAsync(_db, skill.Id, xpReward: 50);
        var exercise = await TestDbSeeder.SeedMultipleChoiceExerciseAsync(_db,
            lesson.Id, correctOptionIndex: 1);

        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        var response = await client.PostAsJsonAsync(
            $"/exercises/{exercise.Id}/submit",
            new { answer = new { selectedOptionIndex = 0 } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isCorrect").GetBoolean().Should().BeFalse();
        body.GetProperty("xpEarned").GetInt32().Should().Be(0);
    }

    [Test]
    public async Task Submit_NonExistentExercise_Returns404()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"ex_nf_{Guid.NewGuid()}@test.com");
        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        var response = await client.PostAsJsonAsync(
            $"/exercises/{Guid.NewGuid()}/submit",
            new { answer = new { selectedOptionIndex = 0 } });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Submit_WithoutAuth_Returns401()
    {
        var client = IntegrationTestSetup.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/exercises/{Guid.NewGuid()}/submit",
            new { answer = new { selectedOptionIndex = 0 } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Submit_CorrectAnswer_LessonProgressMarkedCompleted()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"ex_lp_{Guid.NewGuid()}@test.com");
        var skill = await TestDbSeeder.SeedSkillAsync(_db, slug: $"skill-lp-{Guid.NewGuid()}");
        var lesson = await TestDbSeeder.SeedLessonAsync(_db, skill.Id);
        await TestDbSeeder.SeedSkillProgressAsync(_db, user.Id, skill.Id,
            status: "available", totalLessonCount: 1);
        var exercise = await TestDbSeeder.SeedMultipleChoiceExerciseAsync(_db,
            lesson.Id, correctOptionIndex: 0);

        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        await client.PostAsJsonAsync(
            $"/exercises/{exercise.Id}/submit",
            new { answer = new { selectedOptionIndex = 0 } });

        // Verify via GET lessons
        var lessonsResponse = await client.GetAsync($"/skills/{skill.Slug}/lessons");
        lessonsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var lessons = await lessonsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var first = lessons.EnumerateArray().First();
        first.GetProperty("status").GetString().Should().Be("completed");
    }
}
