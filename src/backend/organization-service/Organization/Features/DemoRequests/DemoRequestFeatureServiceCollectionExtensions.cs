using Sellevate.Organization.Features.DemoRequests.Services.Abstract;
using Sellevate.Organization.Features.DemoRequests.Services.Implementation;
using Sellevate.Organization.Infrastructure.Configuration;

namespace Sellevate.Organization.Features.DemoRequests;

/// <summary>
/// The feature's single registration point, so <c>Program.cs</c> never names an implementation type
/// nor binds <see cref="DemoRequestConfiguration"/> itself. <see cref="IDemoRequestService"/> is
/// <c>Scoped</c> because it takes the request-scoped <c>OrganizationDbContext</c>;
/// <see cref="IDemoRequestNotificationComposer"/> is <c>Singleton</c> because it is a pure formatter
/// with no per-request state.
/// </summary>
public static class DemoRequestFeatureServiceCollectionExtensions
{
    public static IServiceCollection AddDemoRequestFeatureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DemoRequestConfiguration>(
            configuration.GetSection(DemoRequestConfiguration.SectionName));

        services.AddScoped<IDemoRequestService, DemoRequestService>();
        services.AddSingleton<IDemoRequestNotificationComposer, DemoRequestNotificationComposer>();

        return services;
    }
}
