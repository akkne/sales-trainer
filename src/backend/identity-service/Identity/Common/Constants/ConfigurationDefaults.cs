namespace Sellevate.Identity.Common.Constants;

/// <summary>
/// The values the host falls back to when a configuration key is absent. Each one mirrors the
/// default already committed to <c>appsettings.json</c>; they exist so a missing key degrades to the
/// compose-network address instead of a null-reference at startup, not as a second place to tune.
/// </summary>
public static class ConfigurationDefaults
{
    public const string LokiUrl = "http://loki:3100";
    public const string FrontendUrl = "http://localhost:3000";
}
