using Sellevate.Analytics.Features.Funnels.Eventing;
using Sellevate.Analytics.Features.Funnels.Services.Abstract;
using Sellevate.Analytics.Features.Funnels.Services.Implementation;

namespace Sellevate.Analytics.Features.Funnels;

public static class FunnelsServiceCollectionExtensions
{
    public static IServiceCollection AddFunnelsFeatureServices(this IServiceCollection services)
    {
        services.AddSingleton<IFunnelEventRecorder, FunnelEventRecorder>();
        services.AddHostedService<FunnelEventsConsumer>();
        return services;
    }
}
