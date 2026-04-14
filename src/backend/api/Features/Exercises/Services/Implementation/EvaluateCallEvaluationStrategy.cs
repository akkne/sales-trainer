using System.Text;
using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// Evaluates evaluate_call exercises where user analyzes a call transcript.
/// Content schema: { transcript: [{ speaker, text }], evaluation_axes: [{ name, description }], ai_prompt }
/// AI compares user's ratings with its own analysis.
/// </summary>
internal sealed class EvaluateCallEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    AppDbContext databaseContext)
    : AiEvaluationStrategyBase(httpClientFactory, configuration, databaseContext), IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.EvaluateCall;

    public async Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        // Format transcript
        var transcriptBuilder = new StringBuilder();
        foreach (var line in exerciseContent.GetProperty("transcript").EnumerateArray())
        {
            var speaker = line.GetProperty("speaker").GetString();
            var text = line.GetProperty("text").GetString();
            transcriptBuilder.AppendLine($"{speaker}: {text}");
        }

        // Format evaluation axes
        var axesBuilder = new StringBuilder();
        foreach (var axis in exerciseContent.GetProperty("evaluation_axes").EnumerateArray())
        {
            var name = axis.GetProperty("name").GetString();
            var description = axis.GetProperty("description").GetString();
            axesBuilder.AppendLine($"- {name}: {description}");
        }

        // Format user ratings
        var ratingsBuilder = new StringBuilder();
        foreach (var rating in userAnswer.GetProperty("ratings").EnumerateObject())
        {
            ratingsBuilder.AppendLine($"- {rating.Name}: {rating.Value.GetInt32()}");
        }

        var overallComment = userAnswer.TryGetProperty("overallComment", out var commentEl)
            ? commentEl.GetString() ?? ""
            : "";

        var aiPrompt = exerciseContent.TryGetProperty("ai_prompt", out var promptEl)
            ? promptEl.GetString() ?? ""
            : "";

        var userPrompt = $"""
            Транскрипт:
            {transcriptBuilder}

            Критерии оценки:
            {axesBuilder}

            Оценки пользователя:
            {ratingsBuilder}

            Комментарий пользователя: {overallComment}
            """;

        return await EvaluateWithAiAsync(
            SupportedExerciseType,
            userPrompt,
            aiPrompt,
            cancellationToken);
    }
}
