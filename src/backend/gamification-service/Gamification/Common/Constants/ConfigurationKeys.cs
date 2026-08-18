namespace Sellevate.Gamification.Common.Constants;

/// <summary>
/// The configuration paths read straight off <c>IConfiguration</c> during startup, before the
/// Options pattern is wired, together with the local-development fallbacks those reads default to.
/// Anything read after startup goes through <c>IOptions&lt;T&gt;</c> and belongs in
/// <c>Infrastructure/Configuration</c> instead, not here.
/// </summary>
public static class ConfigurationKeys
{
    public const string LokiUrl = "Logging:Loki:Url";

    /// <summary>
    /// Named connection string of gamification-db. Read three times at startup — by the
    /// <c>DbContext</c> registration, by Hangfire's own storage, and by the bootstrapper that
    /// creates the database — which is why it is a constant rather than three literals that could
    /// drift into pointing at different databases.
    /// </summary>
    public const string PostgresConnectionName = "Postgres";

    public const string RedisConnectionName = "Redis";

    public const string JwtSigningKey = "Jwt:Key";
    public const string JwtIssuer = "Jwt:Issuer";
    public const string JwtAudience = "Jwt:Audience";

    /// <summary>
    /// May carry a comma-separated origin list; every entry becomes an allowed CORS origin.
    /// </summary>
    public const string FrontendUrl = "Frontend:Url";

    /// <summary>
    /// Environment variable the design-time factory reads, spelled with ASP.NET's double-underscore
    /// section separator because <c>dotnet ef</c> runs with no configuration provider at all.
    /// </summary>
    public const string PostgresConnectionEnvironmentVariable = "ConnectionStrings__Postgres";

    /// <summary>
    /// The maintenance database the bootstrapper connects to in order to issue
    /// <c>CREATE DATABASE</c>: it cannot connect to the database it is about to create.
    /// </summary>
    public const string MaintenanceDatabaseName = "postgres";

    public const string DefaultLokiUrl = "http://loki:3100";

    public const string DefaultFrontendUrl = "http://localhost:3000";

    /// <summary>
    /// Local-development fallback for <see cref="PostgresConnectionEnvironmentVariable"/>, used only
    /// by <c>dotnet ef</c> at design time. Never a production credential — the running service reads
    /// its connection string from the environment.
    /// </summary>
    public const string DesignTimePostgresConnectionString =
        "Host=localhost;Port=5432;Database=gamification;Username=postgres;Password=postgres";
}
