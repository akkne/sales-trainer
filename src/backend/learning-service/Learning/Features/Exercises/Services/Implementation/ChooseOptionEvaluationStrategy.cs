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
        if (!userAnswer.TryGetProperty("selectedOptionIndex", out var indexEl) || !indexEl.TryGetInt32(out var selectedIndex))
            throw new ExerciseAnswerValidationException(
                "Answer must contain integer field 'selectedOptionIndex'.");

        if (!exerciseContent.TryGetProperty("options", out var optionsEl))
            throw new ExerciseAnswerValidationException("Exercise content is missing 'options'.");

        var options = optionsEl.EnumerateArray().ToList();

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
