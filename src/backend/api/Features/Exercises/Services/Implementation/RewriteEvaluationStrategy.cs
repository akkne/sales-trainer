using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// Evaluates rewrite exercises where user improves a weak text.
/// Content schema: { instruction, original, evaluation_criteria: [], ai_prompt }
/// AI evaluates the improvement against the original.
/// </summary>
internal sealed class RewriteEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    AppDbContext databaseContext)
    : AiEvaluationStrategyBase(httpClientFactory, configuration, databaseContext), IExerciseEvaluationStrategy
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

        // Get evaluation criteria
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
