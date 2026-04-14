using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// Evaluates reorder exercises where user arranges items in correct sequence.
/// Content schema: { instruction, items: [{ text, correct_position }] }
/// IsCorrect only when order matches exactly; no partial credit.
/// </summary>
internal sealed class ReorderEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.Reorder;

    public Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        // Build expected order from items with correct_position
        var items = exerciseContent.GetProperty("items").EnumerateArray()
            .Select((item, index) => new
            {
                Index = index,
                CorrectPosition = item.GetProperty("correct_position").GetInt32()
            })
            .OrderBy(x => x.CorrectPosition)
            .Select(x => x.Index)
            .ToList();

        // User provides order as array of indices
        var userOrder = userAnswer.GetProperty("order")
            .EnumerateArray()
            .Select(e => e.GetInt32())
            .ToList();

        var isCorrect = items.Count == userOrder.Count &&
                        items.SequenceEqual(userOrder);

        string? explanation = null;
        if (exerciseContent.TryGetProperty("explanation", out var explanationElement))
            explanation = explanationElement.GetString();

        var evaluationResult = new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: isCorrect ? 100 : 0,
            Explanation: explanation,
            AiFeedback: null);

        return Task.FromResult(evaluationResult);
    }
}
