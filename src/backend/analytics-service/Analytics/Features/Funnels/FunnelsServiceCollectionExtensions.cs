using Sellevate.Analytics.Features.Funnels.Eventing;
using Sellevate.Analytics.Features.Funnels.Services.Abstract;
using Sellevate.Analytics.Features.Funnels.Services.Implementation;

namespace Sellevate.Analytics.Features.Funnels;

/// <summary>
/// Funnels registration. The recorder is a singleton because it is stateless and the consumer that
/// drives it is a singleton hosted service; injecting a scoped recorder there would be a captured
/// dependency.
/// </summary>
public static class FunnelsServiceCollectionExtensions
{
    public static IServiceCollection AddFunnelsFeatureServices(this IServiceCollection services)
    {
        services.AddSingleton<IFunnelEventRecorder, FunnelEventRecorder>();
        services.AddHostedService<FunnelEventsConsumer>();
        return services;
    }
}
