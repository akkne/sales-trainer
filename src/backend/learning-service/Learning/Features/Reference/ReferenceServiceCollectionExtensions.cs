using Sellevate.Learning.Features.Reference.Services.Abstract;
using Sellevate.Learning.Features.Reference.Services.Implementation;

namespace Sellevate.Learning.Features.Reference;

public static class ReferenceServiceCollectionExtensions
{
    public static IServiceCollection AddReferenceFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IReferenceService, ReferenceService>();
        return services;
    }
}
