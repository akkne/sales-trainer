using Sellevate.Analytics.Features.Presence.Services.Abstract;
using Sellevate.Analytics.Features.Presence.Services.Implementation;

namespace Sellevate.Analytics.Features.Presence;

public static class PresenceServiceCollectionExtensions
{
    public static IServiceCollection AddPresenceFeatureServices(this IServiceCollection services)
    {
        services.AddSingleton<IPresenceTracker, PresenceTracker>();
        services.AddHostedService<PresenceGaugeUpdaterService>();
        return services;
    }
}
