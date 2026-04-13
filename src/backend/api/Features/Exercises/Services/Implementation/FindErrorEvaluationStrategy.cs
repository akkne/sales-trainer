using System.Text;
using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// Evaluates find-error exercises where user identifies mistakes in dialog.
/// Scoring: 50 points for correct line + 25 for explanation (AI) + 25 for fix selection.
/// </summary>
internal sealed class FindErrorEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    AppDbContext databaseContext)
    : AiEvaluationStrategyBase(httpClientFactory, configuration, databaseContext), IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "find_error";

    public async Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var errorLineId = exerciseContent.GetProperty("errorLineId").GetString();
        var selectedLineId = userAnswer.GetProperty("selectedLineId").GetString();

        var lineCorrect = selectedLineId == errorLineId;
        var score = lineCorrect ? 50 : 0;
        var feedback = new StringBuilder();

        if (!lineCorrect)
        {
            feedback.AppendLine("Неверная строка выбрана.");
        }

        // Check if explanation is required and provided
        var requireExplanation = exerciseContent.TryGetProperty("requireExplanation", out var reqExp) &&
                                 reqExp.GetBoolean();

        if (requireExplanation && lineCorrect)
        {
            var userExplanation = userAnswer.TryGetProperty("explanation", out var expEl)
                ? expEl.GetString() ?? ""
                : "";

            if (!string.IsNullOrWhiteSpace(userExplanation))
            {
                // Get the error line text for context
                var errorLineText = "";
                if (exerciseContent.TryGetProperty("dialogLines", out var lines))
                {
                    foreach (var line in lines.EnumerateArray())
                    {
                        if (line.GetProperty("id").GetString() == errorLineId)
                        {
                            errorLineText = line.GetProperty("text").GetString() ?? "";
                            break;
                        }
                    }
                }

                var aiPrompt = exerciseContent.TryGetProperty("aiPrompt", out var promptEl)
                    ? promptEl.GetString() ?? ""
                    : "";

                var userPrompt = $"Контекст ошибки: {errorLineText}\n\nОбъяснение пользователя: {userExplanation}";

                var aiResult = await EvaluateWithAiAsync(
                    SupportedExerciseType,
                    userPrompt,
                    aiPrompt,
                    cancellationToken);

                // AI contributes 25 points max
                var aiScore = (int)Math.Round(aiResult.Score / 100.0 * 25);
                score += aiScore;

                if (!string.IsNullOrEmpty(aiResult.AiFeedback))
                    feedback.AppendLine(aiResult.AiFeedback);
            }
        }
        else if (requireExplanation && lineCorrect)
        {
            feedback.AppendLine("Объяснение не предоставлено.");
        }

        // Check fix selection if applicable
        if (exerciseContent.TryGetProperty("suggestedFixes", out _) &&
            exerciseContent.TryGetProperty("correctFixIds", out var correctFixIdsEl))
        {
            var correctFixIds = correctFixIdsEl.EnumerateArray()
                .Select(e => e.GetString())
                .ToHashSet();

            var selectedFixId = userAnswer.TryGetProperty("selectedFixId", out var fixEl)
                ? fixEl.GetString()
                : null;

            if (!string.IsNullOrEmpty(selectedFixId) && correctFixIds.Contains(selectedFixId))
            {
                score += 25;
            }
            else if (!string.IsNullOrEmpty(selectedFixId))
            {
                feedback.AppendLine("Выбранное исправление не оптимально.");
            }
        }

        var isCorrect = score >= 75; // Line correct + either good explanation or good fix

        return new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: score,
            Explanation: null,
            AiFeedback: feedback.Length > 0 ? feedback.ToString().Trim() : null);
    }
}
