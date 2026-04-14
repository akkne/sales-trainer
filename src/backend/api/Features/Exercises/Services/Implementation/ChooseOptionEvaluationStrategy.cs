using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// Evaluates choose_option exercises where user selects the best answer from options.
/// Content schema: { situation, options: [{ text, is_correct }], explanation }
/// </summary>
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

        var evaluationResult = new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: isCorrect ? 100 : 0,
            Explanation: explanation,
            AiFeedback: null);

        return Task.FromResult(evaluationResult);
    }
}
