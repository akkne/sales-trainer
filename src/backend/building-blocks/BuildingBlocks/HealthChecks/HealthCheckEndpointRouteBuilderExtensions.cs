using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Sellevate.BuildingBlocks.HealthChecks;

/// <summary>
/// Maps the two shared health endpoints with the uniform JSON writer so every service
/// exposes <see cref="HealthCheckConstants.LivenessEndpoint"/> (process up) and
/// <see cref="HealthCheckConstants.ReadinessEndpoint"/> (dependencies reachable).
/// </summary>
public static class HealthCheckEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapSellevateHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(HealthCheckConstants.LivenessEndpoint, new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = HealthCheckResponseWriter.WriteAsync,
        });

        endpoints.MapHealthChecks(HealthCheckConstants.ReadinessEndpoint, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(HealthCheckConstants.ReadinessTag),
            ResponseWriter = HealthCheckResponseWriter.WriteAsync,
        });

        return endpoints;
    }
}
