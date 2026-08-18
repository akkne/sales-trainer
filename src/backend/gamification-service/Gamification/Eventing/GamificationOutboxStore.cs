using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Eventing;

/// <summary>
/// gamification-db's half of the outbox contract: hands the relay the undispatched messages, oldest
/// first, and stamps one as dispatched.
///
/// <para>
/// Ordering by occurrence time rather than by insertion is what makes the relay's delivery order match
/// the order the events actually happened in. <c>OutboxMessages</c> carries no RLS policy and no query
/// filter — it is plumbing read only by the system-mode relay — so this store deliberately sees every
/// tenant's rows (docs/TENANCY/TENANCY.md §1.7).
/// </para>
/// </summary>
internal sealed class GamificationOutboxStore : IOutboxStore
{
    private readonly GamificationDbContext _databaseContext;

    public GamificationOutboxStore(GamificationDbContext databaseContext)
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
