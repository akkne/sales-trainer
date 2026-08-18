using Sellevate.Ai.Features.Evaluation.Services.Abstract;
using Sellevate.Ai.Features.Evaluation.Services.Implementation;

namespace Sellevate.Ai.Features.Evaluation;

/// <summary>
/// Registers the grading feature: the five exercise strategies, the factory that picks one, and the
/// service-to-service authentication filter. The filter must be registered here even though nothing
/// injects it — <c>[ServiceFilter]</c> resolves it from the container, and an unregistered filter fails
/// at request time rather than at startup.
/// </summary>
public static class EvaluationServiceCollectionExtensions
{
    public static IServiceCollection AddEvaluationFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<InternalServiceAuthFilter>();

        services.AddScoped<IExerciseEvaluationStrategy, FreeTextEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, AiDialogueEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, SpotMistakeEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, RewriteEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, EvaluateCallEvaluationStrategy>();
        services.AddScoped<ExerciseEvaluationFactory>();
        services.AddScoped<IExerciseEvaluationService, ExerciseEvaluationService>();
        return services;
    }
}
