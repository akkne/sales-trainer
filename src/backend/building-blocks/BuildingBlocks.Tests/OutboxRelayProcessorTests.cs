using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Outbox;

namespace Sellevate.BuildingBlocks.Tests;

[TestFixture]
public sealed class OutboxRelayProcessorTests
{
    [Test]
    public async Task DispatchPendingAsync_ForwardsEachPendingMessageAndMarksItDispatched()
    {
        var store = new FakeOutboxStore(
            NewMessage("xp.granted"),
            NewMessage("achievement.unlocked"));
        var forwarder = new FakeForwarder();
        var processor = new OutboxRelayProcessor(store, forwarder, NullLogger.Instance);

        var dispatched = await processor.DispatchPendingAsync(50, CancellationToken.None);

        dispatched.Should().Be(2);
        forwarder.Forwarded.Select(forwarded => forwarded.Topic).Should().Equal("xp.granted", "achievement.unlocked");
        store.Pending.Should().BeEmpty();
    }

    [Test]
    public async Task DispatchPendingAsync_StopsAtFirstForwardFailureLeavingTheRestPending()
    {
        var store = new FakeOutboxStore(
            NewMessage("xp.granted"),
            NewMessage("achievement.unlocked"));
        var forwarder = new FakeForwarder { FailOnTopic = "achievement.unlocked" };
        var processor = new OutboxRelayProcessor(store, forwarder, NullLogger.Instance);

        var dispatched = await processor.DispatchPendingAsync(50, CancellationToken.None);

        dispatched.Should().Be(1);
        store.Pending.Should().ContainSingle().Which.Topic.Should().Be("achievement.unlocked");
    }

    [Test]
    public async Task DispatchPendingAsync_ForwardsTheStoredPayloadVerbatim()
    {
        var message = NewMessage("xp.granted");
        message.Payload = "{\"eventId\":\"x\",\"type\":\"xp.granted\"}";
        message.PartitionKey = "user-7";
        var store = new FakeOutboxStore(message);
        var forwarder = new FakeForwarder();
        var processor = new OutboxRelayProcessor(store, forwarder, NullLogger.Instance);

        await processor.DispatchPendingAsync(50, CancellationToken.None);

        forwarder.Forwarded.Should().ContainSingle();
        forwarder.Forwarded[0].Payload.Should().Be(message.Payload);
        forwarder.Forwarded[0].Key.Should().Be("user-7");
    }

    private static OutboxMessage NewMessage(string topic) => new()
    {
        Id = Guid.NewGuid(),
        Topic = topic,
        PartitionKey = "user-1",
        Payload = "{}",
        OccurredAt = DateTimeOffset.UtcNow,
    };

    private sealed class FakeOutboxStore : IOutboxStore
    {
        public FakeOutboxStore(params OutboxMessage[] messages)
        {
            Pending = messages.ToList();
        }

        public List<OutboxMessage> Pending { get; }

        public Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OutboxMessage>>(Pending.Take(batchSize).ToList());

        public Task MarkDispatchedAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            message.DispatchedAt = DateTimeOffset.UtcNow;
            Pending.Remove(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeForwarder : IOutboxEventForwarder
    {
        public string? FailOnTopic { get; init; }

        public List<(string Topic, string Key, string Payload)> Forwarded { get; } = [];

        public Task ForwardAsync(string topic, string partitionKey, string payload, CancellationToken cancellationToken = default)
        {
            if (topic == FailOnTopic)
            {
                throw new InvalidOperationException("broker down");
            }

            Forwarded.Add((topic, partitionKey, payload));
            return Task.CompletedTask;
        }
    }
}
