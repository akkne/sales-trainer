using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.Social.Eventing;

namespace Sellevate.Social.Tests.Unit;

[TestFixture]
public sealed class SocialEventContractTests
{
    [Test]
    public void FriendRequestReceived_envelope_carries_consumer_fields()
    {
        var recipientId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var friendshipId = Guid.NewGuid();

        var envelope = EventEnvelope.Create(
            Topics.FriendRequestReceived,
            new FriendRequestReceivedEvent(recipientId, "Dana", requesterId, friendshipId));

        var json = JsonSerializer.Serialize(envelope.Data, EventEnvelope.JsonOptions);
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        envelope.Type.Should().Be("friend.request.received");
        root.GetProperty("recipientId").GetGuid().Should().Be(recipientId);
        root.GetProperty("requesterName").GetString().Should().Be("Dana");
        root.GetProperty("requesterId").GetGuid().Should().Be(requesterId);
        root.GetProperty("friendshipId").GetGuid().Should().Be(friendshipId);
    }

    [Test]
    public void FriendRequestAccepted_envelope_carries_consumer_fields()
    {
        var recipientId = Guid.NewGuid();
        var accepterId = Guid.NewGuid();

        var envelope = EventEnvelope.Create(
            Topics.FriendRequestAccepted,
            new FriendRequestAcceptedEvent(recipientId, "Sam", accepterId));

        var json = JsonSerializer.Serialize(envelope.Data, EventEnvelope.JsonOptions);
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        envelope.Type.Should().Be("friend.request.accepted");
        root.GetProperty("recipientId").GetGuid().Should().Be(recipientId);
        root.GetProperty("accepterName").GetString().Should().Be("Sam");
        root.GetProperty("accepterId").GetGuid().Should().Be(accepterId);
    }

    [Test]
    public void ChatMessageSent_envelope_carries_consumer_fields()
    {
        var recipientId = Guid.NewGuid();

        var envelope = EventEnvelope.Create(
            Topics.ChatMessageSent,
            new ChatMessageSentEvent(recipientId, "Alex", "Hello there", null));

        var json = JsonSerializer.Serialize(envelope.Data, EventEnvelope.JsonOptions);
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        envelope.Type.Should().Be("chat.message.sent");
        root.GetProperty("recipientId").GetGuid().Should().Be(recipientId);
        root.GetProperty("senderName").GetString().Should().Be("Alex");
        root.GetProperty("preview").GetString().Should().Be("Hello there");
        root.GetProperty("conversationId").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
