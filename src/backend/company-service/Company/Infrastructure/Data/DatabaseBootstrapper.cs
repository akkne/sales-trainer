using Npgsql;
using Sellevate.Company.Common.Constants;

namespace Sellevate.Company.Infrastructure.Data;

/// <summary>
/// Creates company-db if it does not exist yet, before EF migrations run. Migrations can create
/// schema inside a database but not the database itself, so on a fresh Postgres the first startup
/// would otherwise fail on connect.
///
/// <para>
/// Safe to run concurrently with another instance doing the same thing: a losing racer gets
/// <c>42P04 duplicate_database</c>, which is treated as success. The database name is quoted and its
/// quotes doubled because it comes from a connection string rather than from a literal.
/// </para>
/// </summary>
public static class DatabaseBootstrapper
{
    private const string AdministrativeDatabaseName = "postgres";
    private const string DatabaseNameParameter = "name";
    private const string DatabaseExistsSql = "SELECT 1 FROM pg_database WHERE datname = @name";

    public static async Task EnsureDatabaseExistsAsync(string connectionString, ILogger logger, CancellationToken cancellationToken = default)
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var targetDatabase = connectionStringBuilder.Database;

        if (string.IsNullOrWhiteSpace(targetDatabase))
        {
            throw new InvalidOperationException(CompanyErrorMessages.PostgresDatabaseNotSpecified);
        }

        var adminConnectionString =
            new NpgsqlConnectionStringBuilder(connectionString) { Database = AdministrativeDatabaseName }.ConnectionString;

        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var existsCommand = connection.CreateCommand())
        {
            existsCommand.CommandText = DatabaseExistsSql;
            existsCommand.Parameters.AddWithValue(DatabaseNameParameter, targetDatabase);
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
