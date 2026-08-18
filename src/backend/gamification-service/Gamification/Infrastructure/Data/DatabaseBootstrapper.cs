using Npgsql;
using Sellevate.Gamification.Common.Constants;

namespace Sellevate.Gamification.Infrastructure.Data;

/// <summary>
/// Creates gamification-db on first start so a fresh environment needs no manual step before
/// migrations can run.
///
/// <para>
/// Idempotent and safe to run concurrently: it checks <c>pg_database</c> first and treats a
/// duplicate-database error from a racing instance as success. It connects to the maintenance
/// database to do so, because it cannot connect to the database it is about to create.
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
            throw new InvalidOperationException(ErrorMessages.PostgresConnectionStringMustNameDatabase);
        }

        var administratorConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = ConfigurationKeys.MaintenanceDatabaseName,
        }.ConnectionString;

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
