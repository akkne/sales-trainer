using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.Gamification.Eventing;

namespace Sellevate.Gamification.Tests.Unit;

[TestFixture]
public sealed class GamificationOutboxTests
{
    [Test]
    public async Task Publisher_EnqueuesAnOutboxRowThatTheStoreReadsBackAsPending()
    {
        await using var databaseContext = GamificationDbContextFactory.CreateInMemory();
        var writer = new GamificationOutboxWriter(databaseContext);
        var publisher = new KafkaGamificationEventPublisher(writer);
        var userId = Guid.NewGuid();

        await publisher.PublishExperiencePointsGrantedAsync(new ExperiencePointsGrantedEvent(userId, 40, "exercise"));
        await databaseContext.SaveChangesAsync();

        var store = new GamificationOutboxStore(databaseContext);
        var pending = await store.GetPendingAsync(10);

        pending.Should().ContainSingle();
        pending[0].Topic.Should().Be(Topics.XpGranted);
        pending[0].PartitionKey.Should().Be(userId.ToString());
        pending[0].DispatchedAt.Should().BeNull();
    }

    [Test]
    public async Task RelayProcessor_ForwardsThePendingRowAndMarksItDispatched()
    {
        await using var databaseContext = GamificationDbContextFactory.CreateInMemory();
        var writer = new GamificationOutboxWriter(databaseContext);
        var publisher = new KafkaGamificationEventPublisher(writer);
        await publisher.PublishAchievementUnlockedAsync(new AchievementUnlockedEvent(Guid.NewGuid(), "first_lesson", "First step"));
        await databaseContext.SaveChangesAsync();

        var store = new GamificationOutboxStore(databaseContext);
        var forwarder = new RecordingForwarder();
        var processor = new OutboxRelayProcessor(store, forwarder, NullLogger.Instance);

        var dispatched = await processor.DispatchPendingAsync(10, CancellationToken.None);

        dispatched.Should().Be(1);
        forwarder.Topics.Should().Equal(Topics.AchievementUnlocked);
        (await store.GetPendingAsync(10)).Should().BeEmpty();
    }

    [Test]
    public async Task EnqueuedPayload_IsAValidEventEnvelopeForTheConsumerContract()
    {
        await using var databaseContext = GamificationDbContextFactory.CreateInMemory();
        var writer = new GamificationOutboxWriter(databaseContext);
        var publisher = new KafkaGamificationEventPublisher(writer);
        var userId = Guid.NewGuid();
        await publisher.PublishStreakMilestoneAsync(new StreakMilestoneEvent(userId, 7, 50));
        await databaseContext.SaveChangesAsync();

        var store = new GamificationOutboxStore(databaseContext);
        var pending = await store.GetPendingAsync(10);
        var envelope = System.Text.Json.JsonSerializer.Deserialize<EventEnvelope>(pending[0].Payload, EventEnvelope.JsonOptions);

        envelope.Should().NotBeNull();
        envelope!.Type.Should().Be(Topics.StreakMilestone);
        var streakMilestone = envelope.DataAs<StreakMilestoneEvent>();
        streakMilestone!.UserId.Should().Be(userId);
        streakMilestone.DayCount.Should().Be(7);
        streakMilestone.BonusXp.Should().Be(50);
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
