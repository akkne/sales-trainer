using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Sellevate.Ai.Infrastructure.Data;

/// <summary>
/// Phase 40.18. ai-service's copy of learning-service's transaction rule, added because this block
/// is the first thing in this service that puts a non-null <c>OrganizationId</c> into a content
/// table.
///
/// <para>
/// <c>TenantConnectionInterceptor</c> issues <c>SET LOCAL app.organization_id</c> when a transaction
/// starts, and <c>SET LOCAL</c> does nothing outside one. While every dialog bundle and mode was
/// global that cost nothing: the content policy (<c>OrganizationId IS NULL OR = current</c>) returns
/// the global rows even with the session variable unset, and the global rows were all of them. The
/// moment an organization owns a prompt, a read outside a transaction stops seeing it — the
/// administrator overrides a mode and then cannot find it, and the logs say nothing. This was
/// already recorded in docs/DONT_FORGET.md as ai-service's open gap.
/// </para>
///
/// <para>
/// Re-entrant: a nested scope finds a transaction already open and becomes a no-op, so the outermost
/// scope owns the commit. A read scope rolls back on dispose — it exists to make rows visible, never
/// to persist anything.
/// </para>
/// </summary>
internal sealed class AiTenantTransactionScope : IAsyncDisposable
{
    private readonly IDbContextTransaction? _ownedTransaction;
    private bool _committed;

    private AiTenantTransactionScope(IDbContextTransaction? ownedTransaction) => _ownedTransaction = ownedTransaction;

    public static Task<AiTenantTransactionScope> BeginReadAsync(
        AiDbContext databaseContext,
        CancellationToken cancellationToken)
        => BeginAsync(databaseContext, cancellationToken);

    public static Task<AiTenantTransactionScope> BeginWriteAsync(
        AiDbContext databaseContext,
        CancellationToken cancellationToken)
        => BeginAsync(databaseContext, cancellationToken);

    /// <summary>
    /// Opens a transaction, or hands back an inert scope. Inert in two cases: a transaction is already
    /// open (the outermost scope owns the commit), or the provider is not relational — which is true
    /// only for the in-memory provider the unit tests use, and that has neither transactions nor
    /// row-level security.
    /// </summary>
    private static async Task<AiTenantTransactionScope> BeginAsync(
        AiDbContext databaseContext,
        CancellationToken cancellationToken)
    {
        if (!databaseContext.Database.IsRelational() || databaseContext.Database.CurrentTransaction is not null)
        {
            return new AiTenantTransactionScope(null);
        }

        return new AiTenantTransactionScope(await databaseContext.Database.BeginTransactionAsync(cancellationToken));
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
