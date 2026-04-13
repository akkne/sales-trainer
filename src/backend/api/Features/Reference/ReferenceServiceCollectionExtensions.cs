namespace SalesTrainer.Api.Features.Reference;

public static class ReferenceServiceCollectionExtensions
{
    public static IServiceCollection AddReferenceFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IReferenceService, ReferenceService>();
        return services;
    }
}
