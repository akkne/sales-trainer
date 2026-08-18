using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Dialog.Constants;
using Sellevate.Ai.Features.Evaluation.Models;
using Sellevate.Ai.Features.Evaluation.Services.Abstract;
using Sellevate.Ai.Features.Quotas.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Evaluation.Services.Implementation;

/// <summary>
/// Grades a roleplay transcript. Refuses without calling the provider when the learner produced fewer
/// than half the exercise's turns — a two-line conversation cannot be graded, and paying a provider to
/// say so is waste.
/// </summary>
internal sealed class AiDialogueEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenAiConfiguration> openAiOptions,
    IAiSpendMeter spendMeter,
    ILogger<AiDialogueEvaluationStrategy> logger)
    : AiEvaluationStrategyBase(httpClientFactory, openAiOptions, spendMeter, logger), IExerciseEvaluationStrategy
{
    /// <summary>How the transcript names the learner's side to the grading model.</summary>
    private const string SellerSpeakerLabel = "Продавец";

    /// <summary>Fallback name for the roleplay character when the exercise did not give it one.</summary>
    private const string DefaultPersonaLabel = "Клиент";

    /// <summary>Turns assumed when the exercise carries no <c>max_turns</c>.</summary>
    private const int DefaultMaximumTurns = 6;

    /// <summary>Score reported for a transcript too short to grade.</summary>
    private const int TooShortScore = 30;

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
            : DefaultMaximumTurns;
        var minTurns = maxTurns / 2;

        var userMessageCount = messages.Count(message =>
            message.GetProperty("role").GetString() == DialogMessageRoles.User);
        if (userMessageCount < minTurns)
        {
            return new ExerciseEvaluationResult(
                IsCorrect: false,
                Score: TooShortScore,
                Explanation: null,
                AiFeedback: $"Диалог слишком короткий. Нужно минимум {minTurns} ваших реплик.");
        }

        var conversationBuilder = new StringBuilder();
        var persona = exerciseContent.TryGetProperty("persona", out var personaEl)
            ? personaEl.GetString() ?? DefaultPersonaLabel
            : DefaultPersonaLabel;

        foreach (var message in messages)
        {
            var role = message.GetProperty("role").GetString();
            var content = message.GetProperty("content").GetString();
            var speaker = role == DialogMessageRoles.User ? SellerSpeakerLabel : persona;
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
