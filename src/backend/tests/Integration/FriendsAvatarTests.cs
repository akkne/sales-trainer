using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Features.Friends.Models;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests.Helpers;

// Use fully-qualified types to avoid ambiguity with test namespace
using Friendship = SalesTrainer.Api.Features.Friends.Models.Friendship;
using FriendshipStatus = SalesTrainer.Api.Features.Friends.Models.FriendshipStatus;

namespace SalesTrainer.Tests.Integration;

[TestFixture]
public class FriendsAvatarTests
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
    public async Task GetFriends_FriendDto_CarriesCorrectAvatarUrl()
    {
        var me = await TestDbSeeder.SeedUserAsync(_db, email: $"friendavatar_me_{Guid.NewGuid()}@test.com");
        var friend = await TestDbSeeder.SeedUserAsync(_db, email: $"friendavatar_fr_{Guid.NewGuid()}@test.com");

        _db.Friendships.Add(new Friendship
        {
            Id = Guid.NewGuid(),
            RequesterId = me.Id,
            AddresseeId = friend.Id,
            Status = FriendshipStatus.Accepted,
            CreatedAt = DateTime.UtcNow,
            AcceptedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(me.Id, me.Email, me.DisplayName);
        var response = await client.GetAsync("/friends");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<JsonElement>();
        var friendEntry = items.EnumerateArray()
            .First(e => e.GetProperty("userId").GetGuid() == friend.Id);
        friendEntry.GetProperty("avatarUrl").GetString().Should().Be($"/avatars/{friend.Id}");
    }

    [Test]
    public async Task GetFriendLeaderboard_EntryCarriesCorrectAvatarUrl()
    {
        var me = await TestDbSeeder.SeedUserAsync(_db, email: $"lbavatar_me_{Guid.NewGuid()}@test.com");
        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(me.Id, me.Email, me.DisplayName);

        var response = await client.GetAsync("/friends/leaderboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<JsonElement>();
        var myEntry = items.EnumerateArray()
            .First(e => e.GetProperty("userId").GetGuid() == me.Id);
        myEntry.GetProperty("avatarUrl").GetString().Should().Be($"/avatars/{me.Id}");
    }

    [Test]
    public async Task SearchUsers_ResultCarriesCorrectAvatarUrl()
    {
        var me = await TestDbSeeder.SeedUserAsync(_db, email: $"searchavatar_me_{Guid.NewGuid()}@test.com");
        var unique = $"AvtSearch{Guid.NewGuid():N}";
        var target = await TestDbSeeder.SeedUserAsync(_db, email: $"searchavatar_t_{Guid.NewGuid()}@test.com", displayName: unique);

        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(me.Id, me.Email, me.DisplayName);
        var response = await client.GetAsync($"/friends/search?query={unique}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<JsonElement>();
        var targetEntry = items.EnumerateArray()
            .First(e => e.GetProperty("userId").GetGuid() == target.Id);
        targetEntry.GetProperty("avatarUrl").GetString().Should().Be($"/avatars/{target.Id}");
    }

    [Test]
    public async Task GetPublicProfile_CarriesCorrectAvatarUrl()
    {
        var me = await TestDbSeeder.SeedUserAsync(_db, email: $"pubprofileavatar_me_{Guid.NewGuid()}@test.com");
        var target = await TestDbSeeder.SeedUserAsync(_db, email: $"pubprofileavatar_t_{Guid.NewGuid()}@test.com");

        var client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(me.Id, me.Email, me.DisplayName);
        var response = await client.GetAsync($"/friends/profile/{target.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("avatarUrl").GetString().Should().Be($"/avatars/{target.Id}");
    }
}
