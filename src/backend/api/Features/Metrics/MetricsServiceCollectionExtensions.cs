using SalesTrainer.Api.Features.Metrics.Services;
using SalesTrainer.Api.Features.Metrics.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Metrics;

namespace SalesTrainer.Api.Features.Metrics;

public static class MetricsServiceCollectionExtensions
{
    public static IServiceCollection AddMetricsFeatureServices(this IServiceCollection services)
    {
        // Depends only on the singleton IConnectionMultiplexer, so it is safe (and needed,
        // since PresenceGaugeUpdaterService is a singleton hosted service) as a singleton.
        services.AddSingleton<IPresenceTracker, PresenceTracker>();
        services.AddScoped<ActivityTrackingMiddleware>();
        return services;
    }
}
