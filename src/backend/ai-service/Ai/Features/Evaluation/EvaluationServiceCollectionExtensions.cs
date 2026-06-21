using Sellevate.Ai.Features.Evaluation.Services.Abstract;
using Sellevate.Ai.Features.Evaluation.Services.Implementation;

namespace Sellevate.Ai.Features.Evaluation;

public static class EvaluationServiceCollectionExtensions
{
    public static IServiceCollection AddEvaluationFeatureServices(this IServiceCollection services)
    {
        // AI7b: register the service-to-service auth filter so it can be resolved by ServiceFilter.
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
