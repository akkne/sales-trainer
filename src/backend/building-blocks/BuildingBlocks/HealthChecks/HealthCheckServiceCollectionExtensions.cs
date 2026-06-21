using Microsoft.Extensions.DependencyInjection;

namespace Sellevate.BuildingBlocks.HealthChecks;

/// <summary>
/// Registration helpers so each service wires the shared liveness/readiness checks with
/// one fluent call, adding only the readiness probes (Redis, Kafka) that match the
/// dependencies it actually owns. Service-specific checks (PostgreSQL, MongoDB) are added
/// by the service via the standard <c>AspNetCore.HealthChecks.*</c> packages with the
/// shared <see cref="HealthCheckConstants.ReadinessTag"/> and check-name constants.
/// </summary>
public static class HealthCheckServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ASP.NET Core health-check services and returns a builder for adding
    /// the shared readiness probes. The liveness endpoint always reports the process is up;
    /// readiness aggregates every check tagged <see cref="HealthCheckConstants.ReadinessTag"/>.
    /// </summary>
    public static SellevateHealthChecksBuilder AddSellevateHealthChecks(this IServiceCollection services)
    {
        var healthChecksBuilder = services.AddHealthChecks();
        return new SellevateHealthChecksBuilder(healthChecksBuilder);
    }
}
