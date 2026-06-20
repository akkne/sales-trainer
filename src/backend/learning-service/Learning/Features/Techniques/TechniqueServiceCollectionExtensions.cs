using Sellevate.Learning.Features.Techniques.Services.Abstract;
using Sellevate.Learning.Features.Techniques.Services.Implementation;

namespace Sellevate.Learning.Features.Techniques;

public static class TechniqueServiceCollectionExtensions
{
    public static IServiceCollection AddTechniqueFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<ITechniqueService, TechniqueService>();
        return services;
    }
}
