namespace Sellevate.Company.Common.Constants;

/// <summary>
/// Configuration paths company-service reads directly rather than through an
/// <c>IOptions&lt;T&gt;</c> section — startup-time values (JWT, CORS, logging sink) and the internal
/// service secret. A key spelled wrong does not fail the build and does not throw: it silently
/// reads back null, which for the secret means every outbound AI call goes out unauthenticated. That
/// is the reason these are named constants and not string literals at the call site.
/// </summary>
public static class CompanyConfigurationKeys
{
    /// <summary>Name of the Postgres entry under <c>ConnectionStrings</c>.</summary>
    public const string PostgresConnectionName = "Postgres";

    /// <summary>
    /// Environment variable the design-time <c>DbContext</c> factory reads, because
    /// <c>dotnet ef</c> runs without the application's configuration pipeline.
    /// </summary>
    public const string PostgresConnectionEnvironmentVariable = "ConnectionStrings__Postgres";

    /// <summary>Base URL of the Loki sink Serilog writes to.</summary>
    public const string LokiUrl = "Logging:Loki:Url";

    /// <summary>HMAC-SHA256 signing key for inbound access tokens. Never has a code default.</summary>
    public const string JwtKey = "Jwt:Key";

    /// <summary>Expected token issuer.</summary>
    public const string JwtIssuer = "Jwt:Issuer";

    /// <summary>Expected token audience.</summary>
    public const string JwtAudience = "Jwt:Audience";

    /// <summary>Comma-separated list of browser origins allowed through CORS.</summary>
    public const string FrontendUrl = "Frontend:Url";

    /// <summary>
    /// Shared secret sent on every outbound call to ai-service. When unset, the header is omitted
    /// and ai-service leaves its internal endpoints open — the dev/single-service shape.
    /// </summary>
    public const string InternalServiceSecret = "InternalAuth:ServiceSecret";
}
