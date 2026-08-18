using Sellevate.Analytics.Features.Tracking.Services.Abstract;
using Sellevate.Analytics.Features.Tracking.Services.Implementation;

namespace Sellevate.Analytics.Features.Tracking;

/// <summary>
/// Tracking registration. The recorder is a singleton: it owns no state, and the Prometheus counters
/// it writes to are process-wide anyway.
/// </summary>
public static class TrackingServiceCollectionExtensions
{
    public static IServiceCollection AddTrackingFeatureServices(this IServiceCollection services)
    {
        services.AddSingleton<IUsageEventRecorder, UsageEventRecorder>();
        return services;
    }
}
