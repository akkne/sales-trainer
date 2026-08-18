namespace Sellevate.Ai.Common.Constants;

/// <summary>
/// Identifiers this service is known by in the observability stack.
///
/// <para>
/// These are join keys rather than labels: <c>infrastructure/grafana/dashboards/ai-spend.json</c> and the
/// panel queries in <c>docs/MONITORING.md</c> select on <see cref="ServiceLabel"/>, so changing it makes
/// every dashboard silently return no data. <c>ai-service</c> is the third process to export
/// <c>/metrics</c> (after the gateway and analytics-service), because it is the only place per-organization
/// AI spend is known at the instant it happens — the exported series carry no organization label, see
/// <c>AiSpendMetrics</c>.
/// </para>
/// </summary>
public static class AiObservabilityDefaults
{
    /// <summary>Value of the Loki <c>service</c> label and of the Prometheus <c>job</c> Grafana selects on.</summary>
    public const string ServiceLabel = "sellevate-ai";

    /// <summary>Value of the Serilog <c>Application</c> enrichment property.</summary>
    public const string ApplicationName = "Sellevate.Ai";

    /// <summary>Loki endpoint used when none is configured — the compose-network hostname.</summary>
    public const string LokiUrl = "http://loki:3100";
}
