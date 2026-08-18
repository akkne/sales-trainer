namespace Sellevate.Identity.Common.Constants;

/// <summary>
/// Configuration and connection-string keys read directly off <c>IConfiguration</c> during host
/// construction, before the options pattern is available. Every other read goes through a strongly
/// typed configuration class in <c>Infrastructure/Configuration</c> (CODESTYLE §8).
/// </summary>
public static class ConfigurationKeys
{
    public const string PostgresConnectionName = "Postgres";
    public const string RedisConnectionName = "Redis";
    public const string LokiUrl = "Logging:Loki:Url";
    public const string JwtKey = "Jwt:Key";
    public const string JwtIssuer = "Jwt:Issuer";
    public const string JwtAudience = "Jwt:Audience";
    public const string FrontendUrl = "Frontend:Url";
}
