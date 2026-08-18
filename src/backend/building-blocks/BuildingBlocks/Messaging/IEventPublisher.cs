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
    ///
    /// <para>
    /// <paramref name="organizationId"/> fills the envelope's tenant field (Phase 40.3), which is
    /// how a consumer knows which tenant to process a message for. It is passed explicitly rather
    /// than taken from an ambient <c>ITenantContext</c> because a publisher is a singleton and a
    /// tenant context is request-scoped — and because the producers that matter most here are
    /// background jobs, where the tenant is a property of the unit of work rather than of the
    /// caller. Leave it <see langword="null"/> only for genuinely platform-global events.
    /// </para>
    /// </summary>
    Task PublishAsync<TData>(
        string topic,
        string partitionKey,
        string eventType,
        TData data,
        int version = 1,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);
}
