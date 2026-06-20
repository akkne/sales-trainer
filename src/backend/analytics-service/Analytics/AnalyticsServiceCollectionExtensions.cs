using Sellevate.Analytics.Features.Funnels;
using Sellevate.Analytics.Features.Presence;
using Sellevate.Analytics.Features.Tracking;

namespace Sellevate.Analytics;

public static class AnalyticsServiceCollectionExtensions
{
    public static IServiceCollection AddAnalyticsServices(this IServiceCollection services)
    {
        services
            .AddTrackingFeatureServices()
            .AddPresenceFeatureServices()
            .AddFunnelsFeatureServices();
        return services;
    }
}
