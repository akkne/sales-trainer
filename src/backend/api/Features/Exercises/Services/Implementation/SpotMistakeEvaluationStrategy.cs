using System.Text;
using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// Evaluates spot_mistake exercises where user identifies mistakes in dialog.
/// Content schema: { dialogue: [{ speaker, text, is_mistake }], explanation, ai_prompt }
/// Scoring: 50 points for correct line + 50 for AI-evaluated explanation.
/// </summary>
internal sealed class SpotMistakeEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    AppDbContext databaseContext)
    : AiEvaluationStrategyBase(httpClientFactory, configuration, databaseContext), IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.SpotMistake;

    public async Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        // Find the mistake line index
        var mistakeIndex = -1;
        var mistakeText = "";
        var lineIndex = 0;
        foreach (var line in exerciseContent.GetProperty("dialogue").EnumerateArray())
        {
            if (line.GetProperty("is_mistake").GetBoolean())
            {
                mistakeIndex = lineIndex;
                mistakeText = line.GetProperty("text").GetString() ?? "";
                break;
            }
            lineIndex++;
        }

        var selectedLineIndex = userAnswer.GetProperty("selectedLineIndex").GetInt32();
        var lineCorrect = selectedLineIndex == mistakeIndex;
        var score = lineCorrect ? 50 : 0;
        var feedback = new StringBuilder();

        if (!lineCorrect)
        {
            feedback.AppendLine("Неверная строка выбрана.");
        }

        // Check if user provided explanation
        var userExplanation = userAnswer.TryGetProperty("explanation", out var expEl)
            ? expEl.GetString() ?? ""
            : "";

        if (lineCorrect && !string.IsNullOrWhiteSpace(userExplanation))
        {
            var aiPrompt = exerciseContent.TryGetProperty("ai_prompt", out var promptEl)
                ? promptEl.GetString() ?? ""
                : "";

            var userPrompt = $"Контекст ошибки: {mistakeText}\n\nОбъяснение пользователя: {userExplanation}";

            var aiResult = await EvaluateWithAiAsync(
                SupportedExerciseType,
                userPrompt,
                aiPrompt,
                cancellationToken);

            // AI contributes 50 points max
            var aiScore = (int)Math.Round(aiResult.Score / 100.0 * 50);
            score += aiScore;

            if (!string.IsNullOrEmpty(aiResult.AiFeedback))
                feedback.AppendLine(aiResult.AiFeedback);
        }

        // Get static explanation
        string? explanation = null;
        if (exerciseContent.TryGetProperty("explanation", out var explanationElement))
            explanation = explanationElement.GetString();

        var isCorrect = score >= 75;

        return new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: score,
            Explanation: explanation,
            AiFeedback: feedback.Length > 0 ? feedback.ToString().Trim() : null);
    }
}
