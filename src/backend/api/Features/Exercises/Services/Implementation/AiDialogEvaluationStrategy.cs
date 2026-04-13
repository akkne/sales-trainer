using System.Text;
using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// Evaluates AI dialog exercises where user had multi-turn conversation with AI customer.
/// The full conversation history is submitted and AI evaluates the overall quality.
/// </summary>
internal sealed class AiDialogEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    AppDbContext databaseContext)
    : AiEvaluationStrategyBase(httpClientFactory, configuration, databaseContext), IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "ai_dialog";

    public async Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var messages = userAnswer.GetProperty("messages").EnumerateArray().ToList();

        // Check minimum turns
        var minTurns = exerciseContent.TryGetProperty("minTurnsForCompletion", out var minEl)
            ? minEl.GetInt32()
            : 4;

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
            ? personaEl.GetProperty("name").GetString() ?? "Клиент"
            : "Клиент";

        foreach (var msg in messages)
        {
            var role = msg.GetProperty("role").GetString();
            var content = msg.GetProperty("content").GetString();
            var speaker = role == "user" ? "Продавец" : persona;
            conversationBuilder.AppendLine($"{speaker}: {content}");
        }

        var aiPrompt = exerciseContent.TryGetProperty("aiPrompt", out var promptEl)
            ? promptEl.GetString() ?? ""
            : "";

        var userPrompt = $"Полный диалог:\n\n{conversationBuilder}";

        return await EvaluateWithAiAsync(
            SupportedExerciseType,
            userPrompt,
            aiPrompt,
            cancellationToken);
    }
}
