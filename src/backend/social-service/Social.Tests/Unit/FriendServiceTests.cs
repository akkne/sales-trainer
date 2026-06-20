using FluentAssertions;
using NUnit.Framework;
using Sellevate.Social.Features.Friends.Models;
using Sellevate.Social.Features.Friends.Services.Implementation;
using Sellevate.Social.Infrastructure.Data;
using Sellevate.Social.Tests.Helpers;

namespace Sellevate.Social.Tests.Unit;

[TestFixture]
public sealed class FriendServiceTests
{
    private SocialDbContext _databaseContext = null!;
    private RecordingSocialEventPublisher _eventPublisher = null!;
    private FriendService _friendService = null!;

    private static readonly Guid RequesterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AddresseeId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [SetUp]
    public async Task SetUp()
    {
        _databaseContext = TestSocialDatabaseFactory.CreateInMemory();
        _eventPublisher = new RecordingSocialEventPublisher();
        _friendService = new FriendService(_databaseContext, _eventPublisher);

        await TestSocialDatabaseFactory.SeedUserAsync(_databaseContext, RequesterId, "Requester");
        await TestSocialDatabaseFactory.SeedUserAsync(_databaseContext, AddresseeId, "Addressee");
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    [Test]
    public async Task SendFriendRequestAsync_creates_pending_friendship_and_emits_received_event()
    {
        var friendship = await _friendService.SendFriendRequestAsync(RequesterId, AddresseeId);

        friendship.Status.Should().Be(FriendshipStatus.Pending);
        _eventPublisher.FriendRequestReceivedEvents.Should().HaveCount(1);

        var emitted = _eventPublisher.FriendRequestReceivedEvents[0];
        emitted.RecipientId.Should().Be(AddresseeId);
        emitted.RequesterName.Should().Be("Requester");
        emitted.RequesterId.Should().Be(RequesterId);
        emitted.FriendshipId.Should().Be(friendship.Id);
    }

    [Test]
    public async Task SendFriendRequestAsync_to_self_throws()
    {
        var action = async () => await _friendService.SendFriendRequestAsync(RequesterId, RequesterId);
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task SendFriendRequestAsync_to_unknown_user_throws_not_found()
    {
        var action = async () => await _friendService.SendFriendRequestAsync(RequesterId, Guid.NewGuid());
        await action.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Test]
    public async Task SendFriendRequestAsync_when_pending_already_exists_throws()
    {
        await _friendService.SendFriendRequestAsync(RequesterId, AddresseeId);

        var action = async () => await _friendService.SendFriendRequestAsync(RequesterId, AddresseeId);
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task AcceptFriendRequestAsync_by_addressee_marks_accepted_and_emits_accepted_event()
    {
        var friendship = await _friendService.SendFriendRequestAsync(RequesterId, AddresseeId);

        await _friendService.AcceptFriendRequestAsync(AddresseeId, friendship.Id);

        var stored = await _databaseContext.Friendships.FindAsync(friendship.Id);
        stored!.Status.Should().Be(FriendshipStatus.Accepted);
        stored.AcceptedAt.Should().NotBeNull();

        _eventPublisher.FriendRequestAcceptedEvents.Should().HaveCount(1);
        var emitted = _eventPublisher.FriendRequestAcceptedEvents[0];
        emitted.RecipientId.Should().Be(RequesterId);
        emitted.AccepterName.Should().Be("Addressee");
        emitted.AccepterId.Should().Be(AddresseeId);
    }

    [Test]
    public async Task AcceptFriendRequestAsync_by_non_addressee_throws()
    {
        var friendship = await _friendService.SendFriendRequestAsync(RequesterId, AddresseeId);

        var action = async () => await _friendService.AcceptFriendRequestAsync(RequesterId, friendship.Id);
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task DeclineFriendRequestAsync_marks_declined_and_emits_no_event()
    {
        var friendship = await _friendService.SendFriendRequestAsync(RequesterId, AddresseeId);

        await _friendService.DeclineFriendRequestAsync(AddresseeId, friendship.Id);

        var stored = await _databaseContext.Friendships.FindAsync(friendship.Id);
        stored!.Status.Should().Be(FriendshipStatus.Declined);
        _eventPublisher.FriendRequestAcceptedEvents.Should().BeEmpty();
    }

    [Test]
    public async Task SendFriendRequestAsync_after_decline_revives_pending_and_emits_event_again()
    {
        var friendship = await _friendService.SendFriendRequestAsync(RequesterId, AddresseeId);
        await _friendService.DeclineFriendRequestAsync(AddresseeId, friendship.Id);

        var revived = await _friendService.SendFriendRequestAsync(RequesterId, AddresseeId);

        revived.Id.Should().Be(friendship.Id);
        revived.Status.Should().Be(FriendshipStatus.Pending);
        _eventPublisher.FriendRequestReceivedEvents.Should().HaveCount(2);
    }

    [Test]
    public async Task GetFriendsAsync_returns_accepted_friends_with_replica_identity()
    {
        var friendship = await _friendService.SendFriendRequestAsync(RequesterId, AddresseeId);
        await _friendService.AcceptFriendRequestAsync(AddresseeId, friendship.Id);

        var friends = await _friendService.GetFriendsAsync(RequesterId);

        friends.Should().HaveCount(1);
        friends[0].UserId.Should().Be(AddresseeId);
        friends[0].DisplayName.Should().Be("Addressee");
        friends[0].AvatarUrl.Should().Be($"/avatars/{AddresseeId}");
        friends[0].TotalXpAmount.Should().Be(0);
    }

    [Test]
    public async Task RemoveFriendAsync_deletes_accepted_friendship()
    {
        var friendship = await _friendService.SendFriendRequestAsync(RequesterId, AddresseeId);
        await _friendService.AcceptFriendRequestAsync(AddresseeId, friendship.Id);

        await _friendService.RemoveFriendAsync(RequesterId, AddresseeId);

        _databaseContext.Friendships.Should().BeEmpty();
    }

    [Test]
    public async Task GetPendingRequestsAsync_reports_direction()
    {
        await _friendService.SendFriendRequestAsync(RequesterId, AddresseeId);

        var outgoing = await _friendService.GetPendingRequestsAsync(RequesterId);
        var incoming = await _friendService.GetPendingRequestsAsync(AddresseeId);

        outgoing.Should().ContainSingle(request => request.Direction == "outgoing");
        incoming.Should().ContainSingle(request => request.Direction == "incoming");
    }

    [Test]
    public async Task GetPublicProfileAsync_uses_replica_and_resolves_status()
    {
        var friendship = await _friendService.SendFriendRequestAsync(RequesterId, AddresseeId);
        await _friendService.AcceptFriendRequestAsync(AddresseeId, friendship.Id);

        var profile = await _friendService.GetPublicProfileAsync(RequesterId, AddresseeId);

        profile.DisplayName.Should().Be("Addressee");
        profile.FriendshipStatus.Should().Be("friends");
        profile.TotalXpAmount.Should().Be(0);
        profile.AverageExerciseScore.Should().Be(0.0);
    }
}
