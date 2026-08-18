using Npgsql;

namespace Sellevate.Social.Infrastructure.Data;

/// <summary>
/// Creates the service's own database on first boot, before EF is asked to migrate into it — a fresh
/// environment has a Postgres server but no <c>social</c> database, and <c>Migrate()</c> cannot create
/// one.
///
/// <para>
/// It connects to the maintenance database to do so, and treats a duplicate-database error as success
/// rather than a failure: several services boot at once against the same server, and the loser of that
/// race wants the same outcome as the winner. Ordinary application code must never call this — the
/// connection it opens is outside every tenant guard.
/// </para>
/// </summary>
public static class DatabaseBootstrapper
{
    public static async Task EnsureDatabaseExistsAsync(string connectionString, ILogger logger, CancellationToken cancellationToken = default)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var targetDatabase = builder.Database;

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
