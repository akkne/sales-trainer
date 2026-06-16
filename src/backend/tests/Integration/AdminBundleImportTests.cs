using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
public class AdminBundleImportTests
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
        _adminClient = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            admin.Id, admin.Email, admin.DisplayName, UserRole.Admin);

        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"user_{Guid.NewGuid()}@test.com", role: UserRole.User);
        _userClient = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName, UserRole.User);
    }

    [TearDown]
    public void TearDown() => _scope.Dispose();

    private static MultipartFormDataContent JsonFile(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var fileContent = new StringContent(json, Encoding.UTF8);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var form = new MultipartFormDataContent { { fileContent, "file", "bundle.json" } };
        return form;
    }

    private static object SampleBundle(string skillIconic, string topicIconic)
    {
        return new
        {
            skills = new[]
            {
                new
                {
                    iconicName = skillIconic,
                    title = "Cold Calling",
                    description = "desc",
                    orderInTree = 1,
                    stage = "preparation",
                    topics = new[]
                    {
                        new
                        {
                            iconicName = topicIconic,
                            title = "Basics",
                            orderInSkill = 1,
                            lessons = new[]
                            {
                                new
                                {
                                    title = "Opening the call",
                                    orderInTopic = 1,
                                    exercises = new object[]
                                    {
                                        new
                                        {
                                            type = "choose_option",
                                            orderInLesson = 1,
                                            content = new
                                            {
                                                situation = "Client says it is expensive",
                                                options = new[]
                                                {
                                                    new { text = "Lower the price", is_correct = false },
                                                    new { text = "Ask what they compare to", is_correct = true }
                                                }
                                            }
                                        },
                                        new
                                        {
                                            type = "free_text",
                                            orderInLesson = 2,
                                            content = new { instruction = "Respond to the objection" },
                                            customAiPrompt = (string?)null
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    [Test]
    public async Task ImportBundle_CreatesEntireTree()
    {
        var skillIconic = $"sk-{Guid.NewGuid():N}";
        var topicIconic = $"tp-{Guid.NewGuid():N}";

        var response = await _adminClient.PostAsync("/admin/seeder/bundle",
            JsonFile(SampleBundle(skillIconic, topicIconic)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("skillsCreated").GetInt32().Should().Be(1);
        body.GetProperty("topicsCreated").GetInt32().Should().Be(1);
        body.GetProperty("lessonsCreated").GetInt32().Should().Be(1);
        body.GetProperty("exercisesCreated").GetInt32().Should().Be(2);
        body.GetProperty("errors").GetArrayLength().Should().Be(0);

        var skill = await _db.Skills.AsNoTracking().FirstOrDefaultAsync(s => s.IconicName == skillIconic);
        skill.Should().NotBeNull();
        var topic = await _db.Topics.AsNoTracking().FirstOrDefaultAsync(t => t.IconicName == topicIconic);
        topic.Should().NotBeNull();
        topic!.SkillId.Should().Be(skill!.Id);
    }

    [Test]
    public async Task ImportBundle_IsIdempotent_OnReimport()
    {
        var skillIconic = $"sk-{Guid.NewGuid():N}";
        var topicIconic = $"tp-{Guid.NewGuid():N}";
        var bundle = SampleBundle(skillIconic, topicIconic);

        (await _adminClient.PostAsync("/admin/seeder/bundle", JsonFile(bundle)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await _adminClient.PostAsync("/admin/seeder/bundle", JsonFile(bundle));
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("skillsCreated").GetInt32().Should().Be(0);
        body.GetProperty("skillsUpdated").GetInt32().Should().Be(1);
        body.GetProperty("exercisesCreated").GetInt32().Should().Be(0);
        body.GetProperty("exercisesUpdated").GetInt32().Should().Be(2);

        var exercises = await _db.Exercises.AsNoTracking()
            .Where(e => e.Lesson!.Topic!.IconicName == topicIconic).ToListAsync();
        exercises.Should().HaveCount(2);
    }

    [Test]
    public async Task ImportBundle_InvalidExerciseContent_ReportedAsError()
    {
        var skillIconic = $"sk-{Guid.NewGuid():N}";
        var topicIconic = $"tp-{Guid.NewGuid():N}";

        var bundle = new
        {
            skills = new[]
            {
                new
                {
                    iconicName = skillIconic,
                    title = "Skill",
                    orderInTree = 1,
                    topics = new[]
                    {
                        new
                        {
                            iconicName = topicIconic,
                            title = "Topic",
                            orderInSkill = 1,
                            lessons = new[]
                            {
                                new
                                {
                                    title = "Lesson",
                                    orderInTopic = 1,
                                    exercises = new object[]
                                    {
                                        // choose_option missing situation and options → invalid
                                        new { type = "choose_option", orderInLesson = 1, content = new { } }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var response = await _adminClient.PostAsync("/admin/seeder/bundle", JsonFile(bundle));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Skill/topic/lesson still upserted, but the invalid exercise is skipped with an error.
        body.GetProperty("exercisesCreated").GetInt32().Should().Be(0);
        body.GetProperty("errors").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ImportBundle_DuplicateTopicAcrossSkills_ReportedAsError()
    {
        var topicIconic = $"tp-{Guid.NewGuid():N}";
        var bundle = new
        {
            skills = new[]
            {
                new
                {
                    iconicName = $"sk-a-{Guid.NewGuid():N}", title = "A", orderInTree = 1,
                    topics = new[] { new { iconicName = topicIconic, title = "Shared", orderInSkill = 1 } }
                },
                new
                {
                    iconicName = $"sk-b-{Guid.NewGuid():N}", title = "B", orderInTree = 2,
                    topics = new[] { new { iconicName = topicIconic, title = "Shared", orderInSkill = 1 } }
                }
            }
        };

        var response = await _adminClient.PostAsync("/admin/seeder/bundle", JsonFile(bundle));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // First skill creates the topic; the second skill's duplicate iconicName is refused.
        body.GetProperty("topicsCreated").GetInt32().Should().Be(1);
        body.GetProperty("errors").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ImportBundle_AsRegularUser_Returns403()
    {
        var response = await _userClient.PostAsync("/admin/seeder/bundle",
            JsonFile(SampleBundle($"sk-{Guid.NewGuid():N}", $"tp-{Guid.NewGuid():N}")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
