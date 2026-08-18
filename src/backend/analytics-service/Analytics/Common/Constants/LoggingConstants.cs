namespace Sellevate.Analytics.Common.Constants;

/// <summary>
/// The Loki label names and values this service stamps on every log line, plus the Serilog
/// enrichment property names. These are query keys, not cosmetics: Grafana dashboards and log
/// queries select on <c>service="sellevate-analytics"</c>, so renaming a value here silently empties
/// a saved query. Treat them as invariants — see docs/MONITORING.md.
/// </summary>
public static class LoggingConstants
{
    public const string ConsoleOutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    public const string ServiceLabelName = "service";
    public const string ServiceLabelValue = "sellevate-analytics";
    public const string EnvironmentLabelName = "env";

    public const string ApplicationPropertyName = "Application";
    public const string ApplicationPropertyValue = "Sellevate.Analytics";

    public static readonly IReadOnlyList<string> PropertiesPromotedToLabels = ["RequestId", "UserId"];
}
