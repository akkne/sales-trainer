using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;

namespace Sellevate.BuildingBlocks.Tests;

[TestFixture]
public sealed class EventMessageProcessorTests
{
    private const string ConsumerGroup = "test-service";
    private const string Topic = "exercise.completed";

    [Test]
    public async Task ProcessAsync_WhenHandlerSucceeds_MarksProcessedAndCommits()
    {
        var idempotencyStore = new FakeIdempotencyStore();
        var deadLetterPublisher = new FakeDeadLetterPublisher();
        var processor = NewProcessor(idempotencyStore, deadLetterPublisher, NewResilience(maxRetries: 3));
        var raw = SerializeEnvelope();

        var outcome = await processor.ProcessAsync(
            Topic, "user-1", raw, (_, _) => Task.CompletedTask, CancellationToken.None);

        outcome.Should().Be(MessageProcessingOutcome.Commit);
        idempotencyStore.Marked.Should().HaveCount(1);
        deadLetterPublisher.Published.Should().BeEmpty();
    }

    [Test]
    public async Task ProcessAsync_RetriesTheHandlerUpToMaxAttemptsBeforeGivingUp()
    {
        var idempotencyStore = new FakeIdempotencyStore();
        var deadLetterPublisher = new FakeDeadLetterPublisher();
        var processor = NewProcessor(idempotencyStore, deadLetterPublisher, NewResilience(maxRetries: 2));
        var raw = SerializeEnvelope();
        var attempts = 0;

        await processor.ProcessAsync(
            Topic, "user-1", raw,
            (_, _) =>
            {
                attempts++;
                throw new InvalidOperationException("boom");
            },
            CancellationToken.None);

        attempts.Should().Be(3);
    }

    [Test]
    public async Task ProcessAsync_WhenHandlerKeepsFailing_DeadLettersToDltTopicAndCommits()
    {
        var idempotencyStore = new FakeIdempotencyStore();
        var deadLetterPublisher = new FakeDeadLetterPublisher();
        var processor = NewProcessor(idempotencyStore, deadLetterPublisher, NewResilience(maxRetries: 1));
        var raw = SerializeEnvelope();

        var outcome = await processor.ProcessAsync(
            Topic, "user-1", raw,
            (_, _) => throw new InvalidOperationException("poison"),
            CancellationToken.None);

        outcome.Should().Be(MessageProcessingOutcome.DeadLettered);
        deadLetterPublisher.Published.Should().ContainSingle();
        deadLetterPublisher.Published[0].Topic.Should().Be("exercise.completed.dlt");
        deadLetterPublisher.Published[0].RawValue.Should().Be(raw);
        deadLetterPublisher.Published[0].FailureReason.Should().Be("poison");
        idempotencyStore.Marked.Should().HaveCount(1);
    }

    [Test]
    public async Task ProcessAsync_WhenDeadLetteringDisabled_RequestsRedeliveryAndDoesNotPublish()
    {
        var idempotencyStore = new FakeIdempotencyStore();
        var deadLetterPublisher = new FakeDeadLetterPublisher();
        var processor = NewProcessor(idempotencyStore, deadLetterPublisher, NewResilience(maxRetries: 0, deadLetterEnabled: false));
        var raw = SerializeEnvelope();

        var outcome = await processor.ProcessAsync(
            Topic, "user-1", raw,
            (_, _) => throw new InvalidOperationException("still failing"),
            CancellationToken.None);

        outcome.Should().Be(MessageProcessingOutcome.Redeliver);
        deadLetterPublisher.Published.Should().BeEmpty();
        idempotencyStore.Marked.Should().BeEmpty();
    }

    [Test]
    public async Task ProcessAsync_WhenAlreadyProcessed_SkipsHandlerAndCommits()
    {
        var idempotencyStore = new FakeIdempotencyStore();
        var deadLetterPublisher = new FakeDeadLetterPublisher();
        var processor = NewProcessor(idempotencyStore, deadLetterPublisher, NewResilience(maxRetries: 3));
        var envelope = EventEnvelope.Create("exercise.completed", new { userId = Guid.NewGuid() });
        await idempotencyStore.MarkProcessedAsync(ConsumerGroup, envelope.EventId);
        var raw = JsonSerializer.Serialize(envelope, EventEnvelope.JsonOptions);
        var handlerCalled = false;

        var outcome = await processor.ProcessAsync(
            Topic, "user-1", raw, (_, _) => { handlerCalled = true; return Task.CompletedTask; }, CancellationToken.None);

        outcome.Should().Be(MessageProcessingOutcome.Commit);
        handlerCalled.Should().BeFalse();
    }

    [Test]
    public async Task ProcessAsync_WhenMessageUnparseable_CommitsWithoutHandling()
    {
        var idempotencyStore = new FakeIdempotencyStore();
        var deadLetterPublisher = new FakeDeadLetterPublisher();
        var processor = NewProcessor(idempotencyStore, deadLetterPublisher, NewResilience(maxRetries: 3));

        var outcome = await processor.ProcessAsync(
            Topic, "user-1", "{ not valid json", (_, _) => Task.CompletedTask, CancellationToken.None);

        outcome.Should().Be(MessageProcessingOutcome.Commit);
        deadLetterPublisher.Published.Should().BeEmpty();
    }

    private static EventMessageProcessor NewProcessor(
        IIdempotencyStore idempotencyStore,
        IDeadLetterPublisher deadLetterPublisher,
        ConsumerResilienceSettings resilience)
        => new(ConsumerGroup, idempotencyStore, deadLetterPublisher, resilience, NullLogger.Instance);

    private static ConsumerResilienceSettings NewResilience(int maxRetries, bool deadLetterEnabled = true)
        => new()
        {
            MaxHandlerRetries = maxRetries,
            RetryDelayMilliseconds = 0,
            DeadLetterEnabled = deadLetterEnabled,
        };

    private static string SerializeEnvelope()
    {
        var envelope = EventEnvelope.Create("exercise.completed", new { userId = Guid.NewGuid(), score = 80 });
        return JsonSerializer.Serialize(envelope, EventEnvelope.JsonOptions);
    }

    private sealed class FakeIdempotencyStore : IIdempotencyStore
    {
        private readonly HashSet<string> _seen = [];

        public List<Guid> Marked { get; } = [];

        public Task<bool> HasProcessedAsync(string consumerGroup, Guid eventId, CancellationToken cancellationToken = default)
            => Task.FromResult(_seen.Contains($"{consumerGroup}:{eventId}"));

        public Task MarkProcessedAsync(string consumerGroup, Guid eventId, CancellationToken cancellationToken = default)
        {
            _seen.Add($"{consumerGroup}:{eventId}");
            Marked.Add(eventId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDeadLetterPublisher : IDeadLetterPublisher
    {
        public List<(string Topic, string Key, string RawValue, string FailureReason)> Published { get; } = [];

        public Task PublishAsync(
            string deadLetterTopic,
            string partitionKey,
            string rawValue,
            string failureReason,
            CancellationToken cancellationToken = default)
        {
            Published.Add((deadLetterTopic, partitionKey, rawValue, failureReason));
            return Task.CompletedTask;
        }
    }
}
