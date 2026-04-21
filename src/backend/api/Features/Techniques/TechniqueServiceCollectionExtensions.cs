using SalesTrainer.Api.Features.Techniques.Services.Abstract;
using SalesTrainer.Api.Features.Techniques.Services.Implementation;

namespace SalesTrainer.Api.Features.Techniques;

public static class TechniqueServiceCollectionExtensions
{
    public static IServiceCollection AddTechniqueFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<ITechniqueService, TechniqueService>();
        return services;
    }
}
