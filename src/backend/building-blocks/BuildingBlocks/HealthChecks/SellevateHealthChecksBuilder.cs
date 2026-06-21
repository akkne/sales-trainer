using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Sellevate.BuildingBlocks.HealthChecks;

/// <summary>
/// Fluent builder returned by
/// <see cref="HealthCheckServiceCollectionExtensions.AddSellevateHealthChecks"/> for adding
/// the shared readiness probes that every service composes from.
/// </summary>
public sealed class SellevateHealthChecksBuilder
{
    private readonly IHealthChecksBuilder _healthChecksBuilder;

    internal SellevateHealthChecksBuilder(IHealthChecksBuilder healthChecksBuilder)
    {
        _healthChecksBuilder = healthChecksBuilder;
    }

    /// <summary>Adds the Redis readiness probe (requires a registered <c>IConnectionMultiplexer</c>).</summary>
    public SellevateHealthChecksBuilder AddRedis()
    {
        _healthChecksBuilder.AddCheck<RedisHealthCheck>(
            HealthCheckConstants.RedisCheckName,
            failureStatus: HealthStatus.Unhealthy,
            tags: [HealthCheckConstants.ReadinessTag]);
        return this;
    }

    /// <summary>Adds the Kafka broker readiness probe (uses the bound <c>KafkaSettings</c>).</summary>
    public SellevateHealthChecksBuilder AddKafka()
    {
        _healthChecksBuilder.AddCheck<KafkaHealthCheck>(
            HealthCheckConstants.KafkaCheckName,
            failureStatus: HealthStatus.Unhealthy,
            tags: [HealthCheckConstants.ReadinessTag]);
        return this;
    }
}
