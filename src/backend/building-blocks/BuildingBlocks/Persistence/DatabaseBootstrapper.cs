using Microsoft.Extensions.Logging;
using Npgsql;

namespace Sellevate.BuildingBlocks.Persistence;

/// <summary>
/// Creates a service's own Postgres database on first boot, before EF migrations run — migrations can
/// create tables but not the database that holds them.
///
/// <para>
/// Connects to the <c>postgres</c> maintenance database to do it, and treats a
/// <c>DuplicateDatabase</c> error as success: several services boot at once against the same server,
/// so two of them racing on <c>CREATE DATABASE</c> is the expected case rather than a fault.
/// </para>
///
/// <para>
/// Seven byte-identical copies of this used to live one per service. It moved here when the migration
/// connection string was split out from the runtime one, because that change had to be made in every
/// copy at once — the class of duplication where the next edit is the one that gets it wrong.
/// </para>
/// </summary>
public static class DatabaseBootstrapper
{
    private const string MaintenanceDatabaseName = "postgres";

    private const string DatabaseExistsSql = "SELECT 1 FROM pg_database WHERE datname = @name";

    public static async Task EnsureDatabaseExistsAsync(
        string connectionString, ILogger logger, CancellationToken cancellationToken = default)
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var targetDatabase = connectionStringBuilder.Database;

        if (string.IsNullOrWhiteSpace(targetDatabase))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{PostgresConnectionNames.Runtime} must specify a Database.");
        }

        var maintenanceConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = MaintenanceDatabaseName,
        }.ConnectionString;

        await using var connection = new NpgsqlConnection(maintenanceConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var existsCommand = connection.CreateCommand())
        {
            existsCommand.CommandText = DatabaseExistsSql;
            existsCommand.Parameters.AddWithValue("name", targetDatabase);
            var exists = await existsCommand.ExecuteScalarAsync(cancellationToken) is not null;
            if (exists)
            {
                return;
            }
        }

        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = $"CREATE DATABASE \"{targetDatabase.Replace("\"", "\"\"")}\"";
        try
        {
            await createCommand.ExecuteNonQueryAsync(cancellationToken);
            logger.LogInformation("Created database {Database}", targetDatabase);
        }
        catch (PostgresException postgresException)
            when (postgresException.SqlState == PostgresErrorCodes.DuplicateDatabase)
        {
            logger.LogInformation("Database {Database} already created concurrently; continuing", targetDatabase);
        }
    }
}
