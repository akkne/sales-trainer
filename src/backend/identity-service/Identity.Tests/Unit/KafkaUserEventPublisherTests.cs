using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Unit;

[TestFixture]
public sealed class KafkaUserEventPublisherTests
{
    [Test]
    public async Task PublishRegistered_EnqueuesAnOutboxRowThatTheStoreReadsBackAsPending()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var writer = new IdentityOutboxWriter(databaseContext);
        var publisher = new KafkaUserEventPublisher(writer);
        var userId = Guid.NewGuid();

        await publisher.PublishRegisteredAsync(new UserRegisteredEvent(userId, "a@b.com", "Alice", null));
        await databaseContext.SaveChangesAsync();

        var store = new IdentityOutboxStore(databaseContext);
        var pending = await store.GetPendingAsync(10);

        pending.Should().ContainSingle();
        pending[0].Topic.Should().Be(Topics.UserRegistered);
        pending[0].PartitionKey.Should().Be(userId.ToString());
        pending[0].DispatchedAt.Should().BeNull();
    }

    [Test]
    public async Task PublishAvatarChanged_EnqueuesWithAvatarChangedTopic()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var writer = new IdentityOutboxWriter(databaseContext);
        var publisher = new KafkaUserEventPublisher(writer);
        var userId = Guid.NewGuid();

        await publisher.PublishAvatarChangedAsync(new UserAvatarChangedEvent(userId, "k"));
        await databaseContext.SaveChangesAsync();

        var store = new IdentityOutboxStore(databaseContext);
        var pending = await store.GetPendingAsync(10);

        pending.Should().ContainSingle();
        pending[0].Topic.Should().Be(Topics.UserAvatarChanged);
        pending[0].PartitionKey.Should().Be(userId.ToString());
    }

    [Test]
    public async Task RelayProcessor_ForwardsThePendingRowAndMarksItDispatched()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var writer = new IdentityOutboxWriter(databaseContext);
        var publisher = new KafkaUserEventPublisher(writer);
        await publisher.PublishRegisteredAsync(new UserRegisteredEvent(Guid.NewGuid(), "x@y.com", "Bob", null));
        await databaseContext.SaveChangesAsync();

        var store = new IdentityOutboxStore(databaseContext);
        var forwarder = new RecordingForwarder();
        var processor = new OutboxRelayProcessor(store, forwarder, NullLogger.Instance);

        var dispatched = await processor.DispatchPendingAsync(10, CancellationToken.None);

        dispatched.Should().Be(1);
        forwarder.Topics.Should().Equal(Topics.UserRegistered);
        (await store.GetPendingAsync(10)).Should().BeEmpty();
    }

    [Test]
    public async Task EnqueuedPayload_IsAValidEventEnvelopeWithCorrectData()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var writer = new IdentityOutboxWriter(databaseContext);
        var publisher = new KafkaUserEventPublisher(writer);
        var userId = Guid.NewGuid();
        await publisher.PublishRegisteredAsync(new UserRegisteredEvent(userId, "test@example.com", "Test User", null));
        await databaseContext.SaveChangesAsync();

        var store = new IdentityOutboxStore(databaseContext);
        var pending = await store.GetPendingAsync(10);
        var envelope = System.Text.Json.JsonSerializer.Deserialize<EventEnvelope>(pending[0].Payload, EventEnvelope.JsonOptions);

        envelope.Should().NotBeNull();
        envelope!.Type.Should().Be(Topics.UserRegistered);
        var registeredEvent = envelope.DataAs<UserRegisteredEvent>();
        registeredEvent!.UserId.Should().Be(userId);
        registeredEvent.Email.Should().Be("test@example.com");
    }

    [Test]
    public async Task OutboxRoundTrip_EnqueueGetPendingMarkDispatched()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var writer = new IdentityOutboxWriter(databaseContext);
        var store = new IdentityOutboxStore(databaseContext);
        var userId = Guid.NewGuid();

        writer.Enqueue(Topics.UserUpdated, userId.ToString(), Topics.UserUpdated, new UserUpdatedEvent(userId, "Alice", null));
        await databaseContext.SaveChangesAsync();

        var pending = await store.GetPendingAsync(10);
        pending.Should().ContainSingle();

        await store.MarkDispatchedAsync(pending[0]);

        var afterDispatch = await store.GetPendingAsync(10);
        afterDispatch.Should().BeEmpty();
    }

    private sealed class RecordingForwarder : IOutboxEventForwarder
    {
        public List<string> Topics { get; } = [];

        public Task ForwardAsync(string topic, string partitionKey, string payload, CancellationToken cancellationToken = default)
        {
            Topics.Add(topic);
            return Task.CompletedTask;
        }
    }
}
