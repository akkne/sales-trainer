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
public class AdminDialogImportTests
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
        return new MultipartFormDataContent { { fileContent, "file", "dialog.json" } };
    }

    private static object SampleBundle(string skillIconic, string bundleTitle) => new
    {
        bundles = new[]
        {
            new
            {
                skillIconicName = skillIconic,
                title = bundleTitle,
                description = "desc",
                iconEmoji = "📞",
                sortOrder = 1,
                isActive = true,
                modes = new[]
                {
                    new
                    {
                        key = "gatekeeper",
                        title = "Get past the gatekeeper",
                        description = "secretary",
                        chatSystemPrompt = "You are a secretary.",
                        feedbackSystemPrompt = "Rate the seller. [XP:N]",
                        sortOrder = 1,
                        isActive = true,
                        voiceEnabled = false,
                        voiceId = (string?)null
                    }
                }
            }
        }
    };

    [Test]
    public async Task ImportDialog_CreatesBundleAndModes()
    {
        var skill = await TestDbSeeder.SeedSkillAsync(_db, iconicName: $"sk-{Guid.NewGuid():N}");
        var bundleTitle = $"Bundle {Guid.NewGuid():N}";

        var response = await _adminClient.PostAsync("/admin/dialog/import",
            JsonFile(SampleBundle(skill.IconicName, bundleTitle)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("bundlesCreated").GetInt32().Should().Be(1);
        body.GetProperty("modesCreated").GetInt32().Should().Be(1);
        body.GetProperty("errors").GetArrayLength().Should().Be(0);

        var bundle = await _db.DialogBundles.AsNoTracking()
            .FirstOrDefaultAsync(b => b.SkillId == skill.Id && b.Title == bundleTitle);
        bundle.Should().NotBeNull();
        (await _db.DialogModes.AsNoTracking().CountAsync(m => m.BundleId == bundle!.Id))
            .Should().Be(1);
    }

    [Test]
    public async Task ImportDialog_IsIdempotent_OnReimport()
    {
        var skill = await TestDbSeeder.SeedSkillAsync(_db, iconicName: $"sk-{Guid.NewGuid():N}");
        var payload = SampleBundle(skill.IconicName, $"Bundle {Guid.NewGuid():N}");

        (await _adminClient.PostAsync("/admin/dialog/import", JsonFile(payload)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await _adminClient.PostAsync("/admin/dialog/import", JsonFile(payload));
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("bundlesCreated").GetInt32().Should().Be(0);
        body.GetProperty("bundlesUpdated").GetInt32().Should().Be(1);
        body.GetProperty("modesCreated").GetInt32().Should().Be(0);
        body.GetProperty("modesUpdated").GetInt32().Should().Be(1);
    }

    [Test]
    public async Task ImportDialog_UnknownSkill_ReportedAsError()
    {
        var response = await _adminClient.PostAsync("/admin/dialog/import",
            JsonFile(SampleBundle("does-not-exist", "Bundle")));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("bundlesCreated").GetInt32().Should().Be(0);
        body.GetProperty("errors").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ImportDialog_AsRegularUser_Returns403()
    {
        var response = await _userClient.PostAsync("/admin/dialog/import",
            JsonFile(SampleBundle("any", "Bundle")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
