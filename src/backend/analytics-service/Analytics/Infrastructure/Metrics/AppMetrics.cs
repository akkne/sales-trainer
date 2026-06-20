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
}
