using Sellevate.BuildingBlocks.Eventing;

namespace Sellevate.BuildingBlocks.Outbox;

/// <summary>
/// Enqueues an event into the transactional outbox. The implementation adds an
/// <see cref="OutboxMessage"/> to the service's tracked <c>DbContext</c> but does NOT save —
/// the caller commits it in the same <c>SaveChangesAsync</c> as its business change, making
/// the state write and the publish-intent atomic.
/// </summary>
public interface IOutboxWriter
{
    /// <summary>
    /// Serialize <paramref name="data"/> into an <see cref="EventEnvelope"/> and stage an
    /// outbox row for <paramref name="topic"/>, keyed by <paramref name="partitionKey"/>.
    /// </summary>
    void Enqueue<TData>(
        string topic,
        string partitionKey,
        string eventType,
        TData data,
        int version = 1);
}
