using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Features.Exercises.Services.Implementation;

namespace Sellevate.Learning.Features.Exercises;

public static class ExerciseServiceCollectionExtensions
{
    public static IServiceCollection AddExerciseFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IExerciseService, ExerciseService>();
        services.AddScoped<ExerciseEvaluationFactory>();

        services.AddScoped<IExerciseEvaluationStrategy, ChooseOptionEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, FillBlankEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, ReorderEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, MatchPairsEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, CategorizeEvaluationStrategy>();
        services.AddScoped<IExerciseEvaluationStrategy, TheoryCardEvaluationStrategy>();

        return services;
    }
}
