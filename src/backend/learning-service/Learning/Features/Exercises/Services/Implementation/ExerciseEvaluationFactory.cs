using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

internal sealed class ExerciseEvaluationFactory
{
    private readonly Dictionary<string, IExerciseEvaluationStrategy> _strategyByExerciseType;

    public ExerciseEvaluationFactory(
        IEnumerable<IExerciseEvaluationStrategy> deterministicStrategies,
        IAiEvaluationClient aiEvaluationClient,
        LearningDbContext databaseContext)
    {
        _strategyByExerciseType = deterministicStrategies
            .ToDictionary(strategy => strategy.SupportedExerciseType);

        foreach (var aiExerciseType in ExerciseTypes.AiPowered)
        {
            _strategyByExerciseType[aiExerciseType] =
                new AiExerciseEvaluationStrategy(aiExerciseType, aiEvaluationClient, databaseContext);
        }
    }

    public IExerciseEvaluationStrategy GetStrategyForExerciseType(string exerciseType)
    {
        if (_strategyByExerciseType.TryGetValue(exerciseType, out var matchingStrategy))
            return matchingStrategy;

        throw new NotSupportedException($"No evaluation strategy registered for exercise type '{exerciseType}'.");
    }
}
