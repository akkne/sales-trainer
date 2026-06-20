using FluentAssertions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.Notification.Eventing;
using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Tests.Unit;

[TestFixture]
public class NotificationEventMapperTests
{
    private readonly NotificationEventMapper _mapper = new();

    [Test]
    public void Map_AchievementUnlocked_ProducesAchievementNotification()
    {
        var userId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(
            Topics.AchievementUnlocked,
            new AchievementUnlockedEvent(userId, "first-deal", "First Deal Closed"));

        var request = _mapper.Map(envelope);

        request.Should().NotBeNull();
        request!.RecipientUserId.Should().Be(userId);
        request.NotificationType.Should().Be(NotificationType.AchievementUnlocked);
        request.Body.Should().Be("First Deal Closed");
        request.ActionUrl.Should().Be("/profile");
        request.RelatedEntityId.Should().Be("first-deal");
    }

    [Test]
    public void Map_StreakMilestone_IncludesBonusXpWhenPresent()
    {
        var userId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(
            Topics.StreakMilestone,
            new StreakMilestoneEvent(userId, 7, 50));

        var request = _mapper.Map(envelope);

        request.Should().NotBeNull();
        request!.NotificationType.Should().Be(NotificationType.StreakMilestone);
        request.Body.Should().Contain("7-day");
        request.Body.Should().Contain("50");
    }

    [Test]
    public void Map_FriendRequestReceived_LinksToRequestsTab()
    {
        var recipientId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(
            Topics.FriendRequestReceived,
            new FriendRequestReceivedEvent(recipientId, "Dana", Guid.NewGuid(), Guid.NewGuid()));

        var request = _mapper.Map(envelope);

        request.Should().NotBeNull();
        request!.RecipientUserId.Should().Be(recipientId);
        request.NotificationType.Should().Be(NotificationType.FriendRequestReceived);
        request.ActionUrl.Should().Be("/friends?tab=requests");
        request.Body.Should().Contain("Dana");
    }

    [Test]
    public void Map_FriendRequestAccepted_LinksToAccepterProfileWhenIdPresent()
    {
        var recipientId = Guid.NewGuid();
        var accepterId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(
            Topics.FriendRequestAccepted,
            new FriendRequestAcceptedEvent(recipientId, "Max", accepterId));

        var request = _mapper.Map(envelope);

        request.Should().NotBeNull();
        request!.NotificationType.Should().Be(NotificationType.FriendRequestAccepted);
        request.ActionUrl.Should().Be($"/friends/{accepterId}");
    }

    [Test]
    public void Map_ChatMessageSent_LinksToConversationAndTruncatesPreview()
    {
        var recipientId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var longPreview = new string('a', 400);
        var envelope = EventEnvelope.Create(
            Topics.ChatMessageSent,
            new ChatMessageSentEvent(recipientId, "Lena", longPreview, conversationId));

        var request = _mapper.Map(envelope);

        request.Should().NotBeNull();
        request!.NotificationType.Should().Be(NotificationType.ChatMessageReceived);
        request.ActionUrl.Should().Be($"/friends/chat/{conversationId}");
        request.Body.Length.Should().BeLessThanOrEqualTo("Lena: ".Length + 160);
    }

    [Test]
    public void Map_UnknownEventType_ReturnsNull()
    {
        var envelope = EventEnvelope.Create("some.unrelated.event", new { value = 1 });

        var request = _mapper.Map(envelope);

        request.Should().BeNull();
    }

    [Test]
    public void Map_FriendRequestReceived_WithBlankRequesterName_ReturnsNull()
    {
        var envelope = EventEnvelope.Create(
            Topics.FriendRequestReceived,
            new FriendRequestReceivedEvent(Guid.NewGuid(), "  ", null, null));

        var request = _mapper.Map(envelope);

        request.Should().BeNull();
    }
}
