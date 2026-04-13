using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// Evaluates written answer exercises where user writes free-form response to a prompt.
/// AI evaluates based on provided criteria.
/// </summary>
internal sealed class WrittenAnswerEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    AppDbContext databaseContext)
    : AiEvaluationStrategyBase(httpClientFactory, configuration, databaseContext), IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "written_answer";

    public async Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var prompt = exerciseContent.GetProperty("prompt").GetString() ?? "";
        var userText = userAnswer.GetProperty("text").GetString() ?? "";

        // Validate length constraints
        if (exerciseContent.TryGetProperty("minLength", out var minEl))
        {
            var minLength = minEl.GetInt32();
            if (userText.Length < minLength)
            {
                return new ExerciseEvaluationResult(
                    IsCorrect: false,
                    Score: 0,
                    Explanation: null,
                    AiFeedback: $"Ответ слишком короткий. Минимум {minLength} символов.");
            }
        }

        if (exerciseContent.TryGetProperty("maxLength", out var maxEl))
        {
            var maxLength = maxEl.GetInt32();
            if (userText.Length > maxLength)
            {
                return new ExerciseEvaluationResult(
                    IsCorrect: false,
                    Score: 0,
                    Explanation: null,
                    AiFeedback: $"Ответ слишком длинный. Максимум {maxLength} символов.");
            }
        }

        var context = exerciseContent.TryGetProperty("context", out var ctxEl)
            ? ctxEl.GetString() ?? ""
            : "";

        var aiPrompt = exerciseContent.TryGetProperty("aiPrompt", out var promptEl)
            ? promptEl.GetString() ?? ""
            : "";

        var userPrompt = $"Задание: {prompt}";
        if (!string.IsNullOrEmpty(context))
            userPrompt += $"\n\nКонтекст: {context}";
        userPrompt += $"\n\nОтвет пользователя: {userText}";

        return await EvaluateWithAiAsync(
            SupportedExerciseType,
            userPrompt,
            aiPrompt,
            cancellationToken);
    }
}
