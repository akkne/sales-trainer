using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// Evaluates fill_blank exercises where user fills a gap in dialogue.
/// Content schema: { before, after, options: [{ text, is_correct }] }
/// </summary>
internal sealed class FillBlankEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.FillBlank;

    public Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var selectedIndex = userAnswer.GetProperty("selectedOptionIndex").GetInt32();
        var options = exerciseContent.GetProperty("options").EnumerateArray().ToList();

        var isCorrect = false;
        if (selectedIndex >= 0 && selectedIndex < options.Count)
        {
            isCorrect = options[selectedIndex].GetProperty("is_correct").GetBoolean();
        }

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
