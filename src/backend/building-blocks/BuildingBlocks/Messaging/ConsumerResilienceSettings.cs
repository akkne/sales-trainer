namespace Sellevate.BuildingBlocks.Messaging;

/// <summary>
/// Opt-in retry + dead-letter policy for the shared idempotent consumer base, bound from
/// the <c>Kafka:ConsumerResilience</c> config section. Defaults are safe: a few bounded
/// in-process retries, then the poison message is routed to <c>&lt;topic&gt;.dlt</c> and the
/// offset is committed so it can never block the partition.
/// </summary>
public sealed class ConsumerResilienceSettings
{
    public const string SectionName = "Kafka:ConsumerResilience";

    /// <summary>
    /// How many extra times a failing handler is retried in-process before the message is
    /// dead-lettered. <c>0</c> means a single attempt with no retry.
    /// </summary>
    public int MaxHandlerRetries { get; set; } = 3;

    /// <summary>Base delay between in-process retries, in milliseconds (linear back-off per attempt).</summary>
    public int RetryDelayMilliseconds { get; set; } = 500;

    /// <summary>
    /// When <c>true</c> (default), a message still failing after all retries is published to
    /// its dead-letter topic and its offset committed, so the partition keeps flowing. When
    /// <c>false</c>, the offset is left uncommitted and the message is redelivered forever
    /// (the pre-Phase-10 behaviour), which can block the partition on a poison message.
    /// </summary>
    public bool DeadLetterEnabled { get; set; } = true;
}
