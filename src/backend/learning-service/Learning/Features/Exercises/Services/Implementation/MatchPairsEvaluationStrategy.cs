using System.Text.Json;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Exercises.Constants;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

/// <summary>
/// Grades <c>match_pairs</c>: the learner joins each left-hand item to its right-hand counterpart.
///
/// <para>
/// <b>Scored proportionally, and matched as an unordered set of pairs.</b> The submitted pairs are
/// compared against the authored ones by value, so the order the learner built them in is irrelevant —
/// only which things they put together. Half the pairs right is genuinely half the skill, which is why
/// this type scores a proportion where <c>reorder</c> does not.
/// </para>
///
/// <para>
/// <b>Both sides are de-duplicated by the set comparison.</b> A client that submits the same pair
/// twice therefore cannot inflate the match count past the number of authored pairs, and the
/// full-marks check additionally requires the submitted count to equal the authored count, so a partial
/// answer padded with repeats still falls short.
/// </para>
/// </summary>
internal sealed class MatchPairsEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.MatchPairs;

    public Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        if (!exerciseContent.TryGetProperty(ExerciseContentFields.Pairs, out var contentPairsElement))
            throw new ExerciseAnswerValidationException(
                $"Exercise content is missing '{ExerciseContentFields.Pairs}'.");

        if (!userAnswer.TryGetProperty(ExerciseAnswerFields.Pairs, out var submittedPairsElement))
            throw new ExerciseAnswerValidationException(
                $"Answer must contain array field '{ExerciseAnswerFields.Pairs}'.");

        var correctPairs = contentPairsElement
            .EnumerateArray()
            .Select(pair => (
                Left: pair.GetProperty(ExerciseContentFields.Left).GetString() ?? "",
                Right: pair.GetProperty(ExerciseContentFields.Right).GetString() ?? ""))
            .ToHashSet();

        var submittedPairs = submittedPairsElement
            .EnumerateArray()
            .Select((pair, position) =>
            {
                if (!pair.TryGetProperty(ExerciseAnswerFields.Left, out var leftElement)
                    || !pair.TryGetProperty(ExerciseAnswerFields.Right, out var rightElement))
                    throw new ExerciseAnswerValidationException(
                        $"Each pair in answer must have '{ExerciseAnswerFields.Left}' and "
                        + $"'{ExerciseAnswerFields.Right}' fields (pair {position} is invalid).");
                return (Left: leftElement.GetString() ?? "", Right: rightElement.GetString() ?? "");
            })
            .ToHashSet();

        var matchCount = submittedPairs.Count(pair => correctPairs.Contains(pair));
        var totalPairs = correctPairs.Count;
        var isCorrect = matchCount == totalPairs && submittedPairs.Count == totalPairs;
        var score = totalPairs > 0
            ? (int)Math.Round((double)matchCount / totalPairs * ExerciseScores.Maximum)
            : ExerciseScores.Minimum;

        string? explanation = null;
        if (exerciseContent.TryGetProperty(ExerciseContentFields.Explanation, out var explanationElement))
            explanation = explanationElement.GetString();

        return Task.FromResult(new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: score,
            Explanation: explanation,
            AiFeedback: null));
    }
}
