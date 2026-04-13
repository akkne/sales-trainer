using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Features.Exercises.Services.Implementation;

namespace SalesTrainer.Api.Features.Exercises;

public static class ExerciseServiceCollectionExtensions
{
    public static IServiceCollection AddExerciseFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IExerciseService, ExerciseService>();
        services.AddScoped<ExerciseEvaluationFactory>();

        // Original exercise types
        services.AddScoped<IExerciseEvaluationStrategy, MultipleChoiceEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, FillBlankEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, OpenQuestionEvaluationStrategy>();

        // New non-AI exercise types
        services.AddScoped<IExerciseEvaluationStrategy, OrderingEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, MatchingEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, CategorizingEvaluationStrategy>();

        // New AI-powered exercise types
        services.AddScoped<IExerciseEvaluationStrategy, FindErrorEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, RewriteBetterEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, AiDialogEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, RateCallEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, WrittenAnswerEvaluationStrategy>();

        return services;
    }
}
