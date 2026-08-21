using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Evaluation.Models;
using Sellevate.Ai.Features.Evaluation.Services.Abstract;
using Sellevate.Ai.Features.Quotas.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Evaluation.Services.Implementation;

/// <summary>
/// Grades a spot-the-mistake exercise: which planted errors the learner found, and what they said about them.
/// </summary>
internal sealed class SpotMistakeEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenAiConfiguration> openAiOptions,
    IAiSpendMeter spendMeter,
    ILogger<SpotMistakeEvaluationStrategy> logger)
    : AiEvaluationStrategyBase(httpClientFactory, openAiOptions, spendMeter, logger), IExerciseEvaluationStrategy
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

        // X-7: finding the mistake line *is* the exercise the "необязательно" label promises — an
        // explanation is authored as optional colour, not as half the grade. Scoring it as half the
        // credit meant a correct, unexplained line topped out at 50 and "Почти", so a learner who did
        // exactly what was asked could never pass. The line alone now earns full credit; a written
        // explanation is still graded for its own feedback, it just never lowers the score the line
        // already earned. docs/DECISIONS.md records this as the chosen resolution over the other
        // option (making the field mandatory).
        var score = lineCorrect ? 100 : 0;
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

            if (!string.IsNullOrEmpty(aiResult.AiFeedback))
                feedback.AppendLine(aiResult.AiFeedback);
        }

        string? explanation = null;
        if (exerciseContent.TryGetProperty("explanation", out var explanationElement))
            explanation = explanationElement.GetString();

        var isCorrect = lineCorrect;

        return new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: score,
            Explanation: explanation,
            AiFeedback: feedback.Length > 0 ? feedback.ToString().Trim() : null,
            // docs/AUDIT_PROD.md X-8: the learner content strips `is_mistake`, so the client cannot
            // paint the real mistake line — hand back its index now that grading has already needed it.
            CorrectLineIndex: mistakeIndex >= 0 ? mistakeIndex : null);
    }
}
