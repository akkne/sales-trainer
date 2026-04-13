using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// Evaluates rewrite-better exercises where user improves a weak text.
/// AI evaluates the improvement against the original.
/// </summary>
internal sealed class RewriteBetterEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    AppDbContext databaseContext)
    : AiEvaluationStrategyBase(httpClientFactory, configuration, databaseContext), IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "rewrite_better";

    public async Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var originalText = exerciseContent.GetProperty("originalText").GetString() ?? "";
        var rewrittenText = userAnswer.GetProperty("rewrittenText").GetString() ?? "";

        // Validate length constraints
        if (exerciseContent.TryGetProperty("minLength", out var minEl))
        {
            var minLength = minEl.GetInt32();
            if (rewrittenText.Length < minLength)
            {
                return new ExerciseEvaluationResult(
                    IsCorrect: false,
                    Score: 0,
                    Explanation: null,
                    AiFeedback: $"Текст слишком короткий. Минимум {minLength} символов.");
            }
        }

        if (exerciseContent.TryGetProperty("maxLength", out var maxEl))
        {
            var maxLength = maxEl.GetInt32();
            if (rewrittenText.Length > maxLength)
            {
                return new ExerciseEvaluationResult(
                    IsCorrect: false,
                    Score: 0,
                    Explanation: null,
                    AiFeedback: $"Текст слишком длинный. Максимум {maxLength} символов.");
            }
        }

        var context = exerciseContent.TryGetProperty("context", out var ctxEl)
            ? ctxEl.GetString() ?? ""
            : "";

        var aiPrompt = exerciseContent.TryGetProperty("aiPrompt", out var promptEl)
            ? promptEl.GetString() ?? ""
            : "";

        var userPrompt = $"Оригинал: {originalText}";
        if (!string.IsNullOrEmpty(context))
            userPrompt += $"\n\nКонтекст: {context}";
        userPrompt += $"\n\nПереписанный вариант: {rewrittenText}";

        return await EvaluateWithAiAsync(
            SupportedExerciseType,
            userPrompt,
            aiPrompt,
            cancellationToken);
    }
}
