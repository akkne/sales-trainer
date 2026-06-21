using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;

namespace Sellevate.BuildingBlocks.Messaging;

/// <summary>
/// The Kafka-agnostic core of the idempotent consumer: parse the envelope, dedupe on
/// <see cref="EventEnvelope.EventId"/>, run the handler with a bounded in-process retry, and
/// on exhaustion route the poison message to its dead-letter topic. Returns a
/// <see cref="MessageProcessingOutcome"/> so the surrounding consume loop owns the offset
/// commit. Free of Confluent types, so it can be unit-tested with fakes.
/// </summary>
public sealed class EventMessageProcessor
{
    private readonly string _consumerGroupId;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IDeadLetterPublisher _deadLetterPublisher;
    private readonly ConsumerResilienceSettings _resilience;
    private readonly ILogger _logger;

    public EventMessageProcessor(
        string consumerGroupId,
        IIdempotencyStore idempotencyStore,
        IDeadLetterPublisher deadLetterPublisher,
        ConsumerResilienceSettings resilience,
        ILogger logger)
    {
        _consumerGroupId = consumerGroupId;
        _idempotencyStore = idempotencyStore;
        _deadLetterPublisher = deadLetterPublisher;
        _resilience = resilience;
        _logger = logger;
    }

    public async Task<MessageProcessingOutcome> ProcessAsync(
        string topic,
        string? key,
        string rawValue,
        Func<EventEnvelope, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        EventEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<EventEnvelope>(rawValue, EventEnvelope.JsonOptions);
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "Skipping unparseable message on {Topic}", topic);
            return MessageProcessingOutcome.Commit;
        }

        if (envelope is null)
        {
            return MessageProcessingOutcome.Commit;
        }

        if (await _idempotencyStore.HasProcessedAsync(_consumerGroupId, envelope.EventId, cancellationToken))
        {
            _logger.LogDebug(
                "Skipping duplicate event {EventId} ({Type}) in group '{Group}'",
                envelope.EventId, envelope.Type, _consumerGroupId);
            return MessageProcessingOutcome.Commit;
        }

        var handlerError = await RunHandlerWithRetriesAsync(envelope, handler, cancellationToken);
        if (handlerError is null)
        {
            await _idempotencyStore.MarkProcessedAsync(_consumerGroupId, envelope.EventId, cancellationToken);
            return MessageProcessingOutcome.Commit;
        }

        return await DeadLetterAsync(topic, key, rawValue, envelope, handlerError, cancellationToken);
    }

    private async Task<Exception?> RunHandlerWithRetriesAsync(
        EventEnvelope envelope,
        Func<EventEnvelope, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(0, _resilience.MaxHandlerRetries) + 1;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await handler(envelope, cancellationToken);
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (attempt >= maxAttempts)
                {
                    return exception;
                }

                _logger.LogWarning(
                    exception, "Handler failed for event {EventId} ({Type}), attempt {Attempt}/{MaxAttempts}; retrying",
                    envelope.EventId, envelope.Type, attempt, maxAttempts);

                var delay = TimeSpan.FromMilliseconds(Math.Max(0, _resilience.RetryDelayMilliseconds) * attempt);
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        return null;
    }

    private async Task<MessageProcessingOutcome> DeadLetterAsync(
        string topic,
        string? key,
        string rawValue,
        EventEnvelope envelope,
        Exception handlerError,
        CancellationToken cancellationToken)
    {
        if (!_resilience.DeadLetterEnabled)
        {
            _logger.LogError(
                handlerError, "Handler exhausted retries for event {EventId} ({Type}); dead-lettering disabled, will redeliver",
                envelope.EventId, envelope.Type);
            return MessageProcessingOutcome.Redeliver;
        }

        var deadLetterTopic = Topics.DeadLetterFor(topic);

        try
        {
            await _deadLetterPublisher.PublishAsync(
                deadLetterTopic,
                key ?? envelope.EventId.ToString(),
                rawValue,
                handlerError.Message,
                cancellationToken);

            await _idempotencyStore.MarkProcessedAsync(_consumerGroupId, envelope.EventId, cancellationToken);

            _logger.LogError(
                handlerError, "Dead-lettered event {EventId} ({Type}) to {DeadLetterTopic} after exhausting retries",
                envelope.EventId, envelope.Type, deadLetterTopic);

            return MessageProcessingOutcome.DeadLettered;
        }
        catch (Exception deadLetterError)
        {
            _logger.LogError(
                deadLetterError, "Failed to dead-letter event {EventId} ({Type}) to {DeadLetterTopic}; will redeliver",
                envelope.EventId, envelope.Type, deadLetterTopic);
            return MessageProcessingOutcome.Redeliver;
        }
    }
}
