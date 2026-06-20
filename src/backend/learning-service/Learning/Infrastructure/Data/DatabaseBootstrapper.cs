using Npgsql;

namespace Sellevate.Learning.Infrastructure.Data;

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

        var administratorConnectionString = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" }.ConnectionString;

        await using var connection = new NpgsqlConnection(administratorConnectionString);
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
