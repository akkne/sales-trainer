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
public class DiscussAvatarTests
{
    private AppDbContext _db = null!;
    private IServiceScope _scope = null!;
    private Api.Features.Auth.Models.User _user = null!;
    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        _scope = IntegrationTestSetup.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _user = await TestDbSeeder.SeedUserAsync(_db, email: $"discussavatar_{Guid.NewGuid()}@test.com");
        _client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(_user.Id, _user.Email, _user.DisplayName);
    }

    [TearDown]
    public void TearDown() => _scope.Dispose();

    [Test]
    public async Task ListThreads_ThreadSummary_CarriesCorrectAuthorAvatarUrl()
    {
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id,
            title: $"AvtThread-{Guid.NewGuid()}");

        var response = await _client.GetAsync("/discuss/threads?sort=new&pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");

        var threadEntry = items.EnumerateArray()
            .First(e => e.GetProperty("id").GetGuid() == thread.Id);
        threadEntry.GetProperty("authorAvatarUrl").GetString().Should().Be($"/avatars/{_user.Id}");
    }

    [Test]
    public async Task GetThread_Reply_CarriesCorrectAuthorAvatarUrl()
    {
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id,
            title: $"AvtReplyThread-{Guid.NewGuid()}");
        await TestDbSeeder.SeedDiscussReplyAsync(_db, thread.Id, _user.Id, body: "avatar reply test");

        var response = await _client.GetAsync($"/discuss/threads/{thread.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var replies = body.GetProperty("replies");
        replies.GetArrayLength().Should().Be(1);
        replies.EnumerateArray().First()
            .GetProperty("authorAvatarUrl").GetString().Should().Be($"/avatars/{_user.Id}");
    }
}
