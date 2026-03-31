namespace SalesTrainer.Api.Features.Exercises;

public class ExerciseEvaluationFactory(IEnumerable<IExerciseEvaluationStrategy> allEvaluationStrategies)
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
