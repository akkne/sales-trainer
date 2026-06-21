using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;

namespace Sellevate.BuildingBlocks.Messaging;

/// <summary>
/// Kafka-backed <see cref="IEventPublisher"/>. Serializes the
/// <see cref="EventEnvelope"/> to JSON and produces it with idempotent, acks=all
/// settings so a producer retry never duplicates or loses a message broker-side.
/// Registered as a singleton — the underlying producer is thread-safe and pools
/// its broker connections.
/// </summary>
public sealed class KafkaEventPublisher : IEventPublisher, IDeadLetterPublisher, IDisposable
{
    private const string DeadLetterReasonHeader = "x-dead-letter-reason";
    private const string DeadLetterAtHeader = "x-dead-letter-at";

    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IOptions<KafkaSettings> settings, ILogger<KafkaEventPublisher> logger)
    {
        _logger = logger;
        var config = new ProducerConfig
        {
            BootstrapServers = settings.Value.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync<TData>(
        string topic,
        string partitionKey,
        string eventType,
        TData data,
        int version = 1,
        CancellationToken cancellationToken = default)
    {
        var envelope = EventEnvelope.Create(eventType, data, version);
        var json = JsonSerializer.Serialize(envelope, EventEnvelope.JsonOptions);

        var result = await _producer.ProduceAsync(
            topic,
            new Message<string, string> { Key = partitionKey, Value = json },
            cancellationToken);

        _logger.LogDebug(
            "Published {EventType} ({EventId}) to {Topic} [partition {Partition}, offset {Offset}]",
            eventType, envelope.EventId, topic, result.Partition.Value, result.Offset.Value);
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

        var result = await _producer.ProduceAsync(
            deadLetterTopic,
            new Message<string, string> { Key = partitionKey, Value = rawValue, Headers = headers },
            cancellationToken);

        _logger.LogWarning(
            "Dead-lettered message to {Topic} [partition {Partition}, offset {Offset}]: {Reason}",
            deadLetterTopic, result.Partition.Value, result.Offset.Value, failureReason);
    }

    public void Dispose()
    {
        // Block briefly so in-flight messages reach the broker before shutdown.
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
