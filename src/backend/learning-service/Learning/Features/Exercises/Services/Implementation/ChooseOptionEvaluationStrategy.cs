using System.Text.Json;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

internal sealed class ChooseOptionEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.ChooseOption;

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

        return Task.FromResult(new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: isCorrect ? 100 : 0,
            Explanation: explanation,
            AiFeedback: null));
    }
}
