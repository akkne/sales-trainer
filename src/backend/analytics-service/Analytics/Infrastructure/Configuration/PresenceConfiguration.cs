namespace Sellevate.Analytics.Infrastructure.Configuration;

/// <summary>
/// Cadences of the presence subsystem. Tuning only — the committed defaults are the values
/// docs/MONITORING.md documents, and changing one changes what a dashboard means.
///
/// <para>
/// <see cref="GaugeRefreshIntervalSeconds"/> must stay <b>shorter than the Prometheus scrape
/// interval</b> (15 s in <c>infrastructure/prometheus/prometheus.yml</c>), otherwise
/// <c>app_users_online</c> is
/// scraped twice from the same stale reading. It is configurable for exactly that reason: an
/// operator who changes the scrape interval must be able to follow without a rebuild.
/// </para>
///
/// <para>
/// <see cref="WindowMinutes"/> is the definition of "online" — it is the window
/// <c>app_users_online</c> counts within, and the number the metric's help text and every
/// presence dashboard describe.
/// </para>
///
/// <para>
/// <see cref="PruneIntervalMinutes"/> is compaction only, deliberately slower than the refresh:
/// counting already filters by the window, so pruning never affects a reported value and exists to
/// stop the sorted sets growing without bound.
/// </para>
/// </summary>
public sealed class PresenceConfiguration
{
    public const string SectionName = "Presence";

    public int WindowMinutes { get; init; } = 5;

    public int GaugeRefreshIntervalSeconds { get; init; } = 20;

    public int PruneIntervalMinutes { get; init; } = 5;
}
