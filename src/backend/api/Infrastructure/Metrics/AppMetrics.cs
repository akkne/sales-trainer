using Prometheus;

namespace SalesTrainer.Api.Infrastructure.Metrics;

/// <summary>
/// Central catalog of product / usage metrics exposed at <c>/metrics</c> alongside the
/// default prometheus-net HTTP metrics. All instances are process-global statics — the
/// idiomatic prometheus-net pattern — and self-register with the default registry that
/// <c>MapMetrics()</c> already serves, so no DI wiring is required.
///
/// Cardinality is the central risk: only <c>app_users_online</c> is unlabeled, label
/// values for page/event are constrained to a server-side whitelist (see
/// <c>Features.Metrics.Constants.TrackedEvents</c>), and "visits per day/week" are NOT
/// stored — they are derived in Prometheus with <c>increase(counter[1d])</c> /
/// <c>increase(counter[7d])</c> over the monotonic counters below.
/// </summary>
public static class AppMetrics
{
    /// <summary>Distinct users seen in the last few minutes. Set by the presence updater.</summary>
    public static readonly Gauge UsersOnline = Prometheus.Metrics.CreateGauge(
        "app_users_online",
        "Number of distinct users active within the presence window.");

    /// <summary>Authenticated backend requests — a proxy for visits / activity over time.</summary>
    public static readonly Counter AuthenticatedRequests = Prometheus.Metrics.CreateCounter(
        "app_authenticated_requests_total",
        "Total authenticated backend requests (excludes infra paths like /metrics).");

    /// <summary>Frontend page views, by bounded page name.</summary>
    public static readonly Counter PageViews = Prometheus.Metrics.CreateCounter(
        "app_page_views_total",
        "Total frontend page views.",
        new CounterConfiguration { LabelNames = ["page"] });

    /// <summary>Discrete UI click / action events, by bounded event + page name.</summary>
    public static readonly Counter Events = Prometheus.Metrics.CreateCounter(
        "app_events_total",
        "Total frontend UI events (clicks/actions).",
        new CounterConfiguration { LabelNames = ["event", "page"] });

    /// <summary>Successful logins, by authentication method (closed enum).</summary>
    public static readonly Counter Logins = Prometheus.Metrics.CreateCounter(
        "app_logins_total",
        "Total successful logins.",
        new CounterConfiguration { LabelNames = ["method"] });

    /// <summary>Completed registrations (email verified).</summary>
    public static readonly Counter Registrations = Prometheus.Metrics.CreateCounter(
        "app_registrations_total",
        "Total completed registrations (email verified).");
}
