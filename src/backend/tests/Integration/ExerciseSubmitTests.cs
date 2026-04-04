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
    public async Task GetLessons_ValidToken_Returns200WithList()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"gl_{Guid.NewGuid()}@test.com");
        var skill = await TestDbSeeder.SeedSkillAsync(_db, slug: $"skill-gl-{Guid.NewGuid()}");
        await TestDbSeeder.SeedLessonAsync(_db, skill.Id);
        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        var response = await client.GetAsync($"/skills/{skill.Slug}/lessons");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lessons = await response.Content.ReadFromJsonAsync<JsonElement>();
        lessons.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task GetLessons_UnknownSkillSlug_Returns404()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"gl_nf_{Guid.NewGuid()}@test.com");
        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        var response = await client.GetAsync("/skills/nonexistent-slug-xyz/lessons");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetLessons_NoToken_Returns401()
    {
        var client = IntegrationTestSetup.Factory.CreateClient();
        var response = await client.GetAsync("/skills/any-slug/lessons");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetExercises_ValidLessonId_Returns200WithList()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"ge_{Guid.NewGuid()}@test.com");
        var skill = await TestDbSeeder.SeedSkillAsync(_db, slug: $"skill-ge-{Guid.NewGuid()}");
        var lesson = await TestDbSeeder.SeedLessonAsync(_db, skill.Id);
        await TestDbSeeder.SeedMultipleChoiceExerciseAsync(_db, lesson.Id);
        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        var response = await client.GetAsync($"/lessons/{lesson.Id}/exercises");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var exercises = await response.Content.ReadFromJsonAsync<JsonElement>();
        exercises.GetArrayLength().Should().BeGreaterThan(0);
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

    // ──────────────────────────────────────────────────────────────────────────
    // Sequential lesson unlock integration tests
    // ──────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task GetLessons_FirstAccess_FirstLessonAvailable_RestLocked()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db, email: $"seed_{Guid.NewGuid()}@test.com");
        var skill = await TestDbSeeder.SeedSkillAsync(_db, slug: $"seed-skill-{Guid.NewGuid()}");
        await TestDbSeeder.SeedLessonAsync(_db, skill.Id, title: "L1", sortOrder: 1);
        await TestDbSeeder.SeedLessonAsync(_db, skill.Id, title: "L2", sortOrder: 2);
        await TestDbSeeder.SeedLessonAsync(_db, skill.Id, title: "L3", sortOrder: 3);
        await TestDbSeeder.SeedSkillProgressAsync(_db, user.Id, skill.Id,
            status: "available", totalLessonCount: 3);

        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        var response = await client.GetAsync($"/skills/{skill.Slug}/lessons");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var lessons = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .OrderBy(l => l.GetProperty("sortOrder").GetInt32())
            .ToList();

        lessons.Should().HaveCount(3);
        lessons[0].GetProperty("status").GetString().Should().Be("available");
        lessons[1].GetProperty("status").GetString().Should().Be("locked");
        lessons[2].GetProperty("status").GetString().Should().Be("locked");
    }

    [Test]
    public async Task SubmitLesson1_UnlocksLesson2()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db, email: $"unlock_{Guid.NewGuid()}@test.com");
        var skill = await TestDbSeeder.SeedSkillAsync(_db, slug: $"unlock-skill-{Guid.NewGuid()}");
        var lesson1 = await TestDbSeeder.SeedLessonAsync(_db, skill.Id, title: "L1", sortOrder: 1);
        await TestDbSeeder.SeedLessonAsync(_db, skill.Id, title: "L2", sortOrder: 2);
        await TestDbSeeder.SeedSkillProgressAsync(_db, user.Id, skill.Id,
            status: "available", totalLessonCount: 2);
        var exercise = await TestDbSeeder.SeedMultipleChoiceExerciseAsync(_db,
            lesson1.Id, correctOptionIndex: 0);

        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        // Trigger seed by first GET
        await client.GetAsync($"/skills/{skill.Slug}/lessons");

        // Complete lesson 1
        await client.PostAsJsonAsync(
            $"/exercises/{exercise.Id}/submit",
            new { answer = new { selectedOptionIndex = 0 } });

        // Check lesson 2 is now available
        var response = await client.GetAsync($"/skills/{skill.Slug}/lessons");
        var lessons = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray()
            .OrderBy(l => l.GetProperty("sortOrder").GetInt32())
            .ToList();

        lessons[0].GetProperty("status").GetString().Should().Be("completed");
        lessons[1].GetProperty("status").GetString().Should().Be("available");
    }

    [Test]
    public async Task FullLessonUnlockFlow_ThreeLessons_UnlocksSequentially()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db, email: $"flow_{Guid.NewGuid()}@test.com");
        var skill = await TestDbSeeder.SeedSkillAsync(_db, slug: $"flow-skill-{Guid.NewGuid()}");
        var lesson1 = await TestDbSeeder.SeedLessonAsync(_db, skill.Id, title: "L1", sortOrder: 1);
        var lesson2 = await TestDbSeeder.SeedLessonAsync(_db, skill.Id, title: "L2", sortOrder: 2);
        var lesson3 = await TestDbSeeder.SeedLessonAsync(_db, skill.Id, title: "L3", sortOrder: 3);
        await TestDbSeeder.SeedSkillProgressAsync(_db, user.Id, skill.Id,
            status: "available", totalLessonCount: 3);
        var ex1 = await TestDbSeeder.SeedMultipleChoiceExerciseAsync(_db, lesson1.Id, correctOptionIndex: 0);
        var ex2 = await TestDbSeeder.SeedMultipleChoiceExerciseAsync(_db, lesson2.Id, correctOptionIndex: 0);
        var ex3 = await TestDbSeeder.SeedMultipleChoiceExerciseAsync(_db, lesson3.Id, correctOptionIndex: 0);

        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        // Access to trigger seeding
        await client.GetAsync($"/skills/{skill.Slug}/lessons");

        // Complete lesson 1 → lesson 2 unlocks
        await client.PostAsJsonAsync($"/exercises/{ex1.Id}/submit", new { answer = new { selectedOptionIndex = 0 } });

        var r2 = await client.GetAsync($"/skills/{skill.Slug}/lessons");
        var l2 = (await r2.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().OrderBy(l => l.GetProperty("sortOrder").GetInt32()).ToList();
        l2[0].GetProperty("status").GetString().Should().Be("completed");
        l2[1].GetProperty("status").GetString().Should().Be("available");
        l2[2].GetProperty("status").GetString().Should().Be("locked");

        // Complete lesson 2 → lesson 3 unlocks
        await client.PostAsJsonAsync($"/exercises/{ex2.Id}/submit", new { answer = new { selectedOptionIndex = 0 } });

        var r3 = await client.GetAsync($"/skills/{skill.Slug}/lessons");
        var l3 = (await r3.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().OrderBy(l => l.GetProperty("sortOrder").GetInt32()).ToList();
        l3[0].GetProperty("status").GetString().Should().Be("completed");
        l3[1].GetProperty("status").GetString().Should().Be("completed");
        l3[2].GetProperty("status").GetString().Should().Be("available");

        // Complete lesson 3 → skill completed
        await client.PostAsJsonAsync($"/exercises/{ex3.Id}/submit", new { answer = new { selectedOptionIndex = 0 } });

        var r4 = await client.GetAsync($"/skills/{skill.Slug}/lessons");
        var l4 = (await r4.Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().OrderBy(l => l.GetProperty("sortOrder").GetInt32()).ToList();
        l4.Should().AllSatisfy(l => l.GetProperty("status").GetString().Should().Be("completed"));
    }
}
