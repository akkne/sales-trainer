using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Content.Services.Abstract;
using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

/// <summary>
/// Resolves the grading strategy for an exercise type.
///
/// <para>
/// <b>Two populations, and the order matters.</b> The deterministic strategies are DI-registered and
/// arrive as a collection; the AI-graded ones are constructed here, one per entry in
/// <c>ExerciseTypes.AiPowered</c>, because they share a single implementation parameterized by type.
/// The AI entries are written second and would therefore win a collision — there is none today, and a
/// deterministic strategy claiming an AI-graded type would be silently ignored rather than reported.
/// </para>
///
/// <para>
/// <b>An unknown type throws rather than defaulting.</b> Guessing a strategy would grade a learner
/// against rules nobody wrote for that exercise; the loud failure is the safe one, and it is reachable
/// only through content that <c>ExerciseContentValidator</c> should already have rejected.
/// </para>
/// </summary>
internal sealed class ExerciseEvaluationFactory
{
    private readonly Dictionary<string, IExerciseEvaluationStrategy> _strategyByExerciseType;

    public ExerciseEvaluationFactory(
        IEnumerable<IExerciseEvaluationStrategy> deterministicStrategies,
        IAiEvaluationClient aiEvaluationClient,
        LearningDbContext databaseContext,
        IOrganizationProfileProvider organizationProfileProvider)
    {
        _strategyByExerciseType = deterministicStrategies
            .ToDictionary(strategy => strategy.SupportedExerciseType);

        foreach (var aiExerciseType in ExerciseTypes.AiPowered)
        {
            _strategyByExerciseType[aiExerciseType] =
                new AiExerciseEvaluationStrategy(
                    aiExerciseType, aiEvaluationClient, databaseContext, organizationProfileProvider);
        }
    }

    /// <summary>
    /// The strategy for <paramref name="exerciseType"/>. Throws
    /// <see cref="NotSupportedException"/> when none is registered.
    /// </summary>
    public IExerciseEvaluationStrategy GetStrategyForExerciseType(string exerciseType)
    {
        if (_strategyByExerciseType.TryGetValue(exerciseType, out var matchingStrategy))
            return matchingStrategy;

        throw new NotSupportedException($"No evaluation strategy registered for exercise type '{exerciseType}'.");
    }
}
