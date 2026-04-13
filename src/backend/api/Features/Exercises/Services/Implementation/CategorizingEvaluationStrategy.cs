using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// Evaluates categorizing exercises where user sorts items into buckets.
/// Supports partial credit: score = (correctItems / totalItems) * 100.
/// IsCorrect only when all items in correct categories.
/// </summary>
internal sealed class CategorizingEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "categorizing";

    public Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var correctMapping = exerciseContent.GetProperty("correctMapping")
            .EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");

        var userMapping = userAnswer.GetProperty("mapping")
            .EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");

        var totalItems = correctMapping.Count;
        var correctCount = 0;

        foreach (var (itemId, correctCategory) in correctMapping)
        {
            if (userMapping.TryGetValue(itemId, out var userCategory) &&
                userCategory == correctCategory)
            {
                correctCount++;
            }
        }

        var isCorrect = correctCount == totalItems && userMapping.Count == totalItems;
        var score = totalItems > 0 ? (int)Math.Round((double)correctCount / totalItems * 100) : 0;

        string? explanation = null;
        if (exerciseContent.TryGetProperty("explanation", out var explanationElement))
            explanation = explanationElement.GetString();

        var evaluationResult = new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: score,
            Explanation: explanation,
            AiFeedback: null);

        return Task.FromResult(evaluationResult);
    }
}
