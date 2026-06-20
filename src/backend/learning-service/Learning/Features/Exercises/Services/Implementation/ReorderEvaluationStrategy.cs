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
        var items = exerciseContent.GetProperty("items").EnumerateArray()
            .Select((item, index) => new
            {
                Index = index,
                CorrectPosition = item.GetProperty("correct_position").GetInt32()
            })
            .OrderBy(entry => entry.CorrectPosition)
            .Select(entry => entry.Index)
            .ToList();

        var userOrder = userAnswer.GetProperty("order")
            .EnumerateArray()
            .Select(element => element.GetInt32())
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
