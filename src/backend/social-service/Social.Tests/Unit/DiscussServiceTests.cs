using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Social.Features.Discuss.Models;
using Sellevate.Social.Features.Discuss.Services.Implementation;
using Sellevate.Social.Infrastructure.Data;
using Sellevate.Social.Infrastructure.Storage.Abstract;
using Sellevate.Social.Tests.Helpers;

namespace Sellevate.Social.Tests.Unit;

[TestFixture]
public sealed class DiscussServiceTests
{
    private SocialDbContext _databaseContext = null!;
    private IObjectStorage _objectStorage = null!;
    private DiscussService _discussService = null!;

    private static readonly Guid AuthorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ViewerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [SetUp]
    public async Task SetUp()
    {
        _databaseContext = TestSocialDatabaseFactory.CreateInMemory();
        _objectStorage = Substitute.For<IObjectStorage>();
        _discussService = new DiscussService(_databaseContext, _objectStorage, NullLogger<DiscussService>.Instance);

        await TestSocialDatabaseFactory.SeedUserAsync(_databaseContext, AuthorId, "Author");
        await TestSocialDatabaseFactory.SeedUserAsync(_databaseContext, ViewerId, "Viewer");
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    [Test]
    public async Task CreateThreadAsync_persists_thread_with_resolved_tags()
    {
        var thread = await _discussService.CreateThreadAsync(
            AuthorId, "How to handle objections", "Body text", ["Objections", "Tips"]);

        thread.Title.Should().Be("How to handle objections");
        thread.AuthorName.Should().Be("Author");
        thread.Tags.Should().HaveCount(2);
        _databaseContext.DiscussTags.Should().HaveCount(2);
        _databaseContext.DiscussThreadTags.Should().HaveCount(2);
    }

    [Test]
    public async Task CreateThreadAsync_reuses_existing_tag_slug()
    {
        await _discussService.CreateThreadAsync(AuthorId, "First", "Body", ["Sales"]);
        await _discussService.CreateThreadAsync(AuthorId, "Second", "Body", ["sales"]);

        _databaseContext.DiscussTags.Should().HaveCount(1);
    }

    [Test]
    public async Task AddReplyAsync_increments_reply_count_and_bumps_activity()
    {
        var thread = await _discussService.CreateThreadAsync(AuthorId, "Title", "Body", []);

        var reply = await _discussService.AddReplyAsync(thread.Id, ViewerId, "A reply");

        reply.Should().NotBeNull();
        var stored = await _databaseContext.DiscussThreads.FindAsync(thread.Id);
        stored!.ReplyCount.Should().Be(1);
    }

    [Test]
    public async Task SetThreadVoteAsync_toggles_vote_and_counter()
    {
        var thread = await _discussService.CreateThreadAsync(AuthorId, "Title", "Body", []);

        var upvoted = await _discussService.SetThreadVoteAsync(thread.Id, ViewerId, upvote: true);
        upvoted!.UpvoteCount.Should().Be(1);
        upvoted.HasUpvoted.Should().BeTrue();

        var removed = await _discussService.SetThreadVoteAsync(thread.Id, ViewerId, upvote: false);
        removed!.UpvoteCount.Should().Be(0);
        removed.HasUpvoted.Should().BeFalse();
    }

    [Test]
    public async Task SetThreadVoteAsync_is_idempotent_on_double_upvote()
    {
        var thread = await _discussService.CreateThreadAsync(AuthorId, "Title", "Body", []);

        await _discussService.SetThreadVoteAsync(thread.Id, ViewerId, upvote: true);
        var second = await _discussService.SetThreadVoteAsync(thread.Id, ViewerId, upvote: true);

        second!.UpvoteCount.Should().Be(1);
        _databaseContext.DiscussVotes.Should().HaveCount(1);
    }

    [Test]
    public async Task SetAcceptedReplyAsync_by_author_marks_solved()
    {
        var thread = await _discussService.CreateThreadAsync(AuthorId, "Title", "Body", []);
        var reply = await _discussService.AddReplyAsync(thread.Id, ViewerId, "Answer");

        var (status, detail) = await _discussService.SetAcceptedReplyAsync(
            thread.Id, AuthorId, isAdmin: false, reply!.Id);

        status.Should().Be(DiscussOperationStatus.Success);
        detail!.IsSolved.Should().BeTrue();
        detail.AcceptedReplyId.Should().Be(reply.Id);
    }

    [Test]
    public async Task SetAcceptedReplyAsync_by_other_user_is_forbidden()
    {
        var thread = await _discussService.CreateThreadAsync(AuthorId, "Title", "Body", []);
        var reply = await _discussService.AddReplyAsync(thread.Id, ViewerId, "Answer");

        var (status, _) = await _discussService.SetAcceptedReplyAsync(
            thread.Id, ViewerId, isAdmin: false, reply!.Id);

        status.Should().Be(DiscussOperationStatus.Forbidden);
    }

    [Test]
    public async Task GetThreadAsync_increments_view_count()
    {
        var thread = await _discussService.CreateThreadAsync(AuthorId, "Title", "Body", []);

        await _discussService.GetThreadAsync(thread.Id, ViewerId, incrementView: true);

        var stored = await _databaseContext.DiscussThreads.FindAsync(thread.Id);
        stored!.ViewCount.Should().Be(1);
    }

    [Test]
    public async Task ListThreadsAsync_filters_unanswered()
    {
        var answered = await _discussService.CreateThreadAsync(AuthorId, "Answered", "Body", []);
        await _discussService.AddReplyAsync(answered.Id, ViewerId, "Reply");
        await _discussService.CreateThreadAsync(AuthorId, "Unanswered", "Body", []);

        var query = new DiscussThreadQuery("unanswered", null, null, 1, 20, IncludeAll: false);
        var result = await _discussService.ListThreadsAsync(query, ViewerId);

        result.Items.Should().ContainSingle(item => item.Title == "Unanswered");
    }

    [Test]
    public async Task CreateCuratedTagAsync_rejects_duplicate_slug()
    {
        var (firstStatus, _) = await _discussService.CreateCuratedTagAsync("Sales", null);
        var (secondStatus, _) = await _discussService.CreateCuratedTagAsync("Sales", null);

        firstStatus.Should().Be(DiscussOperationStatus.Success);
        secondStatus.Should().Be(DiscussOperationStatus.Conflict);
    }

    [Test]
    public async Task UploadPhotosAsync_for_thread_stores_object_and_metadata()
    {
        var thread = await _discussService.CreateThreadAsync(AuthorId, "Title", "Body", []);
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00 };
        var uploadFiles = new List<DiscussPhotoUploadFile>
        {
            new(new MemoryStream(pngBytes), "photo.png", pngBytes.Length)
        };

        var (status, photos) = await _discussService.UploadPhotosAsync(
            DiscussPhotoOwner.Thread, thread.Id, AuthorId, uploadFiles);

        status.Should().Be(DiscussPhotoUploadStatus.Success);
        photos.Should().HaveCount(1);
        await _objectStorage.Received(1).PutAsync(
            Arg.Any<string>(), Arg.Any<Stream>(), "image/png", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UploadPhotosAsync_by_non_author_is_forbidden()
    {
        var thread = await _discussService.CreateThreadAsync(AuthorId, "Title", "Body", []);
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00 };
        var uploadFiles = new List<DiscussPhotoUploadFile>
        {
            new(new MemoryStream(pngBytes), "photo.png", pngBytes.Length)
        };

        var (status, _) = await _discussService.UploadPhotosAsync(
            DiscussPhotoOwner.Thread, thread.Id, ViewerId, uploadFiles);

        status.Should().Be(DiscussPhotoUploadStatus.Forbidden);
    }

    [Test]
    public async Task DeleteThreadAsync_removes_thread_votes_and_photos()
    {
        var thread = await _discussService.CreateThreadAsync(AuthorId, "Title", "Body", []);
        await _discussService.SetThreadVoteAsync(thread.Id, ViewerId, upvote: true);

        var deleted = await _discussService.DeleteThreadAsync(thread.Id);

        deleted.Should().BeTrue();
        _databaseContext.DiscussThreads.Should().BeEmpty();
        _databaseContext.DiscussVotes.Should().BeEmpty();
    }
}
