using Prometheus;

namespace Sellevate.Analytics.Infrastructure.Metrics;

/// <summary>
/// The service's Prometheus metric catalogue, declared once in one place so nothing creates a metric
/// inline.
///
/// <para>
/// <b>Every name, label name and help string here is an invariant, not a setting.</b> Renaming a
/// metric or a label silently empties the Grafana dashboards and alert rules that select on it,
/// leaving a flat graph rather than an error — so a change here is a change to docs/MONITORING.md and
/// the dashboards, in the same commit. That is also why none of this belongs in configuration.
/// </para>
///
/// <para>
/// No metric carries an organization label, and none may be given one. A customer id would put
/// customer identities into the metrics store and make cardinality grow with the customer list, to
/// answer a question that belongs in a product report. The only labels used are drawn from sets
/// compiled into the platform, which is what bounds them.
/// </para>
/// </summary>
public static class AppMetrics
{
    public static Gauge UsersOnline { get; } = Prometheus.Metrics.CreateGauge(
        "app_users_online",
        "Number of distinct users active within the presence window.");

    public static Counter AuthenticatedRequests { get; } = Prometheus.Metrics.CreateCounter(
        "app_authenticated_requests_total",
        "Total authenticated backend requests (excludes infra paths like /metrics).");

    public static Counter PageViews { get; } = Prometheus.Metrics.CreateCounter(
        "app_page_views_total",
        "Total frontend page views.",
        new CounterConfiguration { LabelNames = ["page"] });

    public static Counter Events { get; } = Prometheus.Metrics.CreateCounter(
        "app_events_total",
        "Total frontend UI events (clicks/actions).",
        new CounterConfiguration { LabelNames = ["event", "page"] });

    public static Counter Registrations { get; } = Prometheus.Metrics.CreateCounter(
        "app_registrations_total",
        "Total completed registrations (email verified).");

    public static Counter ExercisesCompleted { get; } = Prometheus.Metrics.CreateCounter(
        "app_exercises_completed_total",
        "Total exercises completed across all users.");

    public static Counter ExperiencePointsGranted { get; } = Prometheus.Metrics.CreateCounter(
        "app_experience_points_granted_total",
        "Total experience points granted across all users.");

    /// <summary>
    /// Phase 40.25. How many assignment issues the platform has performed — the first step of the
    /// funnel in docs/TENANCY/ASSIGNMENTS.md §4, counted once per recipient.
    ///
    /// <para>
    /// <b>No organization label, on purpose and for the third time in this file.</b> A customer id
    /// here would put identities into the metrics store and make cardinality grow with the customer
    /// list. The per-organization funnel the РОП actually reads is computed in learning-service,
    /// where the progress rows are — see docs/ANALYTICS_SERVICE.md.
    /// </para>
    /// </summary>
    public static Counter AssignmentsIssued { get; } = Prometheus.Metrics.CreateCounter(
        "app_assignments_issued_total",
        "Total assignment issues across all organizations (one per recipient).");

    /// <summary>
    /// Phase 40.25. Movements between the four assignment progress states, labelled by the state
    /// arrived at.
    ///
    /// <para>
    /// The label is bounded at four values that are compiled into the platform, which is what makes
    /// it a safe label where an organization id would not be. It answers the operational question —
    /// "is anybody finishing assignments, and how many are failing the threshold" — and deliberately
    /// cannot answer "which team", because that question belongs in a product report.
    /// </para>
    /// </summary>
    public static Counter AssignmentProgressTransitions { get; } = Prometheus.Metrics.CreateCounter(
        "app_assignment_progress_total",
        "Assignment progress state transitions across all organizations.",
        new CounterConfiguration { LabelNames = ["state"] });
}
