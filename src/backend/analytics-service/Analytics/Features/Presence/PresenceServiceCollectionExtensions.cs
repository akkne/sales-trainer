using Sellevate.Analytics.Features.Presence.Services.Abstract;
using Sellevate.Analytics.Features.Presence.Services.Implementation;
using Sellevate.Analytics.Infrastructure.Configuration;

namespace Sellevate.Analytics.Features.Presence;

/// <summary>
/// Presence registration. The tracker is a singleton because it holds no per-request state — which
/// is also why every one of its members takes the organization as a parameter — and the gauge
/// updater is a hosted service because Prometheus pulls, so something has to push into the gauge on
/// a timer.
/// </summary>
public static class PresenceServiceCollectionExtensions
{
    public static IServiceCollection AddPresenceFeatureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<PresenceConfiguration>(
            configuration.GetSection(PresenceConfiguration.SectionName));
        services.AddSingleton<IPresenceTracker, PresenceTracker>();
        services.AddHostedService<PresenceGaugeUpdaterService>();
        return services;
    }
}
