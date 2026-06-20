using Sellevate.Analytics.Features.Tracking.Services.Abstract;
using Sellevate.Analytics.Features.Tracking.Services.Implementation;

namespace Sellevate.Analytics.Features.Tracking;

public static class TrackingServiceCollectionExtensions
{
    public static IServiceCollection AddTrackingFeatureServices(this IServiceCollection services)
    {
        services.AddSingleton<IUsageEventRecorder, UsageEventRecorder>();
        return services;
    }
}
