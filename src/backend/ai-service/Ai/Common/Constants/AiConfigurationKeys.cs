namespace Sellevate.Ai.Common.Constants;

/// <summary>
/// Configuration keys read by path rather than through an options class, stated once so a rename in
/// <c>appsettings.json</c> cannot silently start returning null on one of several readers.
///
/// <para>
/// Everything here is either a connection string, a secret, or read before the container exists, which
/// is why it is not bound to an options type. Anything an operator tunes belongs in a configuration
/// class under <c>Infrastructure/Configuration</c> instead — see <c>docs/CONFIGURATION.md</c>.
/// </para>
/// </summary>
public static class AiConfigurationKeys
{
    /// <summary>Named connection string for ai-db. Injected from the environment in every deployment.</summary>
    public const string PostgresConnectionStringName = "Postgres";

    /// <summary>Named connection string for the dialog-session store.</summary>
    public const string MongoConnectionStringName = "Mongo";

    /// <summary>Named connection string for the voice counters and the audio cache.</summary>
    public const string RedisConnectionStringName = "Redis";

    /// <summary>HMAC signing key for the platform JWT. A secret; never has a default.</summary>
    public const string JwtSigningKey = "Jwt:Key";

    public const string JwtIssuer = "Jwt:Issuer";

    public const string JwtAudience = "Jwt:Audience";

    /// <summary>Comma-separated list of browser origins allowed through CORS.</summary>
    public const string FrontendUrl = "Frontend:Url";

    /// <summary>Loki ingestion endpoint. Read before the container exists, during host bootstrap.</summary>
    public const string LokiUrl = "Logging:Loki:Url";
}
