using System.Text.Json;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Exercises.Constants;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

/// <summary>
/// Grades <c>reorder</c>: the learner arranges the authored items into the right sequence.
///
/// <para>
/// <b>All or nothing, and the comparison is over item indices rather than the authored positions.</b>
/// The expected answer is the item indices sorted by each item's <c>correct_position</c>, which means
/// the author's numbering may be sparse or one-based without changing the grading. A partial score was
/// deliberately not introduced: getting a call's stages nearly in order is not a partial success at the
/// thing being taught.
/// </para>
/// </summary>
internal sealed class ReorderEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.Reorder;

    public Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        if (!exerciseContent.TryGetProperty(ExerciseContentFields.Items, out var itemsElement))
            throw new ExerciseAnswerValidationException(
                $"Exercise content is missing '{ExerciseContentFields.Items}'.");

        if (!userAnswer.TryGetProperty(ExerciseAnswerFields.Order, out var orderElement))
            throw new ExerciseAnswerValidationException(
                $"Answer must contain array field '{ExerciseAnswerFields.Order}'.");

        var items = itemsElement.EnumerateArray()
            .Select((item, index) =>
            {
                if (!item.TryGetProperty(ExerciseContentFields.CorrectPosition, out var positionElement)
                    || !positionElement.TryGetInt32(out var correctPosition))
                    throw new ExerciseAnswerValidationException(
                        "Each item in exercise content must have integer field "
                        + $"'{ExerciseContentFields.CorrectPosition}'.");
                return new { Index = index, CorrectPosition = correctPosition };
            })
            .OrderBy(entry => entry.CorrectPosition)
            .Select(entry => entry.Index)
            .ToList();

        var submittedOrder = orderElement.EnumerateArray()
            .Select((element, position) =>
            {
                if (!element.TryGetInt32(out var itemIndex))
                    throw new ExerciseAnswerValidationException(
                        $"All elements in '{ExerciseAnswerFields.Order}' must be integers "
                        + $"(element {position} is not).");
                return itemIndex;
            })
            .ToList();

        var isCorrect = items.Count == submittedOrder.Count &&
                        items.SequenceEqual(submittedOrder);

        string? explanation = null;
        if (exerciseContent.TryGetProperty(ExerciseContentFields.Explanation, out var explanationElement))
            explanation = explanationElement.GetString();

        return Task.FromResult(new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: isCorrect ? ExerciseScores.Maximum : ExerciseScores.Minimum,
            Explanation: explanation,
            AiFeedback: null));
    }
}
