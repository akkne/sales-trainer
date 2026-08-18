namespace Sellevate.Notification.Common.Constants;

/// <summary>
/// The configuration paths read straight off <c>IConfiguration</c> during startup, before the
/// Options pattern is wired, together with the local-development fallbacks those reads default to.
/// Anything read after startup goes through <c>IOptions&lt;T&gt;</c> and belongs in
/// <c>Infrastructure/Configuration</c> instead, not here.
/// </summary>
public static class ConfigurationKeys
{
    public const string LokiUrl = "Logging:Loki:Url";
    public const string RedisConnectionName = "Redis";
    public const string JwtSigningKey = "Jwt:Key";
    public const string JwtIssuer = "Jwt:Issuer";
    public const string JwtAudience = "Jwt:Audience";

    /// <summary>
    /// May carry a comma-separated origin list: it doubles as the CORS allow-list and as the base
    /// for the absolute action links in notification emails, where only the first entry is the
    /// canonical UI.
    /// </summary>
    public const string FrontendUrl = "Frontend:Url";

    public const string DefaultLokiUrl = "http://loki:3100";

    public const string DefaultFrontendUrl = "http://localhost:3000";
}
