using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Configuration;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

internal sealed class EvaluateCallEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenAiConfiguration> openAiOptions,
    AppDbContext databaseContext)
    : AiEvaluationStrategyBase(httpClientFactory, openAiOptions, databaseContext), IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.EvaluateCall;

    public async Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var transcriptBuilder = new StringBuilder();
        foreach (var line in exerciseContent.GetProperty("transcript").EnumerateArray())
        {
            var speaker = line.GetProperty("speaker").GetString();
            var text = line.GetProperty("text").GetString();
            transcriptBuilder.AppendLine($"{speaker}: {text}");
        }

        var axesBuilder = new StringBuilder();
        foreach (var axis in exerciseContent.GetProperty("evaluation_axes").EnumerateArray())
        {
            var name = axis.GetProperty("name").GetString();
            var description = axis.GetProperty("description").GetString();
            axesBuilder.AppendLine($"- {name}: {description}");
        }

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
