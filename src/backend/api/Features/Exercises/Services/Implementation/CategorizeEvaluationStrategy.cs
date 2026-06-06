using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

internal sealed class CategorizeEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.Categorize;

    public Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var correctMapping = new Dictionary<int, string>();
        var itemIndex = 0;
        foreach (var item in exerciseContent.GetProperty("items").EnumerateArray())
        {
            correctMapping[itemIndex] = item.GetProperty("category").GetString() ?? "";
            itemIndex++;
        }

        var userMapping = userAnswer.GetProperty("mapping")
            .EnumerateObject()
            .ToDictionary(p => int.Parse(p.Name), p => p.Value.GetString() ?? "");

        var totalItems = correctMapping.Count;
        var correctCount = 0;

        foreach (var (idx, correctCategory) in correctMapping)
        {
            if (userMapping.TryGetValue(idx, out var userCategory) &&
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
