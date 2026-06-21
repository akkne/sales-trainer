using Microsoft.Extensions.Logging;

namespace Sellevate.BuildingBlocks.Outbox;

/// <summary>
/// The storage-agnostic core of the outbox relay: read a batch of pending messages, forward
/// each to Kafka, and mark it dispatched. A forward failure stops the batch (the message stays
/// pending and is retried next tick) so ordering and at-least-once delivery are preserved.
/// Free of EF/Kafka types, so it is unit-testable with fakes.
/// </summary>
public sealed class OutboxRelayProcessor
{
    private readonly IOutboxStore _store;
    private readonly IOutboxEventForwarder _forwarder;
    private readonly ILogger _logger;

    public OutboxRelayProcessor(IOutboxStore store, IOutboxEventForwarder forwarder, ILogger logger)
    {
        _store = store;
        _forwarder = forwarder;
        _logger = logger;
    }

    /// <summary>Dispatches up to <paramref name="batchSize"/> pending messages; returns how many were forwarded.</summary>
    public async Task<int> DispatchPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        var pending = await _store.GetPendingAsync(batchSize, cancellationToken);
        var dispatched = 0;

        foreach (var message in pending)
        {
            try
            {
                await _forwarder.ForwardAsync(message.Topic, message.PartitionKey, message.Payload, cancellationToken);
                await _store.MarkDispatchedAsync(message, cancellationToken);
                dispatched++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception, "Outbox relay failed to forward message {MessageId} to {Topic}; will retry",
                    message.Id, message.Topic);
                break;
            }
        }

        return dispatched;
    }
}
