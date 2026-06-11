using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Discuss.Models;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Integration;

[TestFixture]
public class DiscussTests
{
    private AppDbContext _db = null!;
    private IServiceScope _scope = null!;
    private User _user = null!;
    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        _scope = IntegrationTestSetup.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _user = await TestDbSeeder.SeedUserAsync(_db, email: $"discuss_{Guid.NewGuid()}@test.com");
        _client = IntegrationTestSetup.Factory.CreateAuthenticatedClient(_user.Id, _user.Email, _user.DisplayName);
    }

    [TearDown]
    public void TearDown() => _scope.Dispose();

    [Test]
    public async Task CreateThread_WithCuratedAndFreeTags_CreatesFreeTagAndAppearsInList()
    {
        var curated = await TestDbSeeder.SeedDiscussTagAsync(_db, slug: $"cold-{Guid.NewGuid()}", name: "Cold Calling");
        var freeLabel = $"FreshTag {Guid.NewGuid()}";

        var response = await _client.PostAsJsonAsync("/discuss/threads", new
        {
            title = "Best opener line?",
            body = "What opener works for you on cold calls?",
            tags = new[] { curated.Slug, freeLabel }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("tags").GetArrayLength().Should().Be(2);

        var createdFree = await _db.DiscussTags.FirstOrDefaultAsync(t => t.Name == freeLabel);
        createdFree.Should().NotBeNull();
        createdFree!.IsCurated.Should().BeFalse();
    }

    [Test]
    public async Task CreateThread_MissingTitle_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/discuss/threads", new
        {
            title = "",
            body = "no title here",
            tags = Array.Empty<string>()
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ListThreads_SortNew_OrdersByLastActivityDesc()
    {
        var older = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id,
            title: $"older-{Guid.NewGuid()}", lastActivityAt: DateTime.UtcNow.AddHours(-5));
        var newer = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id,
            title: $"newer-{Guid.NewGuid()}", lastActivityAt: DateTime.UtcNow);

        var response = await _client.GetAsync("/discuss/threads?sort=new&pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");

        var newerIndex = IndexOfThread(items, newer.Id);
        var olderIndex = IndexOfThread(items, older.Id);
        newerIndex.Should().BeGreaterOrEqualTo(0);
        newerIndex.Should().BeLessThan(olderIndex);
    }

    [Test]
    public async Task ListThreads_SortUnanswered_ReturnsOnlyZeroReplyThreads()
    {
        var unanswered = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id, title: $"unans-{Guid.NewGuid()}", replyCount: 0);
        var answered = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id, title: $"ans-{Guid.NewGuid()}", replyCount: 3);

        var response = await _client.GetAsync("/discuss/threads?sort=unanswered&pageSize=100");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");

        IndexOfThread(items, unanswered.Id).Should().BeGreaterOrEqualTo(0);
        IndexOfThread(items, answered.Id).Should().Be(-1);
    }

    [Test]
    public async Task ListThreads_SortHot_PutsPinnedFirst()
    {
        var normal = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id,
            title: $"normal-{Guid.NewGuid()}", upvoteCount: 50);
        var pinned = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id,
            title: $"pinned-{Guid.NewGuid()}", upvoteCount: 0, isPinned: true);

        var response = await _client.GetAsync("/discuss/threads?sort=hot&pageSize=100");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");

        IndexOfThread(items, pinned.Id).Should().BeLessThan(IndexOfThread(items, normal.Id));
    }

    [Test]
    public async Task ListThreads_FilterByTag_ReturnsOnlyMatching()
    {
        var tag = await TestDbSeeder.SeedDiscussTagAsync(_db, slug: $"objections-{Guid.NewGuid()}", name: "Objections");
        var tagged = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id, title: $"tagged-{Guid.NewGuid()}", tagId: tag.Id);
        var untagged = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id, title: $"untagged-{Guid.NewGuid()}");

        var response = await _client.GetAsync($"/discuss/threads?tag={tag.Slug}&pageSize=100");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");

        IndexOfThread(items, tagged.Id).Should().BeGreaterOrEqualTo(0);
        IndexOfThread(items, untagged.Id).Should().Be(-1);
    }

    [Test]
    public async Task ListThreads_Search_MatchesTitle()
    {
        var unique = $"ZebraMarker{Guid.NewGuid():N}";
        var match = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id, title: $"About {unique} topic");
        await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id, title: $"unrelated-{Guid.NewGuid()}");

        var response = await _client.GetAsync($"/discuss/threads?search={unique}&pageSize=100");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");

        items.GetArrayLength().Should().Be(1);
        IndexOfThread(items, match.Id).Should().Be(0);
    }

    [Test]
    public async Task GetThread_ReturnsRepliesAndViewerUpvoteFlag()
    {
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id);
        await TestDbSeeder.SeedDiscussReplyAsync(_db, thread.Id, _user.Id, body: "first reply");
        await TestDbSeeder.SeedDiscussVoteAsync(_db, _user.Id, DiscussVoteTarget.Thread, thread.Id);

        var response = await _client.GetAsync($"/discuss/threads/{thread.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("replies").GetArrayLength().Should().Be(1);
        body.GetProperty("viewerHasUpvoted").GetBoolean().Should().BeTrue();
    }

    [Test]
    public async Task AddReply_IncrementsReplyCount()
    {
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id);

        var response = await _client.PostAsJsonAsync($"/discuss/threads/{thread.Id}/replies", new { body = "my reply" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var refreshed = await _db.DiscussThreads.AsNoTracking().FirstAsync(t => t.Id == thread.Id);
        refreshed.ReplyCount.Should().Be(1);
    }

    [Test]
    public async Task UpvoteThread_IsIdempotent_ThenUnvote()
    {
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id);

        var first = await _client.PostAsync($"/discuss/threads/{thread.Id}/upvote", null);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        firstBody.GetProperty("upvoteCount").GetInt32().Should().Be(1);
        firstBody.GetProperty("hasUpvoted").GetBoolean().Should().BeTrue();

        // Second upvote must be idempotent — still 1, no duplicate vote row.
        await _client.PostAsync($"/discuss/threads/{thread.Id}/upvote", null);
        var voteRows = await _db.DiscussVotes.CountAsync(v =>
            v.UserId == _user.Id && v.TargetType == DiscussVoteTarget.Thread && v.TargetId == thread.Id);
        voteRows.Should().Be(1);

        var remove = await _client.DeleteAsync($"/discuss/threads/{thread.Id}/upvote");
        var removeBody = await remove.Content.ReadFromJsonAsync<JsonElement>();
        removeBody.GetProperty("upvoteCount").GetInt32().Should().Be(0);
        removeBody.GetProperty("hasUpvoted").GetBoolean().Should().BeFalse();
    }

    [Test]
    public async Task UpvoteReply_TogglesCount()
    {
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id);
        var reply = await TestDbSeeder.SeedDiscussReplyAsync(_db, thread.Id, _user.Id);

        var up = await _client.PostAsync($"/discuss/replies/{reply.Id}/upvote", null);
        (await up.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("upvoteCount").GetInt32().Should().Be(1);

        var down = await _client.DeleteAsync($"/discuss/replies/{reply.Id}/upvote");
        (await down.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("upvoteCount").GetInt32().Should().Be(0);
    }

    [Test]
    public async Task AuthorMarksAcceptedReply_ThreadBecomesSolved()
    {
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id);
        var reply = await TestDbSeeder.SeedDiscussReplyAsync(_db, thread.Id, _user.Id);

        var response = await _client.PostAsJsonAsync($"/discuss/threads/{thread.Id}/accepted-reply", new { replyId = reply.Id });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isSolved").GetBoolean().Should().BeTrue();
        body.GetProperty("acceptedReplyId").GetGuid().Should().Be(reply.Id);
    }

    [Test]
    public async Task NonAuthorMarksAcceptedReply_Returns403()
    {
        var owner = await TestDbSeeder.SeedUserAsync(_db, email: $"owner_{Guid.NewGuid()}@test.com");
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, owner.Id);
        var reply = await TestDbSeeder.SeedDiscussReplyAsync(_db, thread.Id, owner.Id);

        // _client is authenticated as _user, who is not the author.
        var response = await _client.PostAsJsonAsync($"/discuss/threads/{thread.Id}/accepted-reply", new { replyId = reply.Id });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task ClearAcceptedReply_MakesThreadUnsolved()
    {
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id);
        var reply = await TestDbSeeder.SeedDiscussReplyAsync(_db, thread.Id, _user.Id);
        await _client.PostAsJsonAsync($"/discuss/threads/{thread.Id}/accepted-reply", new { replyId = reply.Id });

        var response = await _client.DeleteAsync($"/discuss/threads/{thread.Id}/accepted-reply");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isSolved").GetBoolean().Should().BeFalse();
    }

    [Test]
    public async Task GetStats_CountsOnlyLastWeekVotesForTopAuthors()
    {
        var author = await TestDbSeeder.SeedUserAsync(_db, email: $"top_{Guid.NewGuid()}@test.com", displayName: "Top Author");
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, author.Id);
        var voter = await TestDbSeeder.SeedUserAsync(_db, email: $"voter_{Guid.NewGuid()}@test.com");
        var oldVoter = await TestDbSeeder.SeedUserAsync(_db, email: $"oldvoter_{Guid.NewGuid()}@test.com");

        await TestDbSeeder.SeedDiscussVoteAsync(_db, voter.Id, DiscussVoteTarget.Thread, thread.Id);
        await TestDbSeeder.SeedDiscussVoteAsync(_db, oldVoter.Id, DiscussVoteTarget.Thread, thread.Id,
            createdAt: DateTime.UtcNow.AddDays(-30)); // excluded from week window

        var response = await _client.GetAsync("/discuss/stats");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("totalThreads").GetInt32().Should().BeGreaterThan(0);
        var top = body.GetProperty("topAuthorsOfWeek").EnumerateArray()
            .FirstOrDefault(a => a.GetProperty("authorId").GetGuid() == author.Id);
        top.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        top.GetProperty("upvotesReceived").GetInt32().Should().Be(1);
    }

    [Test]
    public async Task GetPopularTags_OrdersByThreadCount()
    {
        var popular = await TestDbSeeder.SeedDiscussTagAsync(_db, slug: $"pop-{Guid.NewGuid()}", name: "Popular");
        var rare = await TestDbSeeder.SeedDiscussTagAsync(_db, slug: $"rare-{Guid.NewGuid()}", name: "Rare");
        await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id, tagId: popular.Id);
        await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id, tagId: popular.Id);
        await TestDbSeeder.SeedDiscussThreadAsync(_db, _user.Id, tagId: rare.Id);

        var response = await _client.GetAsync("/discuss/tags/popular?limit=50");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var slugs = body.EnumerateArray().Select(t => t.GetProperty("slug").GetString()).ToList();

        slugs.IndexOf(popular.Slug).Should().BeLessThan(slugs.IndexOf(rare.Slug));
    }

    [Test]
    public async Task ListThreads_Unauthenticated_Returns401()
    {
        var anon = IntegrationTestSetup.Factory.CreateClient();
        var response = await anon.GetAsync("/discuss/threads");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static int IndexOfThread(JsonElement items, Guid threadId)
    {
        var index = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (item.GetProperty("id").GetGuid() == threadId) return index;
            index++;
        }
        return -1;
    }
}
