using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// Evaluates match_pairs exercises where user connects items from two columns.
/// Content schema: { instruction, pairs: [{ left, right }] }
/// Supports partial credit: score = (correctPairs / totalPairs) * 100.
/// IsCorrect only when all pairs match.
/// </summary>
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
            .Select(p => (
                Left: p.GetProperty("left").GetString() ?? "",
                Right: p.GetProperty("right").GetString() ?? ""))
            .ToHashSet();

        var userPairs = userAnswer.GetProperty("pairs")
            .EnumerateArray()
            .Select(p => (
                Left: p.GetProperty("left").GetString() ?? "",
                Right: p.GetProperty("right").GetString() ?? ""))
            .ToHashSet();

        var matchCount = userPairs.Count(pair => correctPairs.Contains(pair));
        var totalPairs = correctPairs.Count;
        var isCorrect = matchCount == totalPairs && userPairs.Count == totalPairs;
        var score = totalPairs > 0 ? (int)Math.Round((double)matchCount / totalPairs * 100) : 0;

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
