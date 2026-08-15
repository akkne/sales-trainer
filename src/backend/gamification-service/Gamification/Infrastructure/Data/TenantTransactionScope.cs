using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Sellevate.Gamification.Infrastructure.Data;

/// <summary>
/// Phase 40.13. The one and only transaction pattern in gamification-service, copied deliberately
/// from learning-service's 40.10 scope and company-service's 40.12 one rather than reinvented —
/// same problem, same shape, so it should read the same in a review.
///
/// <para>
/// <c>TenantConnectionInterceptor</c> issues <c>SET LOCAL app.organization_id</c> when a
/// transaction starts, and <c>SET LOCAL</c> has no effect outside one. EF Core opens an implicit
/// transaction for every <c>SaveChangesAsync</c>, so a lone write is covered for free; a plain
/// <c>SELECT</c> is not, and under the row-level-security policies the AddOrganizationId migration
/// installs it silently returns <em>zero</em> rows. Zero rows from a gamification read is not a
/// visible error — an empty achievement list looks exactly like a new user, and an empty league
/// looks exactly like a quiet week — which is why this is a rule and not a suggestion.
/// </para>
///
/// <para>
/// Rule: <b>every service method and every admin action that touches a tenant-scoped
/// gamification-service table opens exactly one scope as its first statement.</b> The tables that
/// need it are the seven the migration lists; the catalogues and configuration rows
/// (<c>Achievements</c>, <c>LeagueTiers</c>, <c>GamificationSettings</c>, <c>StreakMilestones</c>,
/// <c>ExerciseTypeRewards</c>) have no policy and would read fine without one — but methods mix
/// the two freely, so the scope goes on the method rather than on each query.
/// <see cref="BeginReadAsync"/> for methods that only read (it rolls back on dispose, because it
/// exists to make rows visible, never to persist anything); <see cref="BeginWriteAsync"/> plus an
/// explicit <see cref="CommitAsync"/> for methods that also write. Both are re-entrant: a nested
/// call finds a transaction already open and becomes a no-op, so the outermost scope owns the
/// transaction. A write scope must therefore never be nested inside a read scope, or its commit
/// would be swallowed.
/// </para>
///
/// <para>
/// <c>LeagueService.CloseCurrentLeagueAndCreateNextAsync</c> opens its own transaction directly and
/// deliberately keeps doing so: it has always relied on that transaction for the optimistic
/// concurrency guard around the period rollover, not for tenancy, and re-entrancy means the two
/// mechanisms do not fight.
/// </para>
/// </summary>
internal sealed class TenantTransactionScope : IAsyncDisposable
{
    private readonly IDbContextTransaction? _ownedTransaction;
    private bool _committed;

    private TenantTransactionScope(IDbContextTransaction? ownedTransaction) => _ownedTransaction = ownedTransaction;

    public static Task<TenantTransactionScope> BeginReadAsync(
        GamificationDbContext databaseContext,
        CancellationToken cancellationToken)
        => BeginAsync(databaseContext, cancellationToken);

    public static Task<TenantTransactionScope> BeginWriteAsync(
        GamificationDbContext databaseContext,
        CancellationToken cancellationToken)
        => BeginAsync(databaseContext, cancellationToken);

    private static async Task<TenantTransactionScope> BeginAsync(
        GamificationDbContext databaseContext,
        CancellationToken cancellationToken)
    {
        // IsRelational() is false only for the in-memory provider the unit tests use, which has no
        // transactions and no row-level security — there is nothing to scope there.
        if (!databaseContext.Database.IsRelational() || databaseContext.Database.CurrentTransaction is not null)
        {
            return new TenantTransactionScope(null);
        }

        return new TenantTransactionScope(await databaseContext.Database.BeginTransactionAsync(cancellationToken));
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_ownedTransaction is null)
        {
            return;
        }

        await _ownedTransaction.CommitAsync(cancellationToken);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownedTransaction is null)
        {
            return;
        }

        if (!_committed)
        {
            await _ownedTransaction.RollbackAsync();
        }

        await _ownedTransaction.DisposeAsync();
    }
}
