using System.Text;
using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// Evaluates ai_dialogue exercises where user had multi-turn conversation with AI customer.
/// Content schema: { persona, scenario, context, max_turns, success_criteria: [], ai_prompt }
/// The full conversation history is submitted and AI evaluates the overall quality.
/// </summary>
internal sealed class AiDialogueEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    AppDbContext databaseContext)
    : AiEvaluationStrategyBase(httpClientFactory, configuration, databaseContext), IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.AiDialogue;

    public async Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var messages = userAnswer.GetProperty("messages").EnumerateArray().ToList();

        // Check minimum turns (default 3)
        var maxTurns = exerciseContent.TryGetProperty("max_turns", out var maxEl)
            ? maxEl.GetInt32()
            : 6;
        var minTurns = maxTurns / 2; // At least half the max turns

        var userMessageCount = messages.Count(m => m.GetProperty("role").GetString() == "user");
        if (userMessageCount < minTurns)
        {
            return new ExerciseEvaluationResult(
                IsCorrect: false,
                Score: 30,
                Explanation: null,
                AiFeedback: $"Диалог слишком короткий. Нужно минимум {minTurns} ваших реплик.");
        }

        // Format conversation for evaluation
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

        // Get success criteria
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
            SupportedExerciseType,
            userPrompt,
            aiPrompt,
            cancellationToken);
    }
}
