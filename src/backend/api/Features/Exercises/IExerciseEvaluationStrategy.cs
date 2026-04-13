using System.Text.Json;

namespace SalesTrainer.Api.Features.Exercises;

public interface IExerciseEvaluationStrategy
{
    string SupportedExerciseType { get; }
    Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default);
}
