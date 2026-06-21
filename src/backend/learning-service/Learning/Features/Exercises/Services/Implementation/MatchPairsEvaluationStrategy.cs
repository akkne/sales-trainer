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
        if (!exerciseContent.TryGetProperty("pairs", out var contentPairsEl))
            throw new ExerciseAnswerValidationException("Exercise content is missing 'pairs'.");

        if (!userAnswer.TryGetProperty("pairs", out var userPairsEl))
            throw new ExerciseAnswerValidationException("Answer must contain array field 'pairs'.");

        var correctPairs = contentPairsEl
            .EnumerateArray()
            .Select(pair => (
                Left: pair.GetProperty("left").GetString() ?? "",
                Right: pair.GetProperty("right").GetString() ?? ""))
            .ToHashSet();

        var userPairs = userPairsEl
            .EnumerateArray()
            .Select((pair, i) =>
            {
                if (!pair.TryGetProperty("left", out var leftEl) || !pair.TryGetProperty("right", out var rightEl))
                    throw new ExerciseAnswerValidationException(
                        $"Each pair in answer must have 'left' and 'right' fields (pair {i} is invalid).");
                return (Left: leftEl.GetString() ?? "", Right: rightEl.GetString() ?? "");
            })
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
