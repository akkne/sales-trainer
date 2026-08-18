using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Features.Exercises.Services.Implementation;

namespace Sellevate.Learning.Features.Exercises;

/// <summary>
/// Registers the exercise feature's services and the one deterministic evaluation strategy per
/// exercise type.
///
/// <para>
/// Each strategy is registered against <c>IExerciseEvaluationStrategy</c> so that
/// <see cref="Services.Implementation.ExerciseEvaluationFactory"/> receives them as a collection: a new
/// deterministic type is added by one line here and needs no change to the factory. The AI-graded types
/// are deliberately absent — the factory builds those itself.
/// </para>
/// </summary>
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
