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
    private const string DeadLetterTimestampFormat = "O";
    private const int MinimumPublishTimeoutSeconds = 1;

    /// <summary>
    /// How long <see cref="Dispose"/> blocks so in-flight messages reach the broker before the host
    /// tears the producer down. Bounded on purpose: a shutdown must not hang on a dead broker.
    /// </summary>
    private static readonly TimeSpan ShutdownFlushTimeout = TimeSpan.FromSeconds(5);

    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;
    private readonly TimeSpan _publishTimeout;

    /// <summary>
    /// Builds the shared producer. <c>MessageTimeoutMs</c> is pinned to
    /// <see cref="KafkaSettings.PublishTimeoutSeconds"/> because librdkafka's own default retries an
    /// unreachable broker for five minutes before reporting the failure, and every producer call
    /// would inherit that as its worst case.
    /// </summary>
    public KafkaEventPublisher(IOptions<KafkaSettings> settings, ILogger<KafkaEventPublisher> logger)
    {
        _logger = logger;
        _publishTimeout = TimeSpan.FromSeconds(
            Math.Max(MinimumPublishTimeoutSeconds, settings.Value.PublishTimeoutSeconds));
        var config = new ProducerConfig
        {
            BootstrapServers = settings.Value.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
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
    ///
    /// <para>
    /// The <c>catch</c> covers a full local queue or a producer in a fatal state — both of which
    /// lose the event, which is why they are logged as errors rather than rethrown.
    /// </para>
    /// </summary>
    public Task PublishAsync<TData>(
        string topic,
        string partitionKey,
        string eventType,
        TData data,
        int version = 1,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        var envelope = EventEnvelope.Create(eventType, data, version, organizationId);
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

    /// <summary>
    /// Parks a poison message on its dead-letter topic, stamping the failure reason and the current
    /// UTC instant as headers.
    ///
    /// <para>
    /// Unlike <see cref="PublishAsync{TData}"/>, a failure here <b>propagates</b>: the caller decides
    /// what to do with a message it could neither process nor park, and swallowing it would commit
    /// the offset on a message that went nowhere.
    /// </para>
    /// </summary>
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
            {
                DeadLetterAtHeader,
                System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToString(DeadLetterTimestampFormat))
            },
        };

        var result = await ProduceWithinTimeoutAsync(
            deadLetterTopic,
            new Message<string, string> { Key = partitionKey, Value = rawValue, Headers = headers },
            cancellationToken);

        _logger.LogWarning(
            "Dead-lettered message to {Topic} [partition {Partition}, offset {Offset}]: {Reason}",
            deadLetterTopic, result.Partition.Value, result.Offset.Value, failureReason);
    }

    /// <summary>
    /// Re-emits an outbox row's stored envelope verbatim. Also propagates on failure — the outbox
    /// row then stays unsent and the relay retries it on a later tick, which is what preserves
    /// at-least-once delivery.
    /// </summary>
    public async Task ForwardAsync(
        string topic,
        string partitionKey,
        string payload,
        CancellationToken cancellationToken = default)
    {
        var result = await ProduceWithinTimeoutAsync(
            topic,
            new Message<string, string> { Key = partitionKey, Value = payload },
            cancellationToken);

        _logger.LogDebug(
            "Forwarded outbox message to {Topic} [partition {Partition}, offset {Offset}]",
            topic, result.Partition.Value, result.Offset.Value);
    }

    /// <summary>
    /// Flushes for at most <see cref="ShutdownFlushTimeout"/> so queued messages reach the broker,
    /// then releases the producer.
    /// </summary>
    public void Dispose()
    {
        _producer.Flush(ShutdownFlushTimeout);
        _producer.Dispose();
    }
}
