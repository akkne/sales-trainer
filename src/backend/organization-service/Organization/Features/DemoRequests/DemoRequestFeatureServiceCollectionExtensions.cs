using Sellevate.Organization.Features.DemoRequests.Services.Abstract;
using Sellevate.Organization.Features.DemoRequests.Services.Implementation;
using Sellevate.Organization.Infrastructure.Configuration;
using Sellevate.Organization.Infrastructure.Identity;

namespace Sellevate.Organization.Features.DemoRequests;

/// <summary>
/// The feature's single registration point, so <c>Program.cs</c> never names an implementation type
/// nor binds <see cref="DemoRequestConfiguration"/> itself. <see cref="IDemoRequestService"/> and
/// <see cref="IDemoRequestProvisioningService"/> are <c>Scoped</c> because both take the
/// request-scoped <c>OrganizationDbContext</c>; <see cref="IDemoRequestNotificationComposer"/> is
/// <c>Singleton</c> because it is a pure formatter with no per-request state.
/// <see cref="FrontendConfiguration"/> is bound here rather than in <c>Program.cs</c> because the
/// approval email's registration link is currently its only reader in this service.
/// <see cref="AddIdentityOrganizationBootstrapClient"/> registers provisioning's typed
/// <see cref="HttpClient"/> to identity-service alongside the rest of the feature that is its only
/// caller.
/// </summary>
public static class DemoRequestFeatureServiceCollectionExtensions
{
    public static IServiceCollection AddDemoRequestFeatureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DemoRequestConfiguration>(
            configuration.GetSection(DemoRequestConfiguration.SectionName));
        services.Configure<FrontendConfiguration>(
            configuration.GetSection(FrontendConfiguration.SectionName));

        services.AddScoped<IDemoRequestService, DemoRequestService>();
        services.AddScoped<IDemoRequestProvisioningService, DemoRequestProvisioningService>();
        services.AddSingleton<IDemoRequestNotificationComposer, DemoRequestNotificationComposer>();
        services.AddIdentityOrganizationBootstrapClient(configuration);

        return services;
    }
}
