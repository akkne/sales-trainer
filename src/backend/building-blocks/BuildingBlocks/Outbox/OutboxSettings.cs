namespace Sellevate.BuildingBlocks.Outbox;

/// <summary>
/// Polling configuration for the outbox relay, bound from the <c>Outbox</c> config section.
/// </summary>
public sealed class OutboxSettings
{
    public const string SectionName = "Outbox";

    /// <summary>How often the relay polls for pending messages, in milliseconds.</summary>
    public int PollingIntervalMilliseconds { get; set; } = 1000;

    /// <summary>Maximum messages dispatched per poll tick.</summary>
    public int BatchSize { get; set; } = 50;
}
