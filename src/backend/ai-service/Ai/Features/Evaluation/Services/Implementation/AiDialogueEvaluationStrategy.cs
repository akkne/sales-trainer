using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Evaluation.Models;
using Sellevate.Ai.Features.Evaluation.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Evaluation.Services.Implementation;

internal sealed class AiDialogueEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenAiConfiguration> openAiOptions,
    ILogger<AiDialogueEvaluationStrategy> logger)
    : AiEvaluationStrategyBase(httpClientFactory, openAiOptions, logger), IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.AiDialogue;

    public async Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        string? globalSystemPrompt,
        CancellationToken cancellationToken = default)
    {
        var messages = userAnswer.GetProperty("messages").EnumerateArray().ToList();

        var maxTurns = exerciseContent.TryGetProperty("max_turns", out var maxEl)
            ? maxEl.GetInt32()
            : 6;
        var minTurns = maxTurns / 2;

        var userMessageCount = messages.Count(m => m.GetProperty("role").GetString() == "user");
        if (userMessageCount < minTurns)
        {
            return new ExerciseEvaluationResult(
                IsCorrect: false,
                Score: 30,
                Explanation: null,
                AiFeedback: $"Диалог слишком короткий. Нужно минимум {minTurns} ваших реплик.");
        }

        var conversationBuilder = new StringBuilder();
        var persona = exerciseContent.TryGetProperty("persona", out var personaEl)
            ? personaEl.GetString() ?? "Клиент"
            : "Клиент";

        foreach (var msg in messages)
        {
            var role = msg.GetProperty("role").GetString();
            var content = msg.GetProperty("content").GetString();
            var speaker = role == "user" ? "Продавец" : persona;
            conversationBuilder.AppendLine($"{speaker}: {content}");
        }

        var criteria = new List<string>();
        if (exerciseContent.TryGetProperty("success_criteria", out var criteriaEl))
        {
            foreach (var c in criteriaEl.EnumerateArray())
            {
                var criterion = c.GetString();
                if (!string.IsNullOrEmpty(criterion))
                    criteria.Add(criterion);
            }
        }

        var scenario = exerciseContent.TryGetProperty("scenario", out var scenarioEl)
            ? scenarioEl.GetString() ?? ""
            : "";

        var aiPrompt = exerciseContent.TryGetProperty("ai_prompt", out var promptEl)
            ? promptEl.GetString() ?? ""
            : "";

        var userPrompt = $"Сценарий: {scenario}\n\nПолный диалог:\n\n{conversationBuilder}";
        if (criteria.Count > 0)
            userPrompt += $"\n\nКритерии успеха:\n- {string.Join("\n- ", criteria)}";

        return await EvaluateWithAiAsync(
            userPrompt,
            aiPrompt,
            globalSystemPrompt,
            cancellationToken);
    }
}
