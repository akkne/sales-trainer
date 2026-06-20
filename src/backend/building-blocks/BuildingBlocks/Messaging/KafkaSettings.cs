namespace Sellevate.BuildingBlocks.Messaging;

/// <summary>
/// Per-service Kafka configuration, bound from the <c>Kafka</c> config section.
/// Every service shares the same broker(s) but uses its own <see cref="ConsumerGroupId"/>
/// so each gets its own copy of every event it subscribes to.
/// </summary>
public sealed class KafkaSettings
{
    public const string SectionName = "Kafka";

    /// <summary>Comma-separated broker list, e.g. <c>localhost:9092</c>.</summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// Consumer group id for this service (e.g. <c>gamification-service</c>). Each
    /// service owns its group so it independently tracks its own offsets.
    /// </summary>
    public string ConsumerGroupId { get; set; } = "sellevate-service";

    /// <summary>How long a processed <c>eventId</c> stays remembered in the idempotency store.</summary>
    public int IdempotencyTtlDays { get; set; } = 7;
}
