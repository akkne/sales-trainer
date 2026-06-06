using System.Text.Json;
using Microsoft.Extensions.Options;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Configuration;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

internal sealed class RewriteEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenAiConfiguration> openAiOptions,
    AppDbContext databaseContext)
    : AiEvaluationStrategyBase(httpClientFactory, openAiOptions, databaseContext), IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.Rewrite;

    public async Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var instruction = exerciseContent.TryGetProperty("instruction", out var instEl)
            ? instEl.GetString() ?? ""
            : "";
        var originalText = exerciseContent.GetProperty("original").GetString() ?? "";
        var rewrittenText = userAnswer.GetProperty("rewrittenText").GetString() ?? "";

        var criteria = new List<string>();
        if (exerciseContent.TryGetProperty("evaluation_criteria", out var criteriaEl))
        {
            foreach (var c in criteriaEl.EnumerateArray())
            {
                var criterion = c.GetString();
                if (!string.IsNullOrEmpty(criterion))
                    criteria.Add(criterion);
            }
        }

        var aiPrompt = exerciseContent.TryGetProperty("ai_prompt", out var promptEl)
            ? promptEl.GetString() ?? ""
            : "";

        var userPrompt = $"Задание: {instruction}\n\nОригинал: {originalText}";
        if (criteria.Count > 0)
            userPrompt += $"\n\nКритерии оценки:\n- {string.Join("\n- ", criteria)}";
        userPrompt += $"\n\nПереписанный вариант: {rewrittenText}";

        return await EvaluateWithAiAsync(
            SupportedExerciseType,
            userPrompt,
            aiPrompt,
            cancellationToken);
    }
}
