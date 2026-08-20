using Sellevate.Identity.Features.Organizations.Services.Abstract;
using Sellevate.Identity.Features.Organizations.Services.Implementation;

namespace Sellevate.Identity.Features.Organizations;

/// <summary>The feature's single registration point, so <c>Program.cs</c> never names an
/// implementation type.</summary>
public static class OrganizationBootstrapServiceCollectionExtensions
{
    public static IServiceCollection AddOrganizationBootstrapFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationBootstrapService, OrganizationBootstrapService>();
        return services;
    }
}
