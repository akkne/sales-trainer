namespace Sellevate.BuildingBlocks.Messaging;

/// <summary>
/// Publishes integration events to the backend-to-backend event bus (Kafka).
/// The payload is wrapped in an <see cref="Eventing.EventEnvelope"/> by the implementation;
/// callers only supply the topic, partition key, event type and payload.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publish <paramref name="data"/> as event <paramref name="eventType"/> to
    /// <paramref name="topic"/>, keyed by <paramref name="partitionKey"/> (use the
    /// user id to preserve per-user ordering).
    /// </summary>
    Task PublishAsync<TData>(
        string topic,
        string partitionKey,
        string eventType,
        TData data,
        int version = 1,
        CancellationToken cancellationToken = default);
}
