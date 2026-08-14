using Sellevate.Organization.Features.Organizations.Services.Abstract;
using Sellevate.Organization.Features.Organizations.Services.Implementation;

namespace Sellevate.Organization.Features.Organizations;

public static class OrganizationFeatureServiceCollectionExtensions
{
    public static IServiceCollection AddOrganizationFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IOrganizationProfileService, OrganizationProfileService>();
        return services;
    }
}
