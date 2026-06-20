using System.Text.Json;
using Sellevate.Learning.Features.Exercises.Models;

namespace Sellevate.Learning.Features.Exercises.Services.Abstract;

public interface IExerciseEvaluationStrategy
{
    string SupportedExerciseType { get; }

    Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default);
}
