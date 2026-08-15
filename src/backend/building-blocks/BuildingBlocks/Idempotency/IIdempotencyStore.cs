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
///
/// <para>
/// Phase 40.11 adds the organization, taken from the envelope
/// (<c>EventEnvelope.OrganizationId</c>) rather than from any ambient context, because the
/// consumer's tenant is a property of the message it is holding. It is optional: a
/// platform-global event carries no organization and keeps the un-prefixed key, which is also
/// what preserves dedupe for events already processed before 40.11.
/// </para>
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Returns <c>true</c> if (<paramref name="organizationId"/>, <paramref name="consumerGroup"/>,
    /// <paramref name="eventId"/>) was already processed.
    /// </summary>
    Task<bool> HasProcessedAsync(
        string consumerGroup,
        Guid eventId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that (<paramref name="organizationId"/>, <paramref name="consumerGroup"/>,
    /// <paramref name="eventId"/>) has been processed.
    /// </summary>
    Task MarkProcessedAsync(
        string consumerGroup,
        Guid eventId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);
}
