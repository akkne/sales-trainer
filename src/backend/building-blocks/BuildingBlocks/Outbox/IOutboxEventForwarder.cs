namespace Sellevate.BuildingBlocks.Outbox;

/// <summary>
/// Publishes an already-serialized outbox payload (a full <c>EventEnvelope</c> JSON) to its
/// Kafka topic verbatim, so the relay re-emits exactly what the producer enqueued.
/// </summary>
public interface IOutboxEventForwarder
{
    Task ForwardAsync(
        string topic,
        string partitionKey,
        string payload,
        CancellationToken cancellationToken = default);
}
