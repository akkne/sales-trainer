using SalesTrainer.Api.Features.Reference.Services.Abstract;
using SalesTrainer.Api.Features.Reference.Services.Implementation;

namespace SalesTrainer.Api.Features.Reference;

public static class ReferenceServiceCollectionExtensions
{
    public static IServiceCollection AddReferenceFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IReferenceService, ReferenceService>();
        return services;
    }
}
