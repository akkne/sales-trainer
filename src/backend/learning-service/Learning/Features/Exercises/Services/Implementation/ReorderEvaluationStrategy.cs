using System.Text.Json;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

internal sealed class ReorderEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.Reorder;

    public Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        if (!exerciseContent.TryGetProperty("items", out var itemsEl))
            throw new ExerciseAnswerValidationException("Exercise content is missing 'items'.");

        if (!userAnswer.TryGetProperty("order", out var orderEl))
            throw new ExerciseAnswerValidationException("Answer must contain array field 'order'.");

        var items = itemsEl.EnumerateArray()
            .Select((item, index) =>
            {
                if (!item.TryGetProperty("correct_position", out var posEl) || !posEl.TryGetInt32(out var pos))
                    throw new ExerciseAnswerValidationException(
                        "Each item in exercise content must have integer field 'correct_position'.");
                return new { Index = index, CorrectPosition = pos };
            })
            .OrderBy(entry => entry.CorrectPosition)
            .Select(entry => entry.Index)
            .ToList();

        var userOrder = orderEl.EnumerateArray()
            .Select((element, i) =>
            {
                if (!element.TryGetInt32(out var val))
                    throw new ExerciseAnswerValidationException(
                        $"All elements in 'order' must be integers (element {i} is not).");
                return val;
            })
            .ToList();

        var isCorrect = items.Count == userOrder.Count &&
                        items.SequenceEqual(userOrder);

        string? explanation = null;
        if (exerciseContent.TryGetProperty("explanation", out var explanationElement))
            explanation = explanationElement.GetString();

        return Task.FromResult(new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: isCorrect ? 100 : 0,
            Explanation: explanation,
            AiFeedback: null));
    }
}
