using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Sellevate.Social.Infrastructure.Data;

/// <summary>
/// Phase 40.13. The one and only transaction pattern in social-service, copied deliberately from
/// company-service's 40.12 scope (itself copied from learning-service's 40.10) rather than
/// reinvented — same problem, same shape, so it reads the same in a review.
///
/// <para>
/// <c>TenantConnectionInterceptor</c> issues <c>SET LOCAL app.organization_id</c> when a transaction
/// starts, and <c>SET LOCAL</c> has no effect outside one. EF Core opens an implicit transaction for
/// every <c>SaveChangesAsync</c>, so a lone write is covered for free; a plain <c>SELECT</c> is not,
/// and under the row-level-security policies the AddOrganizationId migration installs it silently
/// returns <em>zero</em> rows. Zero rows is not a visible error on any screen this service backs —
/// an empty Discuss feed looks like a quiet forum and an empty friend list looks like a new
/// account — which is why this is a rule and not a suggestion.
/// </para>
///
/// <para>
/// Rule: <b>every service method that touches a tenant-scoped table opens exactly one scope as its
/// first statement.</b> That includes methods that only read <c>DiscussTags</c>: tags are content
/// (NULL = the curated vocabulary every organization shares), and the content policy still needs the
/// GUC to decide whether the organization's own tags are visible alongside the global ones.
/// <c>UserReplicas</c> is the single table with no policy at all, but a method that reads only
/// replicas does not exist — every one of them starts from a friendship, a thread or a vote.
/// <see cref="BeginReadAsync"/> for methods that only read (it rolls back on dispose, because it
/// exists to make rows visible, never to persist anything); <see cref="BeginWriteAsync"/> plus an
/// explicit <see cref="CommitAsync"/> for methods that also write. Both are re-entrant: a nested
/// call finds a transaction already open and becomes a no-op, so the outermost scope owns the
/// transaction. A write scope must therefore never be nested inside a read scope, or its commit
/// would be swallowed.
/// </para>
///
/// <para>
/// Note on the photo endpoints: the MinIO round-trip happens <em>inside</em> the scope, so an upload
/// holds a Postgres transaction open for the duration of the object write. Accepted rather than
/// worked around, on the same reasoning company-service used for its ai-service calls — these are
/// per-user, low-frequency endpoints, the storage client carries its own timeout, and the row and
/// the object have to be committed together anyway or a failed upload leaves a photo row pointing at
/// nothing.
/// </para>
/// </summary>
internal sealed class TenantTransactionScope : IAsyncDisposable
{
    private readonly IDbContextTransaction? _ownedTransaction;
    private bool _committed;

    private TenantTransactionScope(IDbContextTransaction? ownedTransaction) => _ownedTransaction = ownedTransaction;

    public static Task<TenantTransactionScope> BeginReadAsync(
        SocialDbContext databaseContext,
        CancellationToken cancellationToken)
        => BeginAsync(databaseContext, cancellationToken);

    public static Task<TenantTransactionScope> BeginWriteAsync(
        SocialDbContext databaseContext,
        CancellationToken cancellationToken)
        => BeginAsync(databaseContext, cancellationToken);

    /// <summary>
    /// Opens the transaction, unless one is already open or the provider has none. A non-relational
    /// provider is the in-memory one the unit tests use: no transactions and no row-level security, so
    /// there is nothing to scope and the scope becomes an object that owns nothing.
    /// </summary>
    private static async Task<TenantTransactionScope> BeginAsync(
        SocialDbContext databaseContext,
        CancellationToken cancellationToken)
    {
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
