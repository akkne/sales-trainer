using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Phase 40.10. The one and only transaction pattern in learning-service.
///
/// <para>
/// <c>TenantConnectionInterceptor</c> issues <c>SET LOCAL app.organization_id</c> when a
/// transaction starts, and <c>SET LOCAL</c> has no effect outside one. EF Core opens an implicit
/// transaction for every <c>SaveChangesAsync</c>, so a lone write is covered for free; a plain
/// <c>SELECT</c> is not, and under the row-level-security policies added by the AddOrganizationId
/// migration it silently returns <em>zero</em> tenant rows (and only the global content library).
/// See docs/DECISIONS.md, "SET LOCAL vs SET, and the read-transaction requirement (40.4)".
/// </para>
///
/// <para>
/// Rule, stated once so it is not re-litigated per call site: <b>every service method that touches
/// a tenant-scoped table — or any content table, which may hold organization-owned rows — opens
/// exactly one scope as its first statement.</b> <see cref="BeginReadAsync"/> for methods that only
/// read (it rolls back on dispose, because it exists to make rows visible, never to persist
/// anything); <see cref="BeginWriteAsync"/> plus an explicit <see cref="CommitAsync"/> for methods
/// that also write. Both are re-entrant: a nested call finds a transaction already open and becomes
/// a no-op, so the outermost scope owns the transaction. A write scope must therefore never be
/// nested inside a read scope, or its commit would be swallowed.
/// </para>
///
/// <para>
/// Known exception, deliberately not covered: the superadmin-only controllers under
/// <c>Features/Admin</c> talk to the content tables directly and open no scope. Content is global
/// (<c>OrganizationId IS NULL</c>) for the whole of 40.10, and the content RLS policy admits global
/// rows even with the session variable unset, so those endpoints keep working. Phase 40.18
/// (organization-authored content) has to revisit them.
/// </para>
/// </summary>
internal sealed class TenantTransactionScope : IAsyncDisposable
{
    private readonly IDbContextTransaction? _ownedTransaction;
    private bool _committed;

    private TenantTransactionScope(IDbContextTransaction? ownedTransaction) => _ownedTransaction = ownedTransaction;

    public static Task<TenantTransactionScope> BeginReadAsync(
        LearningDbContext databaseContext,
        CancellationToken cancellationToken)
        => BeginAsync(databaseContext, cancellationToken);

    public static Task<TenantTransactionScope> BeginWriteAsync(
        LearningDbContext databaseContext,
        CancellationToken cancellationToken)
        => BeginAsync(databaseContext, cancellationToken);

    private static async Task<TenantTransactionScope> BeginAsync(
        LearningDbContext databaseContext,
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
