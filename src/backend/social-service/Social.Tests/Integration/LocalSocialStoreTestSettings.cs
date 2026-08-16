using MongoDB.Driver;
using Npgsql;

namespace Sellevate.Social.Tests.Integration;

/// <summary>
/// Phase 40.13. Connection settings for the two-store isolation fixture, mirroring
/// <c>Ai.Tests/Integration/LocalAiStoreTestSettings</c> from 40.11 — social-service has the same
/// Postgres + Mongo shape, so the fixture should read the same in a review. Redis is absent here
/// because social-service keeps no tenant-scoped Redis state; its cross-service Redis surface is
/// notification-service's inbox, tested there.
///
/// <para>
/// Defaults match <c>scripts/lib-local-env.sh</c> (the same values <c>scripts/dev-infra.sh</c>
/// starts), so the fixture runs with zero configuration against a developer's running dev-infra
/// stack. Its throwaway Postgres database, login role and Mongo database are all named distinctly
/// from anything the application uses: it never touches the real <c>social</c> database or the real
/// <c>sallevate</c> Mongo database.
/// </para>
///
/// <para>
/// Every probe is bounded and short. Per CLAUDE.md and docs/DONT_FORGET.md this fixture must never
/// hang waiting for infrastructure that is not running — with nothing up, the whole file skips in
/// seconds.
/// </para>
/// </summary>
internal static class LocalSocialStoreTestSettings
{
    private static readonly string PostgresHost = Environment.GetEnvironmentVariable("TENANCY_RLS_TEST_POSTGRES_HOST") ?? "localhost";
    private static readonly string PostgresPort = Environment.GetEnvironmentVariable("LOCAL_POSTGRES_PORT") ?? "5433";
    private static readonly string SuperuserUsername = Environment.GetEnvironmentVariable("TENANCY_RLS_TEST_POSTGRES_SUPERUSER") ?? "st";
    private static readonly string SuperuserPassword = Environment.GetEnvironmentVariable("TENANCY_RLS_TEST_POSTGRES_SUPERUSER_PASSWORD") ?? "st_secret";

    private static readonly string MongoHost = Environment.GetEnvironmentVariable("TENANCY_TEST_MONGO_HOST") ?? "localhost";
    private static readonly string MongoPort = Environment.GetEnvironmentVariable("LOCAL_MONGO_PORT") ?? "27017";

    public const string TestDatabaseName = "social_tenancy_integration_test";
    public const string ApplicationRoleName = "sellevate_social_app_test";
    public const string ApplicationRolePassword = "social_tenancy_test_only_password";

    /// <summary>Its own Mongo database, dropped at the start and end of the run.</summary>
    public const string MongoDatabaseName = "social_tenancy_integration_test";

    public static string AdminConnectionString(string databaseName = "postgres")
        => new NpgsqlConnectionStringBuilder
        {
            Host = PostgresHost,
            Port = int.Parse(PostgresPort),
            Username = SuperuserUsername,
            Password = SuperuserPassword,
            Database = databaseName,
            Timeout = 3,
            CommandTimeout = 30,
        }.ConnectionString;

    /// <summary>
    /// The connection the application uses in production shape: a role that owns nothing and has
    /// NOBYPASSRLS, so <c>FORCE ROW LEVEL SECURITY</c> actually applies to it. Testing isolation as
    /// the superuser would prove nothing — the superuser bypasses every policy.
    /// </summary>
    public static string ApplicationRoleConnectionString()
        => new NpgsqlConnectionStringBuilder
        {
            Host = PostgresHost,
            Port = int.Parse(PostgresPort),
            Username = ApplicationRoleName,
            Password = ApplicationRolePassword,
            Database = TestDatabaseName,
            Timeout = 3,
            CommandTimeout = 30,
        }.ConnectionString;

    public static string MongoConnectionString() => $"mongodb://{MongoHost}:{MongoPort}";

    public static async Task<bool> IsPostgresReachableAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(AdminConnectionString());
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await connection.OpenAsync(timeout.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> IsMongoReachableAsync()
    {
        try
        {
            var settings = MongoClientSettings.FromConnectionString(MongoConnectionString());
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
            settings.ConnectTimeout = TimeSpan.FromSeconds(3);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            await new MongoClient(settings)
                .GetDatabase("admin")
                .RunCommandAsync<MongoDB.Bson.BsonDocument>(
                    new MongoDB.Bson.BsonDocument("ping", 1),
                    cancellationToken: timeout.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
