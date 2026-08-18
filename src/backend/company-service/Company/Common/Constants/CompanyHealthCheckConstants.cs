namespace Sellevate.Company.Common.Constants;

/// <summary>
/// Names the orchestrator's probes match on. The tag is what separates readiness from liveness: a
/// check registered without <see cref="ReadinessTag"/> is invisible to the readiness endpoint, so a
/// service with an unreachable database would report itself ready to receive traffic.
/// </summary>
public static class CompanyHealthCheckConstants
{
    public const string PostgresCheckName = "postgres";
    public const string ReadinessTag = "ready";
}
