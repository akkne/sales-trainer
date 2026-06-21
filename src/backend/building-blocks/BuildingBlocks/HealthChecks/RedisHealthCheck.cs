using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Sellevate.BuildingBlocks.HealthChecks;

/// <summary>
/// Readiness check that confirms Redis is reachable by issuing a <c>PING</c> on the
/// shared <see cref="IConnectionMultiplexer"/>. Reports <see cref="HealthStatus.Unhealthy"/>
/// if the connection is down or the ping throws.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis is not reachable.", exception);
        }
    }
}
