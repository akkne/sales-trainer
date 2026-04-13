using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

internal sealed class FillBlankEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "fill_blank";

    public Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var correctOptionIndex = exerciseContent.GetProperty("correctOptionIndex").GetInt32();
        var selectedOptionIndex = userAnswer.GetProperty("selectedOptionIndex").GetInt32();
        var isCorrect = selectedOptionIndex == correctOptionIndex;

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
