using Sellevate.Ai.Features.Evaluation.Models;
using Sellevate.Ai.Features.Evaluation.Services.Abstract;

namespace Sellevate.Ai.Features.Evaluation.Services.Implementation;

/// <summary>
/// Validates the request and hands it to the strategy for its exercise type. Holds no grading logic of its
/// own by design — a rule that lived here would apply to every exercise type and belong to none.
/// </summary>
internal sealed class ExerciseEvaluationService : IExerciseEvaluationService
{
    private readonly ExerciseEvaluationFactory _evaluationFactory;

    public ExerciseEvaluationService(ExerciseEvaluationFactory evaluationFactory)
    {
        _evaluationFactory = evaluationFactory;
    }

    public Task<ExerciseEvaluationResult> EvaluateAsync(
        EvaluateExerciseRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExerciseType);

        var strategy = _evaluationFactory.GetStrategyForExerciseType(request.ExerciseType);
        return strategy.EvaluateAnswerAsync(
            request.ExerciseContent,
            request.UserAnswer,
            request.SystemPrompt,
            cancellationToken);
    }
}
