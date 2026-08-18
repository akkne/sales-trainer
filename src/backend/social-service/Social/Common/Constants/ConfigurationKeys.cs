namespace Sellevate.Social.Common.Constants;

/// <summary>
/// The configuration keys this service reads directly at startup, before the options pattern is
/// available. Everything a request path needs is bound to a configuration class instead — see
/// <c>Infrastructure/Configuration</c> — so this list stays short by design.
///
/// <para>
/// The names must match <c>appsettings.json</c> and the environment variables docker-compose sets from
/// the root <c>.env</c> (a section separator is a double underscore there, so <c>Jwt:Key</c> arrives as
/// <c>Jwt__Key</c>). A typo here does not fail the build: it silently reads null, which is why the JWT
/// key is validated explicitly at startup rather than at first request.
/// </para>
/// </summary>
public static class ConfigurationKeys
{
    /// <summary>Names of the entries under <c>ConnectionStrings</c>.</summary>
    public static class ConnectionStringNames
    {
        public const string Postgres = "Postgres";
        public const string Mongo = "Mongo";
        public const string Redis = "Redis";
    }

    public const string JwtKey = "Jwt:Key";
    public const string JwtIssuer = "Jwt:Issuer";
    public const string JwtAudience = "Jwt:Audience";

    /// <summary>Comma-separated list of origins allowed through CORS.</summary>
    public const string FrontendUrl = "Frontend:Url";

    public const string LokiUrl = "Logging:Loki:Url";
}
