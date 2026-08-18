namespace Sellevate.Organization.Common.Constants;

/// <summary>
/// Names and tags for this service's own health-check registrations. The readiness tag has to match
/// the one <c>BuildingBlocks.HealthChecks</c> filters <c>/health/ready</c> by, otherwise the check is
/// registered and never aggregated — a probe that reports healthy while Postgres is down.
/// </summary>
public static class OrganizationHealthCheckConstants
{
    public const string PostgresCheckName = "postgres";
    public const string ReadinessTag = "ready";
}
