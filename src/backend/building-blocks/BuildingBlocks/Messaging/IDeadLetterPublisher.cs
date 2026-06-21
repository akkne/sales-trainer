namespace Sellevate.BuildingBlocks.Messaging;

/// <summary>
/// Publishes a poison message — one that still failed after the configured retries — to its
/// dead-letter topic, forwarding the original key and raw value verbatim so it can be
/// inspected or replayed later without re-parsing.
/// </summary>
public interface IDeadLetterPublisher
{
    /// <summary>
    /// Forward the original <paramref name="rawValue"/> (keyed by <paramref name="partitionKey"/>)
    /// to <paramref name="deadLetterTopic"/>, stamping the failure reason for diagnostics.
    /// </summary>
    Task PublishAsync(
        string deadLetterTopic,
        string partitionKey,
        string rawValue,
        string failureReason,
        CancellationToken cancellationToken = default);
}
