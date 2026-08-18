using Sellevate.Ai.Features.Evaluation.Models;

namespace Sellevate.Ai.Features.Evaluation.Services.Abstract;

/// <summary>
/// The single entry point for grading one submitted answer. Picks the strategy for the exercise type and
/// delegates; throws <see cref="NotSupportedException"/> for a type no strategy claims.
/// </summary>
public interface IExerciseEvaluationService
{
    Task<ExerciseEvaluationResult> EvaluateAsync(
        EvaluateExerciseRequestDto request,
        CancellationToken cancellationToken = default);
}
