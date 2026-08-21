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

        // Q-15 (docs/NIGHT_AUDIT_QUESTIONS.md, docs/DECISIONS.md): the mistake line alone *is* the
        // exercise the "необязательно" label promises, so it earns the session's own pass mark (70,
        // the same >=70 the session gate uses) on its own — a learner who did exactly what was asked
        // always passes. A written explanation is graded by the AI and can raise the score up to 100,
        // but only as a bonus on top of the 70 the line already earned: it never pulls the score back
        // below the pass mark, even when the AI grades it poorly or fails to parse.
        var score = lineCorrect ? 70 : 0;
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

            // aiResult.Score is the AI grader's rating*10, in [10, 100] (or 0 if the AI response
            // failed to parse). Map it onto the [0, 30] bonus range and add it to the 70 the correct
            // line already earned — clamped so a weak or failed explanation never drops the score
            // below the pass mark and a strong one never pushes it past 100.
            var bonus = (aiResult.Score - 10) * 30 / 90;
            score = Math.Clamp(70 + bonus, 70, 100);
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
