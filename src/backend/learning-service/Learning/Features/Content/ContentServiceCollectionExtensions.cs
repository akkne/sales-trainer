using Sellevate.Learning.Features.Content.Services.Abstract;
using Sellevate.Learning.Features.Content.Services.Implementation;

namespace Sellevate.Learning.Features.Content;

public static class ContentServiceCollectionExtensions
{
    public static IServiceCollection AddContentOverrideServices(this IServiceCollection services)
    {
        services.AddScoped<IContentOverrideService, ContentOverrideService>();

        return services;
    }
}
