using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Eventing;

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
