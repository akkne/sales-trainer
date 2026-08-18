using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Sellevate.BuildingBlocks.Persistence;

/// <summary>
/// Resolves which Postgres connection a given piece of work should use, in one place, for all seven
/// services.
///
/// <para>
/// Two connections exist because two different roles are needed: DDL has to run as a role that RLS
/// does not filter, and request-time queries have to run as a role that RLS does filter. Before this
/// type there was one key, <c>ConnectionStrings:Postgres</c>, used for both — which made "switch the
/// application to <c>sellevate_app</c>" an operation that silently broke the next deploy carrying a
/// migration. See docs/TENANCY/RUNBOOK.md.
/// </para>
/// </summary>
public static class PostgresConnectionStrings
{
    public static string Runtime(IConfiguration configuration)
        => configuration.GetConnectionString(PostgresConnectionNames.Runtime)
           ?? throw new InvalidOperationException(
               $"ConnectionStrings:{PostgresConnectionNames.Runtime} is not configured.");

    /// <summary>
    /// The DDL connection, falling back to the runtime one when it is not configured separately.
    ///
    /// <para>
    /// The fallback is deliberate and is what keeps this change invisible to an installation that has
    /// not moved to a separate application role yet: one key configured means one role for
    /// everything, exactly as before.
    /// </para>
    /// </summary>
    public static string Migrations(IConfiguration configuration)
        => IsSplitConfigured(configuration)
            ? configuration.GetConnectionString(PostgresConnectionNames.Migrations)!
            : Runtime(configuration);

    /// <summary>
    /// <see langword="true"/> when the operator has actually split the two roles apart, rather than
    /// leaving the migration connection to fall back. Used only to decide what to say in the startup
    /// log — a service that quietly migrates as its runtime role is the state this whole mechanism
    /// exists to make visible.
    /// </summary>
    public static bool IsSplitConfigured(IConfiguration configuration)
        => !string.IsNullOrWhiteSpace(configuration.GetConnectionString(PostgresConnectionNames.Migrations));

    /// <summary>
    /// Host, database and username of a connection string, with the password removed, for logging.
    /// Every caller logs which role it is about to run DDL as, so "the migration ran as the app role"
    /// is a line in the log rather than a mystery three deploys later.
    /// </summary>
    public static string Describe(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return $"{builder.Host}:{builder.Port}/{builder.Database} as {builder.Username}";
    }
}
