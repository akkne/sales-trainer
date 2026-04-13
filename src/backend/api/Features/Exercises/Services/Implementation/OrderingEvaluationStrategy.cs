using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// Evaluates ordering exercises where user arranges items in correct sequence.
/// IsCorrect only when order matches exactly; no partial credit.
/// </summary>
internal sealed class OrderingEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "ordering";

    public Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var correctOrder = exerciseContent.GetProperty("correctOrder")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();

        var userOrder = userAnswer.GetProperty("order")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();

        var isCorrect = correctOrder.Count == userOrder.Count &&
                        correctOrder.SequenceEqual(userOrder);

        string? explanation = null;
        if (exerciseContent.TryGetProperty("explanation", out var explanationElement))
            explanation = explanationElement.GetString();

        var evaluationResult = new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: isCorrect ? 100 : 0,
            Explanation: explanation,
            AiFeedback: null);

        return Task.FromResult(evaluationResult);
    }
}
