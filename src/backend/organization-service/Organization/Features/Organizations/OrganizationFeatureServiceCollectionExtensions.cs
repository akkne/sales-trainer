using Sellevate.Organization.Features.Organizations.Services.Abstract;
using Sellevate.Organization.Features.Organizations.Services.Implementation;

namespace Sellevate.Organization.Features.Organizations;

/// <summary>
/// The feature's single registration point, so <c>Program.cs</c> never names an implementation type.
/// Both services are <c>Scoped</c> and must stay that way: they take the tenant-filtered
/// <c>OrganizationDbContext</c>, which closes over the per-request <c>ITenantContext</c>, so a longer
/// lifetime would pin one organization's filter onto every later request.
/// </summary>
public static class OrganizationFeatureServiceCollectionExtensions
{
    public static IServiceCollection AddOrganizationFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IOrganizationProfileService, OrganizationProfileService>();
        return services;
    }
}
