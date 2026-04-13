using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Features.Exercises.Services.Implementation;

namespace SalesTrainer.Api.Features.Exercises;

public static class ExerciseServiceCollectionExtensions
{
    public static IServiceCollection AddExerciseFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IExerciseService, ExerciseService>();
        services.AddScoped<ExerciseEvaluationFactory>();
        services.AddScoped<IExerciseEvaluationStrategy, MultipleChoiceEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, FillBlankEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, OpenQuestionEvaluationStrategy>();
        return services;
    }
}
