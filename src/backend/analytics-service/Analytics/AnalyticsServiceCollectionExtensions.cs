using Sellevate.Analytics.Common.Constants;
using Sellevate.Analytics.Features.Funnels;
using Sellevate.Analytics.Features.Presence;
using Sellevate.Analytics.Features.Tracking;
using StackExchange.Redis;

namespace Sellevate.Analytics;

/// <summary>
/// The service's composition root: the host calls these two methods and registers nothing itself.
/// </summary>
public static class AnalyticsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared Redis multiplexer — the only store analytics-service has. Must be called
    /// <b>before</b> <c>AddSellevateEventing</c>, whose idempotency store resolves it.
    ///
    /// <para>
    /// The connection is opened lazily inside the factory rather than at registration time, which is
    /// what lets the integration-test host swap in a stub before anything dials Redis.
    /// </para>
    /// </summary>
    public static IServiceCollection AddAnalyticsRedisConnection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration.GetConnectionString(ConfigurationKeys.RedisConnectionName)!));
        return services;
    }

    /// <summary>
    /// Registers the three bounded contexts of this service — tracking, presence and funnels.
    /// </summary>
    public static IServiceCollection AddAnalyticsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddTrackingFeatureServices()
            .AddPresenceFeatureServices(configuration)
            .AddFunnelsFeatureServices();
        return services;
    }
}
