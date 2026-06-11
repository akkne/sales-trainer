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
public class AdminDiscussTests
{
    private AppDbContext _db = null!;
    private IServiceScope _scope = null!;
    private User _author = null!;
    private HttpClient _adminClient = null!;
    private HttpClient _userClient = null!;

    [SetUp]
    public async Task SetUp()
    {
        _scope = IntegrationTestSetup.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var admin = await TestDbSeeder.SeedUserAsync(_db, email: $"admin_{Guid.NewGuid()}@test.com", role: UserRole.Admin);
        var user = await TestDbSeeder.SeedUserAsync(_db, email: $"user_{Guid.NewGuid()}@test.com", role: UserRole.User);
        _author = user;

        _adminClient = IntegrationTestSetup.Factory.CreateAuthenticatedClient(admin.Id, admin.Email, admin.DisplayName, UserRole.Admin);
        _userClient = IntegrationTestSetup.Factory.CreateAuthenticatedClient(user.Id, user.Email, user.DisplayName, UserRole.User);
    }

    [TearDown]
    public void TearDown() => _scope.Dispose();

    [Test]
    public async Task AdminEndpoint_AsRegularUser_Returns403()
    {
        var response = await _userClient.GetAsync("/admin/discuss/threads");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task DeleteThread_RemovesThreadRepliesTagsAndVotes()
    {
        var tag = await TestDbSeeder.SeedDiscussTagAsync(_db, slug: $"del-{Guid.NewGuid()}");
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, _author.Id, tagId: tag.Id);
        var reply = await TestDbSeeder.SeedDiscussReplyAsync(_db, thread.Id, _author.Id);
        await TestDbSeeder.SeedDiscussVoteAsync(_db, _author.Id, DiscussVoteTarget.Thread, thread.Id);
        await TestDbSeeder.SeedDiscussVoteAsync(_db, _author.Id, DiscussVoteTarget.Reply, reply.Id);

        var response = await _adminClient.DeleteAsync($"/admin/discuss/threads/{thread.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _db.DiscussThreads.AnyAsync(t => t.Id == thread.Id)).Should().BeFalse();
        (await _db.DiscussReplies.AnyAsync(r => r.Id == reply.Id)).Should().BeFalse();
        (await _db.DiscussThreadTags.AnyAsync(tt => tt.ThreadId == thread.Id)).Should().BeFalse();
        (await _db.DiscussVotes.AnyAsync(v => v.TargetId == thread.Id || v.TargetId == reply.Id)).Should().BeFalse();
    }

    [Test]
    public async Task DeleteReply_ThatWasAccepted_ClearsAcceptedAndDecrementsCount()
    {
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, _author.Id, replyCount: 1);
        var reply = await TestDbSeeder.SeedDiscussReplyAsync(_db, thread.Id, _author.Id);
        thread.AcceptedReplyId = reply.Id;
        reply.IsAccepted = true;
        await _db.SaveChangesAsync();

        var response = await _adminClient.DeleteAsync($"/admin/discuss/replies/{reply.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshed = await _db.DiscussThreads.AsNoTracking().FirstAsync(t => t.Id == thread.Id);
        refreshed.AcceptedReplyId.Should().BeNull();
        refreshed.ReplyCount.Should().Be(0);
    }

    [Test]
    public async Task PinThread_MakesItAppearFirstInUserList()
    {
        await TestDbSeeder.SeedDiscussThreadAsync(_db, _author.Id, title: $"loud-{Guid.NewGuid()}", upvoteCount: 99);
        var pinTarget = await TestDbSeeder.SeedDiscussThreadAsync(_db, _author.Id, title: $"quiet-{Guid.NewGuid()}");

        var pin = await _adminClient.PostAsJsonAsync($"/admin/discuss/threads/{pinTarget.Id}/pin", new { isPinned = true });
        pin.StatusCode.Should().Be(HttpStatusCode.OK);
        (await pin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isPinned").GetBoolean().Should().BeTrue();

        var list = await _userClient.GetAsync("/discuss/threads?sort=hot&pageSize=100");
        var items = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
        items[0].GetProperty("id").GetGuid().Should().Be(pinTarget.Id);
    }

    [Test]
    public async Task SetHot_TogglesFlag()
    {
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, _author.Id);
        var response = await _adminClient.PostAsJsonAsync($"/admin/discuss/threads/{thread.Id}/hot", new { isHot = true });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isHot").GetBoolean().Should().BeTrue();
    }

    [Test]
    public async Task AdminMarksAcceptedReply_OnOthersThread_Succeeds()
    {
        var thread = await TestDbSeeder.SeedDiscussThreadAsync(_db, _author.Id);
        var reply = await TestDbSeeder.SeedDiscussReplyAsync(_db, thread.Id, _author.Id);

        // Admin is not the author but may accept.
        var response = await _adminClient.PostAsJsonAsync($"/discuss/threads/{thread.Id}/accepted-reply", new { replyId = reply.Id });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isSolved").GetBoolean().Should().BeTrue();
    }

    [Test]
    public async Task CreateTag_CreatesCuratedTag()
    {
        var name = $"Curated {Guid.NewGuid()}";
        var response = await _adminClient.PostAsJsonAsync("/admin/discuss/tags", new { name });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tag = await response.Content.ReadFromJsonAsync<JsonElement>();
        tag.GetProperty("isCurated").GetBoolean().Should().BeTrue();
    }

    [Test]
    public async Task CreateTag_DuplicateSlug_Returns409()
    {
        var slug = $"dup-{Guid.NewGuid()}";
        await TestDbSeeder.SeedDiscussTagAsync(_db, slug: slug, name: "Existing");

        var response = await _adminClient.PostAsJsonAsync("/admin/discuss/tags", new { name = "Existing", slug });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task UpdateTag_ChangesName()
    {
        var tag = await TestDbSeeder.SeedDiscussTagAsync(_db, slug: $"upd-{Guid.NewGuid()}", name: "Old");
        var newName = $"New {Guid.NewGuid()}";

        var response = await _adminClient.PutAsJsonAsync($"/admin/discuss/tags/{tag.Id}", new { name = newName });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString().Should().Be(newName);
    }

    [Test]
    public async Task DeleteTag_CascadesThreadTags()
    {
        var tag = await TestDbSeeder.SeedDiscussTagAsync(_db, slug: $"deltag-{Guid.NewGuid()}");
        await TestDbSeeder.SeedDiscussThreadAsync(_db, _author.Id, tagId: tag.Id);

        var response = await _adminClient.DeleteAsync($"/admin/discuss/tags/{tag.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _db.DiscussTags.AnyAsync(t => t.Id == tag.Id)).Should().BeFalse();
        (await _db.DiscussThreadTags.AnyAsync(tt => tt.TagId == tag.Id)).Should().BeFalse();
    }

    [Test]
    public async Task ListThreads_AsAdmin_ReturnsPagedResult()
    {
        await TestDbSeeder.SeedDiscussThreadAsync(_db, _author.Id, title: $"admin-list-{Guid.NewGuid()}");
        var response = await _adminClient.GetAsync("/admin/discuss/threads?page=1&pageSize=5");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("pageSize").GetInt32().Should().Be(5);
        body.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
    }
}
