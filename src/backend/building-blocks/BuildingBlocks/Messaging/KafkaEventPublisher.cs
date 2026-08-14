using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;

namespace Sellevate.BuildingBlocks.Messaging;

/// <summary>
/// Kafka-backed <see cref="IEventPublisher"/>. Serializes the
/// <see cref="EventEnvelope"/> to JSON and produces it with idempotent, acks=all
/// settings so a producer retry never duplicates or loses a message broker-side.
/// Registered as a singleton — the underlying producer is thread-safe and pools
/// its broker connections.
/// </summary>
public sealed class KafkaEventPublisher : IEventPublisher, IDeadLetterPublisher, IOutboxEventForwarder, IDisposable
{
    private const string DeadLetterReasonHeader = "x-dead-letter-reason";
    private const string DeadLetterAtHeader = "x-dead-letter-at";

    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;
    private readonly TimeSpan _publishTimeout;

    public KafkaEventPublisher(IOptions<KafkaSettings> settings, ILogger<KafkaEventPublisher> logger)
    {
        _logger = logger;
        _publishTimeout = TimeSpan.FromSeconds(Math.Max(1, settings.Value.PublishTimeoutSeconds));
        var config = new ProducerConfig
        {
            BootstrapServers = settings.Value.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            // Without this librdkafka retries an unreachable broker for 5 minutes before it
            // reports the failure, and every producer call inherits that as its worst case.
            MessageTimeoutMs = (int)_publishTimeout.TotalMilliseconds,
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    /// <summary>
    /// Publishes a domain event. Delivery is **best-effort and does not block the caller**: the
    /// message is queued locally (ordering per partition is preserved by the producer) and the
    /// delivery report is logged when it arrives. The state change the event announces is already
    /// committed, so an unreachable broker must cost the request that produced it nothing — not a
    /// failure, and not a single second of waiting. A dropped event is logged as an error; that is
    /// the signal to look at the broker.
    ///
    /// <paramref name="cancellationToken"/> is accepted for interface symmetry and unused: there is
    /// nothing to wait for, and cancelling the caller must not un-announce a committed change.
    /// </summary>
    public Task PublishAsync<TData>(
        string topic,
        string partitionKey,
        string eventType,
        TData data,
        int version = 1,
        CancellationToken cancellationToken = default)
    {
        var envelope = EventEnvelope.Create(eventType, data, version);
        var json = JsonSerializer.Serialize(envelope, EventEnvelope.JsonOptions);

        try
        {
            _producer.Produce(
                topic,
                new Message<string, string> { Key = partitionKey, Value = json },
                deliveryReport =>
                {
                    if (deliveryReport.Error.IsError)
                    {
                        _logger.LogError(
                            "Dropped {EventType} ({EventId}) for {Topic}: {Reason}. "
                            + "The originating change is already persisted; the event is lost",
                            eventType, envelope.EventId, topic, deliveryReport.Error.Reason);
                        return;
                    }

                    _logger.LogDebug(
                        "Published {EventType} ({EventId}) to {Topic} [partition {Partition}, offset {Offset}]",
                        eventType, envelope.EventId, topic,
                        deliveryReport.Partition.Value, deliveryReport.Offset.Value);
                });
        }
        catch (Exception exception)
        {
            // The local queue is full, or the producer is in a fatal state.
            _logger.LogError(
                exception,
                "Could not queue {EventType} ({EventId}) for {Topic}; the event is lost",
                eventType, envelope.EventId, topic);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Produces and stops waiting after <see cref="_publishTimeout"/>, for the callers that do need
    /// the delivery result. `MessageTimeoutMs` already bounds librdkafka itself; this guards the
    /// steps it does not cover (metadata lookups, a broker that connects and then goes quiet).
    /// </summary>
    private async Task<DeliveryResult<string, string>> ProduceWithinTimeoutAsync(
        string topic,
        Message<string, string> message,
        CancellationToken cancellationToken)
    {
        return await _producer
            .ProduceAsync(topic, message, cancellationToken)
            .WaitAsync(_publishTimeout, cancellationToken);
    }

    public async Task PublishAsync(
        string deadLetterTopic,
        string partitionKey,
        string rawValue,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        var headers = new Headers
        {
            { DeadLetterReasonHeader, System.Text.Encoding.UTF8.GetBytes(failureReason) },
            { DeadLetterAtHeader, System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToString("O")) },
        };

        // Unlike a domain event, a failure here must propagate: the caller decides what to do with
        // a message it could neither process nor park.
        var result = await ProduceWithinTimeoutAsync(
            deadLetterTopic,
            new Message<string, string> { Key = partitionKey, Value = rawValue, Headers = headers },
            cancellationToken);

        _logger.LogWarning(
            "Dead-lettered message to {Topic} [partition {Partition}, offset {Offset}]: {Reason}",
            deadLetterTopic, result.Partition.Value, result.Offset.Value, failureReason);
    }

    public async Task ForwardAsync(
        string topic,
        string partitionKey,
        string payload,
        CancellationToken cancellationToken = default)
    {
        // Also propagates on failure — the outbox row stays unsent and is retried later.
        var result = await ProduceWithinTimeoutAsync(
            topic,
            new Message<string, string> { Key = partitionKey, Value = payload },
            cancellationToken);

        _logger.LogDebug(
            "Forwarded outbox message to {Topic} [partition {Partition}, offset {Offset}]",
            topic, result.Partition.Value, result.Offset.Value);
    }

    public void Dispose()
    {
        // Block briefly so in-flight messages reach the broker before shutdown.
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
