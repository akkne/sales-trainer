using Npgsql;

namespace Sellevate.Organization.Infrastructure.Data;

/// <summary>
/// Creates the service's own Postgres database on first boot, before EF migrations run — migrations can
/// create tables but not the database that holds them.
///
/// <para>
/// Connects to the <c>postgres</c> maintenance database to do it, and treats a
/// <c>DuplicateDatabase</c> error as success: several services boot at once against the same server, so
/// two of them racing on <c>CREATE DATABASE</c> is the expected case rather than a fault.
/// </para>
/// </summary>
public static class DatabaseBootstrapper
{
    public static async Task EnsureDatabaseExistsAsync(string connectionString, ILogger logger, CancellationToken cancellationToken = default)
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var targetDatabase = connectionStringBuilder.Database;

        if (string.IsNullOrWhiteSpace(targetDatabase))
        {
            throw new InvalidOperationException("ConnectionStrings:Postgres must specify a Database.");
        }

        var adminConnectionString = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" }.ConnectionString;

        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var existsCommand = connection.CreateCommand())
        {
            existsCommand.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name";
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
        catch (PostgresException postgresException) when (postgresException.SqlState == PostgresErrorCodes.DuplicateDatabase)
        {
            logger.LogInformation("Database {Database} already created concurrently; continuing", targetDatabase);
        }
    }
}
