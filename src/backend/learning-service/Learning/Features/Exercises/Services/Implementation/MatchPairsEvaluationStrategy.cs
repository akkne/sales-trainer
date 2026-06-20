using System.Text.Json;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

internal sealed class MatchPairsEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.MatchPairs;

    public Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var correctPairs = exerciseContent.GetProperty("pairs")
            .EnumerateArray()
            .Select(pair => (
                Left: pair.GetProperty("left").GetString() ?? "",
                Right: pair.GetProperty("right").GetString() ?? ""))
            .ToHashSet();

        var userPairs = userAnswer.GetProperty("pairs")
            .EnumerateArray()
            .Select(pair => (
                Left: pair.GetProperty("left").GetString() ?? "",
                Right: pair.GetProperty("right").GetString() ?? ""))
            .ToHashSet();

        var matchCount = userPairs.Count(pair => correctPairs.Contains(pair));
        var totalPairs = correctPairs.Count;
        var isCorrect = matchCount == totalPairs && userPairs.Count == totalPairs;
        var score = totalPairs > 0 ? (int)Math.Round((double)matchCount / totalPairs * 100) : 0;

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
