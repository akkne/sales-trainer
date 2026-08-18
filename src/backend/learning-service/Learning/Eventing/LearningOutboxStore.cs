using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Eventing;

/// <summary>
/// learning-db's half of the shared outbox relay: hands the relay the undispatched messages in the
/// order they occurred and records a dispatch.
///
/// <para>
/// <c>OutboxMessages</c> deliberately carries no tenant query filter: the relay is a platform-wide
/// pump that has to drain every organization's staged messages, and the owning organization travels
/// inside the serialized envelope rather than being used to restrict the read.
/// </para>
/// </summary>
internal sealed class LearningOutboxStore : IOutboxStore
{
    private readonly LearningDbContext _databaseContext;

    public LearningOutboxStore(LearningDbContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default)
        => await _databaseContext.OutboxMessages
            .Where(outboxMessage => outboxMessage.DispatchedAt == null)
            .OrderBy(outboxMessage => outboxMessage.OccurredAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task MarkDispatchedAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        message.DispatchedAt = DateTimeOffset.UtcNow;
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }
}
