using Sellevate.Learning.Features.Content.Services.Abstract;
using Sellevate.Learning.Features.Content.Services.Implementation;

namespace Sellevate.Learning.Features.Content;

public static class ContentServiceCollectionExtensions
{
    public static IServiceCollection AddContentOverrideServices(this IServiceCollection services)
    {
        services.AddScoped<IContentOverrideService, ContentOverrideService>();

        // Phase 40.19. Scoped, not singleton: the memo inside it is a per-request memo, and the
        // profile it reads is whichever tenant's the request belongs to.
        services.AddScoped<IOrganizationProfileProvider, OrganizationProfileProvider>();

        return services;
    }
}
