using FluentAssertions;
using NUnit.Framework;
using Sellevate.Social.Features.Chat.Services.Implementation;
using Sellevate.Social.Features.Friends.Models;
using Sellevate.Social.Infrastructure.Data;
using Sellevate.Social.Tests.Helpers;

namespace Sellevate.Social.Tests.Unit;

[TestFixture]
public sealed class ChatServiceTests
{
    private SocialDbContext _databaseContext = null!;
    private RecordingSocialEventPublisher _eventPublisher = null!;
    private ChatService _chatService = null!;

    private static readonly Guid UserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid FriendUserId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [SetUp]
    public async Task SetUp()
    {
        _databaseContext = TestSocialDatabaseFactory.CreateInMemory();
        _eventPublisher = new RecordingSocialEventPublisher();
        _chatService = new ChatService(mongoContext: null!, _databaseContext, _eventPublisher);

        await TestSocialDatabaseFactory.SeedUserAsync(_databaseContext, UserId, "User");
        await TestSocialDatabaseFactory.SeedUserAsync(_databaseContext, FriendUserId, "Friend");
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    [Test]
    public async Task GetOrCreateConversationAsync_without_accepted_friendship_throws()
    {
        var action = async () => await _chatService.GetOrCreateConversationAsync(UserId, FriendUserId);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("You can only chat with accepted friends.");
    }

    [Test]
    public async Task GetOrCreateConversationAsync_passes_friendship_guard_for_accepted_friend()
    {
        _databaseContext.Friendships.Add(new Friendship
        {
            Id = Guid.NewGuid(),
            RequesterId = UserId,
            AddresseeId = FriendUserId,
            Status = FriendshipStatus.Accepted,
            CreatedAt = DateTime.UtcNow,
            AcceptedAt = DateTime.UtcNow
        });
        await _databaseContext.SaveChangesAsync();

        InvalidOperationException? friendshipGuardException = null;
        try
        {
            await _chatService.GetOrCreateConversationAsync(UserId, FriendUserId);
        }
        catch (InvalidOperationException invalidOperationException)
            when (invalidOperationException.Message == "You can only chat with accepted friends.")
        {
            friendshipGuardException = invalidOperationException;
        }
        catch
        {
        }

        friendshipGuardException.Should().BeNull();
    }
}
