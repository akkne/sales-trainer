using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Eventing;

/// <summary>
/// The relay's view of identity-db's outbox table: oldest undispatched messages first, so ordering
/// per topic follows the order the facts occurred in. Marking a message dispatched is a save, not a
/// delete — the row stays as the record that it went out.
/// </summary>
internal sealed class IdentityOutboxStore : IOutboxStore
{
    private readonly IdentityDbContext _databaseContext;

    public IdentityOutboxStore(IdentityDbContext databaseContext)
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
