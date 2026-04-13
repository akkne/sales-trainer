using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;

namespace SalesTrainer.Api.Features.Exercises.Services.Abstract;

public interface IExerciseEvaluationStrategy
{
    string SupportedExerciseType { get; }
    Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default);
}
