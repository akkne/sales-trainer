using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Evaluation.Models;
using Sellevate.Ai.Features.Evaluation.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Evaluation.Services.Implementation;

internal sealed class FreeTextEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenAiConfiguration> openAiOptions)
    : AiEvaluationStrategyBase(httpClientFactory, openAiOptions), IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.FreeText;

    public async Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        string? globalSystemPrompt,
        CancellationToken cancellationToken = default)
    {
        var situation = exerciseContent.TryGetProperty("situation", out var sitEl)
            ? sitEl.GetString() ?? ""
            : "";
        var instruction = exerciseContent.TryGetProperty("instruction", out var instEl)
            ? instEl.GetString() ?? ""
            : "";
        var userText = userAnswer.GetProperty("text").GetString() ?? "";

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

        var userPrompt = "";
        if (!string.IsNullOrEmpty(situation))
            userPrompt += $"Ситуация: {situation}\n\n";
        userPrompt += $"Задание: {instruction}";
        if (criteria.Count > 0)
            userPrompt += $"\n\nКритерии оценки:\n- {string.Join("\n- ", criteria)}";
        userPrompt += $"\n\nОтвет пользователя: {userText}";

        return await EvaluateWithAiAsync(
            userPrompt,
            aiPrompt,
            globalSystemPrompt,
            cancellationToken);
    }
}
