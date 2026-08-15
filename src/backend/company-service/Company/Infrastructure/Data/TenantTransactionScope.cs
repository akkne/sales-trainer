using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Sellevate.Company.Infrastructure.Data;

/// <summary>
/// Phase 40.12. The one and only transaction pattern in company-service, copied deliberately from
/// learning-service's 40.10 scope rather than reinvented — same problem, same shape, so it should
/// read the same in a review.
///
/// <para>
/// <c>TenantConnectionInterceptor</c> issues <c>SET LOCAL app.organization_id</c> when a
/// transaction starts, and <c>SET LOCAL</c> has no effect outside one. EF Core opens an implicit
/// transaction for every <c>SaveChangesAsync</c>, so a lone write is covered for free; a plain
/// <c>SELECT</c> is not, and under the row-level-security policies the AddOrganizationId migration
/// installs it silently returns <em>zero</em> rows. Zero rows from a company list is not a visible
/// error — it looks exactly like a salesperson who has not added anyone yet, which is why this is
/// a rule and not a suggestion.
/// </para>
///
/// <para>
/// Rule: <b>every service method that touches any company-service table opens exactly one scope as
/// its first statement.</b> All five tables here are tenant-scoped — there is no global content in
/// this database — so there is no exception to remember. <see cref="BeginReadAsync"/> for methods
/// that only read (it rolls back on dispose, because it exists to make rows visible, never to
/// persist anything); <see cref="BeginWriteAsync"/> plus an explicit <see cref="CommitAsync"/> for
/// methods that also write. Both are re-entrant: a nested call finds a transaction already open and
/// becomes a no-op, so the outermost scope owns the transaction. A write scope must therefore never
/// be nested inside a read scope, or its commit would be swallowed.
/// </para>
///
/// <para>
/// Note for the AI-backed methods (briefing, readiness, persona generation, log parsing): the HTTP
/// call to ai-service happens <em>inside</em> the scope, so a slow model holds a Postgres
/// transaction open for its duration. That is accepted rather than worked around — these are
/// per-user, low-frequency endpoints, the ai-service clients carry their own timeouts, and the
/// alternative (close the transaction, call the model, reopen) would need the reopened scope to
/// re-read and re-validate the row anyway.
/// </para>
/// </summary>
internal sealed class TenantTransactionScope : IAsyncDisposable
{
    private readonly IDbContextTransaction? _ownedTransaction;
    private bool _committed;

    private TenantTransactionScope(IDbContextTransaction? ownedTransaction) => _ownedTransaction = ownedTransaction;

    public static Task<TenantTransactionScope> BeginReadAsync(
        CompanyDbContext databaseContext,
        CancellationToken cancellationToken)
        => BeginAsync(databaseContext, cancellationToken);

    public static Task<TenantTransactionScope> BeginWriteAsync(
        CompanyDbContext databaseContext,
        CancellationToken cancellationToken)
        => BeginAsync(databaseContext, cancellationToken);

    private static async Task<TenantTransactionScope> BeginAsync(
        CompanyDbContext databaseContext,
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
