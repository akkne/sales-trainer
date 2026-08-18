using Sellevate.Ai.Features.ContentAdaptation.Services.Abstract;
using Sellevate.Ai.Features.ContentAdaptation.Services.Implementation;

namespace Sellevate.Ai.Features.ContentAdaptation;

/// <summary>
/// Phase 40.32. The two stateless per-exercise halves of batch adaptation (rewrite, review). Neither
/// touches a database, so nothing here needs a tenant.
/// </summary>
public static class ContentAdaptationServiceCollectionExtensions
{
    public static IServiceCollection AddContentAdaptationFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IExerciseRewriteService, ExerciseRewriteService>();
        services.AddScoped<IExerciseReviewService, ExerciseReviewService>();

        return services;
    }
}
