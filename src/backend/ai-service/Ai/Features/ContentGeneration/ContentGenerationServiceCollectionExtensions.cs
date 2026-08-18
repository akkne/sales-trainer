using Sellevate.Ai.Features.ContentGeneration.Services.Abstract;
using Sellevate.Ai.Features.ContentGeneration.Services.Implementation;

namespace Sellevate.Ai.Features.ContentGeneration;

/// <summary>
/// Phase 40.27. The two stateless halves of the admin content pipeline (structuring, generation).
/// Neither touches a database, so nothing here needs a tenant.
/// </summary>
public static class ContentGenerationServiceCollectionExtensions
{
    public static IServiceCollection AddContentGenerationFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IMaterialStructuringService, MaterialStructuringService>();
        services.AddScoped<IExerciseGenerationService, ExerciseGenerationService>();

        return services;
    }
}
