using System.Text.Json;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

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
            .ToDictionary(property => int.Parse(property.Name), property => property.Value.GetString() ?? "");

        var totalItems = correctMapping.Count;
        var correctCount = 0;

        foreach (var (index, correctCategory) in correctMapping)
        {
            if (userMapping.TryGetValue(index, out var userCategory) &&
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

        return Task.FromResult(new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: score,
            Explanation: explanation,
            AiFeedback: null));
    }
}
