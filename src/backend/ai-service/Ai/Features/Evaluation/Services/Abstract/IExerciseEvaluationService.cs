using Sellevate.Ai.Features.Evaluation.Models;

namespace Sellevate.Ai.Features.Evaluation.Services.Abstract;

public interface IExerciseEvaluationService
{
    Task<ExerciseEvaluationResult> EvaluateAsync(
        EvaluateExerciseRequestDto request,
        CancellationToken cancellationToken = default);
}
