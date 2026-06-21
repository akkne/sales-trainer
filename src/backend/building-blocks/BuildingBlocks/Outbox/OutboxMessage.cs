namespace Sellevate.BuildingBlocks.Outbox;

/// <summary>
/// A transactional-outbox row. The producing service writes one of these in the *same* EF
/// transaction as its business change, so the state change and the intent to publish commit
/// atomically. A relay (<see cref="OutboxRelayProcessor"/>) later reads pending rows, publishes
/// the stored event to Kafka, and marks them dispatched — giving at-least-once delivery with
/// no lost events even if the process crashes between the DB commit and the Kafka produce.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    /// <summary>Kafka topic the event must be published to.</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>Partition key (the user id, to preserve per-user ordering).</summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>The fully-serialized <c>EventEnvelope</c> JSON, produced verbatim by the relay.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>When the row was enqueued (UTC).</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>When the relay successfully published the event; <c>null</c> while still pending.</summary>
    public DateTimeOffset? DispatchedAt { get; set; }
}
