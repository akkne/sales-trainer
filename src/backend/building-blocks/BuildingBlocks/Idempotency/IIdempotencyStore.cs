namespace Sellevate.BuildingBlocks.Idempotency;

/// <summary>
/// Remembers which events a consumer group has already processed, so an
/// at-least-once redelivery is handled exactly once. Keyed by
/// (consumer group, eventId) so the same event can be processed independently by
/// different services.
///
/// <para>
/// Usage is "check → handle → mark": a consumer skips events that
/// <see cref="HasProcessedAsync"/> reports as seen, and only calls
/// <see cref="MarkProcessedAsync"/> <em>after</em> a successful handle. A handler
/// that throws leaves the event unmarked, so it is safely retried on redelivery.
/// </para>
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>Returns <c>true</c> if (<paramref name="consumerGroup"/>, <paramref name="eventId"/>) was already processed.</summary>
    Task<bool> HasProcessedAsync(string consumerGroup, Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>Records that (<paramref name="consumerGroup"/>, <paramref name="eventId"/>) has been processed.</summary>
    Task MarkProcessedAsync(string consumerGroup, Guid eventId, CancellationToken cancellationToken = default);
}
