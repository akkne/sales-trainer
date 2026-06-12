using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Integration;

[TestFixture]
public class ProfileAvatarTests
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
    public async Task GetProfile_ReturnsAvatarUrl_MatchingCurrentUserId()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db, email: $"profileavatar_{Guid.NewGuid()}@test.com");
        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(user.Id, user.Email, user.DisplayName);

        var response = await client.GetAsync("/profile");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var avatarUrl = body.GetProperty("avatarUrl").GetString();
        avatarUrl.Should().Be($"/avatars/{user.Id}");
    }
}
