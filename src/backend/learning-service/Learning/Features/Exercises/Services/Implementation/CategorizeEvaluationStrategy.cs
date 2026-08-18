using System.Text.Json;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Exercises.Constants;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

/// <summary>
/// Grades <c>categorize</c>: the learner files each authored item under one of the declared
/// categories.
///
/// <para>
/// <b>The answer is keyed by the item's position in the authored array, not by its text.</b> The
/// mapping arrives as an object whose keys are index strings, so a client that reorders the items it
/// displays must still submit the original indices; a non-integer key is refused rather than skipped,
/// because guessing which item was meant is how an answer gets graded against the wrong item.
/// </para>
///
/// <para>
/// <b>Scored proportionally, like match-pairs.</b> Full marks additionally require the submitted
/// mapping to cover exactly as many items as were authored, so leaving items unfiled cannot pass even
/// when everything filed is right.
/// </para>
/// </summary>
internal sealed class CategorizeEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.Categorize;

    public Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        if (!exerciseContent.TryGetProperty(ExerciseContentFields.Items, out var itemsElement))
            throw new ExerciseAnswerValidationException(
                $"Exercise content is missing '{ExerciseContentFields.Items}'.");

        if (!userAnswer.TryGetProperty(ExerciseAnswerFields.Mapping, out var mappingElement))
            throw new ExerciseAnswerValidationException(
                $"Answer must contain object field '{ExerciseAnswerFields.Mapping}'.");

        var correctMapping = new Dictionary<int, string>();
        var itemIndex = 0;
        foreach (var item in itemsElement.EnumerateArray())
        {
            correctMapping[itemIndex] = item.GetProperty(ExerciseContentFields.Category).GetString() ?? "";
            itemIndex++;
        }

        var submittedMapping = mappingElement
            .EnumerateObject()
            .ToDictionary(
                property =>
                {
                    if (!int.TryParse(property.Name, out var submittedItemIndex))
                        throw new ExerciseAnswerValidationException(
                            $"'{ExerciseAnswerFields.Mapping}' keys must be integer strings; "
                            + $"got '{property.Name}'.");
                    return submittedItemIndex;
                },
                property => property.Value.GetString() ?? "");

        var totalItems = correctMapping.Count;
        var correctCount = 0;

        foreach (var (index, correctCategory) in correctMapping)
        {
            if (submittedMapping.TryGetValue(index, out var submittedCategory) &&
                submittedCategory == correctCategory)
            {
                correctCount++;
            }
        }

        var isCorrect = correctCount == totalItems && submittedMapping.Count == totalItems;
        var score = totalItems > 0
            ? (int)Math.Round((double)correctCount / totalItems * ExerciseScores.Maximum)
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
