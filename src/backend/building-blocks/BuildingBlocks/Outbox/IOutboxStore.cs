namespace Sellevate.BuildingBlocks.Outbox;

/// <summary>
/// Per-service persistence for the transactional outbox, implemented over that service's own
/// EF <c>DbContext</c>. The relay reads pending rows in enqueue order and marks them dispatched
/// after a successful publish.
/// </summary>
public interface IOutboxStore
{
    /// <summary>Returns up to <paramref name="batchSize"/> not-yet-dispatched messages, oldest first.</summary>
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Marks <paramref name="message"/> as dispatched and persists the change.</summary>
    Task MarkDispatchedAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
