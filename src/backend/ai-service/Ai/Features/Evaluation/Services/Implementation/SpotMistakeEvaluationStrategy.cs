using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Evaluation.Models;
using Sellevate.Ai.Features.Evaluation.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Evaluation.Services.Implementation;

internal sealed class SpotMistakeEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenAiConfiguration> openAiOptions)
    : AiEvaluationStrategyBase(httpClientFactory, openAiOptions), IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.SpotMistake;

    public async Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        string? globalSystemPrompt,
        CancellationToken cancellationToken = default)
    {
        var mistakeIndex = -1;
        var mistakeText = "";
        var lineIndex = 0;
        foreach (var line in exerciseContent.GetProperty("dialogue").EnumerateArray())
        {
            if (line.GetProperty("is_mistake").GetBoolean())
            {
                mistakeIndex = lineIndex;
                mistakeText = line.GetProperty("text").GetString() ?? "";
                break;
            }
            lineIndex++;
        }

        var selectedLineIndex = userAnswer.GetProperty("selectedLineIndex").GetInt32();
        var lineCorrect = selectedLineIndex == mistakeIndex;
        var score = lineCorrect ? 50 : 0;
        var feedback = new StringBuilder();

        if (!lineCorrect)
        {
            feedback.AppendLine("Неверная строка выбрана.");
        }

        var userExplanation = userAnswer.TryGetProperty("explanation", out var expEl)
            ? expEl.GetString() ?? ""
            : "";

        if (lineCorrect && !string.IsNullOrWhiteSpace(userExplanation))
        {
            var aiPrompt = exerciseContent.TryGetProperty("ai_prompt", out var promptEl)
                ? promptEl.GetString() ?? ""
                : "";

            var userPrompt = $"Контекст ошибки: {mistakeText}\n\nОбъяснение пользователя: {userExplanation}";

            var aiResult = await EvaluateWithAiAsync(
                userPrompt,
                aiPrompt,
                globalSystemPrompt,
                cancellationToken);

            var aiScore = (int)Math.Round(aiResult.Score / 100.0 * 50);
            score += aiScore;

            if (!string.IsNullOrEmpty(aiResult.AiFeedback))
                feedback.AppendLine(aiResult.AiFeedback);
        }

        string? explanation = null;
        if (exerciseContent.TryGetProperty("explanation", out var explanationElement))
            explanation = explanationElement.GetString();

        var isCorrect = score >= 75;

        return new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: score,
            Explanation: explanation,
            AiFeedback: feedback.Length > 0 ? feedback.ToString().Trim() : null);
    }
}
