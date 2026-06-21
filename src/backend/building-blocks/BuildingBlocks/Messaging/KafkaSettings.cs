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

    /// <summary>
    /// When true (default), every service provisions all known topics (and their
    /// <c>.dlt</c> companions) on startup via an admin client. This makes the platform
    /// independent of the broker's <c>auto.create.topics.enable</c> setting — on a
    /// hardened/managed broker (where auto-create is off) a consumer that subscribes to a
    /// not-yet-existing topic would otherwise fail with "Unknown topic or partition".
    /// </summary>
    public bool ProvisionTopics { get; set; } = true;

    /// <summary>Partition count used when a topic is created by the provisioner.</summary>
    public int TopicPartitions { get; set; } = 1;

    /// <summary>
    /// Replication factor used when a topic is created by the provisioner. Keep at 1 for a
    /// single-broker (local/dev) cluster; raise it (e.g. 3) on a multi-broker production
    /// cluster — a managed broker may reject a topic whose replication factor it cannot satisfy.
    /// </summary>
    public short TopicReplicationFactor { get; set; } = 1;
}
