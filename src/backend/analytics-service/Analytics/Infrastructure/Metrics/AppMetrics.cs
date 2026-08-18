using Prometheus;

namespace Sellevate.Analytics.Infrastructure.Metrics;

public static class AppMetrics
{
    public static readonly Gauge UsersOnline = Prometheus.Metrics.CreateGauge(
        "app_users_online",
        "Number of distinct users active within the presence window.");

    public static readonly Counter AuthenticatedRequests = Prometheus.Metrics.CreateCounter(
        "app_authenticated_requests_total",
        "Total authenticated backend requests (excludes infra paths like /metrics).");

    public static readonly Counter PageViews = Prometheus.Metrics.CreateCounter(
        "app_page_views_total",
        "Total frontend page views.",
        new CounterConfiguration { LabelNames = ["page"] });

    public static readonly Counter Events = Prometheus.Metrics.CreateCounter(
        "app_events_total",
        "Total frontend UI events (clicks/actions).",
        new CounterConfiguration { LabelNames = ["event", "page"] });

    public static readonly Counter Registrations = Prometheus.Metrics.CreateCounter(
        "app_registrations_total",
        "Total completed registrations (email verified).");

    public static readonly Counter ExercisesCompleted = Prometheus.Metrics.CreateCounter(
        "app_exercises_completed_total",
        "Total exercises completed across all users.");

    public static readonly Counter ExperiencePointsGranted = Prometheus.Metrics.CreateCounter(
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
    public static readonly Counter AssignmentsIssued = Prometheus.Metrics.CreateCounter(
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
    public static readonly Counter AssignmentProgressTransitions = Prometheus.Metrics.CreateCounter(
        "app_assignment_progress_total",
        "Assignment progress state transitions across all organizations.",
        new CounterConfiguration { LabelNames = ["state"] });
}
