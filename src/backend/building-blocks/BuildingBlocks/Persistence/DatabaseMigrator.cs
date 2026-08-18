using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sellevate.BuildingBlocks.Persistence;

/// <summary>
/// Brings a service's database up to the current schema at process start, using the
/// <b>migration</b> connection rather than the runtime one.
///
/// <para>
/// The context it migrates with is built here instead of being resolved from the container, and that
/// is the whole point: the container's context is bound to
/// <see cref="PostgresConnectionNames.Runtime"/> and carries the tenant interceptors. A migration run
/// through it would connect as the application role, which after the move to <c>sellevate_app</c>
/// cannot run DDL at all — and would set <c>app.organization_id</c> on a connection that has no
/// business having one.
/// </para>
///
/// <para>
/// The context is still constructed through <see cref="ActivatorUtilities"/> against the caller's
/// scope, so its other constructor dependency (<c>ITenantContext</c>) resolves normally. Migrations
/// are raw SQL and ignore query filters, so the tenant context's state does not affect them.
/// </para>
/// </summary>
public static class DatabaseMigrator
{
    /// <summary>
    /// Ensures the database exists, applies every pending migration, and hands the migrated context to
    /// <paramref name="afterMigrationsAsync"/> if the caller has data work that must run on the same
    /// privileged connection.
    /// </summary>
    /// <param name="serviceProvider">A scoped provider — the caller owns the scope and its lifetime.</param>
    public static async Task MigrateAsync<TContext>(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger logger,
        Func<TContext, CancellationToken, Task>? afterMigrationsAsync = null,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        var migrationConnectionString = PostgresConnectionStrings.Migrations(configuration);

        if (PostgresConnectionStrings.IsSplitConfigured(configuration))
        {
            logger.LogInformation(
                "Applying migrations on the dedicated migration connection {Connection}",
                PostgresConnectionStrings.Describe(migrationConnectionString));
        }
        else
        {
            logger.LogInformation(
                "ConnectionStrings:{Key} is not configured; applying migrations on the runtime connection {Connection}",
                PostgresConnectionNames.Migrations,
                PostgresConnectionStrings.Describe(migrationConnectionString));
        }

        await DatabaseBootstrapper.EnsureDatabaseExistsAsync(migrationConnectionString, logger, cancellationToken);

        var options = new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(migrationConnectionString)
            .Options;

        await using var databaseContext = ActivatorUtilities.CreateInstance<TContext>(serviceProvider, options);

        await databaseContext.Database.MigrateAsync(cancellationToken);

        if (afterMigrationsAsync is not null)
        {
            await afterMigrationsAsync(databaseContext, cancellationToken);
        }
    }
}
