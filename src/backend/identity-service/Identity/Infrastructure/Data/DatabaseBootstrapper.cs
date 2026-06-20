using Npgsql;

namespace Sellevate.Identity.Infrastructure.Data;

/// <summary>
/// Ensures the service's own Postgres database exists before EF migrations run.
/// Database-per-service means Identity owns a database the shared Postgres instance does
/// not create for it, and <c>EF Migrate()</c> can create tables but not the database
/// itself. This connects to the <c>postgres</c> maintenance database and issues
/// <c>CREATE DATABASE</c> once if the target is missing — idempotent and safe to run on
/// every startup (including against an already-populated shared volume).
/// </summary>
public static class DatabaseBootstrapper
{
    public static async Task EnsureDatabaseExistsAsync(string connectionString, ILogger logger, CancellationToken cancellationToken = default)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var targetDatabase = builder.Database;

        if (string.IsNullOrWhiteSpace(targetDatabase))
            throw new InvalidOperationException("ConnectionStrings:Postgres must specify a Database.");

        // Connect to the always-present maintenance DB to check/create the target.
        var adminConnectionString = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" }.ConnectionString;

        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var existsCommand = connection.CreateCommand())
        {
            existsCommand.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name";
            existsCommand.Parameters.AddWithValue("name", targetDatabase);
            var exists = await existsCommand.ExecuteScalarAsync(cancellationToken) is not null;
            if (exists)
                return;
        }

        // Database name comes from our own config, not user input; quote it defensively anyway.
        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = $"CREATE DATABASE \"{targetDatabase.Replace("\"", "\"\"")}\"";
        await createCommand.ExecuteNonQueryAsync(cancellationToken);
        logger.LogInformation("Created database {Database}", targetDatabase);
    }
}
