using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.Learning.Eventing;

namespace Sellevate.Learning.Tests.Unit;

[TestFixture]
public sealed class LearningOutboxTests
{
    [Test]
    public async Task Publisher_EnqueuesAnOutboxRowThatTheStoreReadsBackAsPending()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();
        var writer = new LearningOutboxWriter(databaseContext);
        var publisher = new KafkaLearningEventPublisher(writer);
        var userId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();

        await publisher.PublishExerciseCompletedAsync(new ExerciseCompletedEvent(userId, "choose_option", 100, true));
        await databaseContext.SaveChangesAsync();

        var store = new LearningOutboxStore(databaseContext);
        var pending = await store.GetPendingAsync(10);

        pending.Should().ContainSingle();
        pending[0].Topic.Should().Be(Topics.ExerciseCompleted);
        pending[0].PartitionKey.Should().Be(userId.ToString());
        pending[0].DispatchedAt.Should().BeNull();
    }

    [Test]
    public async Task RelayProcessor_ForwardsThePendingRowAndMarksItDispatched()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();
        var writer = new LearningOutboxWriter(databaseContext);
        var publisher = new KafkaLearningEventPublisher(writer);
        var lessonId = Guid.NewGuid();
        await publisher.PublishLessonCompletedAsync(new LessonCompletedEvent(Guid.NewGuid(), lessonId, 80));
        await databaseContext.SaveChangesAsync();

        var store = new LearningOutboxStore(databaseContext);
        var forwarder = new RecordingForwarder();
        var processor = new OutboxRelayProcessor(store, forwarder, NullLogger.Instance);

        var dispatched = await processor.DispatchPendingAsync(10, CancellationToken.None);

        dispatched.Should().Be(1);
        forwarder.Topics.Should().Equal(Topics.LessonCompleted);
        (await store.GetPendingAsync(10)).Should().BeEmpty();
    }

    [Test]
    public async Task EnqueuedPayload_IsAValidEventEnvelopeForTheConsumerContract()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();
        var writer = new LearningOutboxWriter(databaseContext);
        var publisher = new KafkaLearningEventPublisher(writer);
        var userId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        await publisher.PublishSkillCompletedAsync(new SkillCompletedEvent(userId, skillId));
        await databaseContext.SaveChangesAsync();

        var store = new LearningOutboxStore(databaseContext);
        var pending = await store.GetPendingAsync(10);
        var envelope = System.Text.Json.JsonSerializer.Deserialize<EventEnvelope>(pending[0].Payload, EventEnvelope.JsonOptions);

        envelope.Should().NotBeNull();
        envelope!.Type.Should().Be(Topics.SkillCompleted);
        var skillCompleted = envelope.DataAs<SkillCompletedEvent>();
        skillCompleted!.UserId.Should().Be(userId);
        skillCompleted.SkillId.Should().Be(skillId);
    }

    [Test]
    public async Task MarkDispatchedAsync_ClearsThePendingRow()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();
        var writer = new LearningOutboxWriter(databaseContext);
        var publisher = new KafkaLearningEventPublisher(writer);
        await publisher.PublishExerciseCompletedAsync(new ExerciseCompletedEvent(Guid.NewGuid(), "reorder", 75, true));
        await databaseContext.SaveChangesAsync();

        var store = new LearningOutboxStore(databaseContext);
        var pending = await store.GetPendingAsync(10);
        pending.Should().ContainSingle();

        await store.MarkDispatchedAsync(pending[0]);

        (await store.GetPendingAsync(10)).Should().BeEmpty();
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
