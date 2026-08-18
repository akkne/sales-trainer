using Npgsql;

namespace Sellevate.BuildingBlocks.Tests.Tenancy.Support;

/// <summary>
/// Connection settings for the real-Postgres RLS integration test
/// (<see cref="TenantRowLevelSecurityIntegrationTests"/>). Defaults match the credentials
/// <c>scripts/dev-infra.sh</c> / <c>scripts/lib-local-env.sh</c> already use for local Docker
/// Postgres (port from <c>LOCAL_POSTGRES_PORT</c>, superuser <c>st</c>) so the test runs with zero
/// configuration against a developer's already-running dev-infra stack, and stays overridable via
/// environment variables for CI or a different local port.
/// </summary>
internal static class LocalPostgresTestSettings
{
    private static readonly string Host = Environment.GetEnvironmentVariable("TENANCY_RLS_TEST_POSTGRES_HOST") ?? "localhost";
    private static readonly string Port = Environment.GetEnvironmentVariable("LOCAL_POSTGRES_PORT") ?? "5433";
    private static readonly string SuperuserUsername = Environment.GetEnvironmentVariable("TENANCY_RLS_TEST_POSTGRES_SUPERUSER") ?? "st";
    private static readonly string SuperuserPassword = Environment.GetEnvironmentVariable("TENANCY_RLS_TEST_POSTGRES_SUPERUSER_PASSWORD") ?? "st_secret";

    public const string TestDatabaseName = "tenancy_rls_integration_test";
    public const string ApplicationRoleName = "sellevate_app_test";
    public const string ApplicationRolePassword = "tenancy_rls_test_only_password";

    public static string AdminConnectionString(string databaseName = "postgres")
        => new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = int.Parse(Port),
            Username = SuperuserUsername,
            Password = SuperuserPassword,
            Database = databaseName,
            Timeout = 3,
            CommandTimeout = 5,
        }.ConnectionString;

    public static string ApplicationRoleConnectionString()
        => new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = int.Parse(Port),
            Username = ApplicationRoleName,
            Password = ApplicationRolePassword,
            Database = TestDatabaseName,
            Timeout = 3,
            CommandTimeout = 5,
        }.ConnectionString;

    /// <summary>
    /// A short, bounded reachability probe. Per CLAUDE.md, this test must never hang waiting for
    /// infrastructure that is not running — if the connection does not open within a few seconds,
    /// the caller skips the whole fixture instead of failing it.
    /// </summary>
    public static async Task<bool> IsReachableAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(AdminConnectionString());
            using var timeoutCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await connection.OpenAsync(timeoutCancellationTokenSource.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
