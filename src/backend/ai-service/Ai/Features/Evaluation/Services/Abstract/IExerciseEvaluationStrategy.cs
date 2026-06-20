using System.Text.Json;
using Sellevate.Ai.Features.Evaluation.Models;

namespace Sellevate.Ai.Features.Evaluation.Services.Abstract;

public interface IExerciseEvaluationStrategy
{
    string SupportedExerciseType { get; }

    Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        string? globalSystemPrompt,
        CancellationToken cancellationToken = default);
}
