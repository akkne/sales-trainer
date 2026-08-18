using System.Text.Json;
using Sellevate.Ai.Features.Evaluation.Models;

namespace Sellevate.Ai.Features.Evaluation.Services.Abstract;

/// <summary>
/// Grades one kind of exercise. <c>SupportedExerciseType</c> is the registration key and must be unique
/// across every implementation — two strategies claiming the same type is a startup failure, not a
/// tie-break.
/// </summary>
public interface IExerciseEvaluationStrategy
{
    string SupportedExerciseType { get; }

    Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        string? globalSystemPrompt,
        CancellationToken cancellationToken = default);
}
