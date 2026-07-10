using Sellevate.Ai.Features.Companies.Services.Abstract;
using Sellevate.Ai.Features.Companies.Services.Implementation;

namespace Sellevate.Ai.Features.Companies;

public static class BriefingServiceCollectionExtensions
{
    public static IServiceCollection AddBriefingFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IBriefingService, BriefingService>();
        return services;
    }
}
