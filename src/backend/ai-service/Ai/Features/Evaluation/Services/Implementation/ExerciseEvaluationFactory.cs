using Sellevate.Ai.Features.Evaluation.Services.Abstract;

namespace Sellevate.Ai.Features.Evaluation.Services.Implementation;

/// <summary>
/// Indexes every registered strategy by the exercise type it claims, once per scope.
///
/// <para>
/// Two strategies claiming the same type throw here, at construction, rather than silently letting one win:
/// a duplicated exercise type means one grader is dead code and nobody would notice from the results.
/// </para>
/// </summary>
internal sealed class ExerciseEvaluationFactory(IEnumerable<IExerciseEvaluationStrategy> allEvaluationStrategies)
{
    private readonly Dictionary<string, IExerciseEvaluationStrategy> _strategyByExerciseType =
        allEvaluationStrategies.ToDictionary(strategy => strategy.SupportedExerciseType);

    public IExerciseEvaluationStrategy GetStrategyForExerciseType(string exerciseType)
    {
        if (_strategyByExerciseType.TryGetValue(exerciseType, out var matchingStrategy))
            return matchingStrategy;

        throw new NotSupportedException($"No evaluation strategy registered for exercise type '{exerciseType}'.");
    }
}
