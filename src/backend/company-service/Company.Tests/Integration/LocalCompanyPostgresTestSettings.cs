using Npgsql;

namespace Sellevate.Company.Tests.Integration;

/// <summary>
/// Connection settings for the real-Postgres tenant-isolation test
/// (<see cref="CompanyTenantIsolationIntegrationTests"/>), deliberately mirroring
/// <c>Learning.Tests/Integration/LocalLearningPostgresTestSettings</c>: same defaults
/// (<c>LOCAL_POSTGRES_PORT</c>, superuser <c>st</c>) as <c>scripts/dev-infra.sh</c> already uses,
/// so the fixture runs with zero configuration against a developer's running dev-infra stack.
///
/// <para>
/// Its own throwaway database and its own throwaway login role, both named distinctly from the
/// other services' fixtures so they can run in the same session without fighting over a database
/// name. Neither ever touches the real <c>company</c> database.
/// </para>
/// </summary>
internal static class LocalCompanyPostgresTestSettings
{
    private static readonly string Host = Environment.GetEnvironmentVariable("TENANCY_RLS_TEST_POSTGRES_HOST") ?? "localhost";
    private static readonly string Port = Environment.GetEnvironmentVariable("LOCAL_POSTGRES_PORT") ?? "5433";
    private static readonly string SuperuserUsername = Environment.GetEnvironmentVariable("TENANCY_RLS_TEST_POSTGRES_SUPERUSER") ?? "st";
    private static readonly string SuperuserPassword = Environment.GetEnvironmentVariable("TENANCY_RLS_TEST_POSTGRES_SUPERUSER_PASSWORD") ?? "st_secret";

    public const string TestDatabaseName = "company_tenancy_integration_test";
    public const string ApplicationRoleName = "sellevate_company_app_test";
    public const string ApplicationRolePassword = "company_tenancy_test_only_password";

    public static string AdminConnectionString(string databaseName = "postgres")
        => new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = int.Parse(Port),
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
            Host = Host,
            Port = int.Parse(Port),
            Username = ApplicationRoleName,
            Password = ApplicationRolePassword,
            Database = TestDatabaseName,
            Timeout = 3,
            CommandTimeout = 30,
        }.ConnectionString;

    /// <summary>
    /// A short, bounded reachability probe. Per CLAUDE.md and docs/DONT_FORGET.md this fixture must
    /// never hang waiting for infrastructure that is not running: if the connection does not open
    /// within a few seconds, every test skips in seconds instead of blocking the build.
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
