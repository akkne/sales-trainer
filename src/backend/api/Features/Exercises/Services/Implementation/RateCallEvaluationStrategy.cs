using System.Text;
using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// Evaluates rate-call exercises where user analyzes a call transcript.
/// AI compares user's ratings with its own analysis.
/// </summary>
internal sealed class RateCallEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    AppDbContext databaseContext)
    : AiEvaluationStrategyBase(httpClientFactory, configuration, databaseContext), IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "rate_call";

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

        // Format criteria
        var criteriaBuilder = new StringBuilder();
        foreach (var criterion in exerciseContent.GetProperty("criteria").EnumerateArray())
        {
            var id = criterion.GetProperty("id").GetString();
            var name = criterion.GetProperty("name").GetString();
            var description = criterion.GetProperty("description").GetString();
            criteriaBuilder.AppendLine($"- {id}: {name} — {description}");
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

        var aiPrompt = exerciseContent.TryGetProperty("aiPrompt", out var promptEl)
            ? promptEl.GetString() ?? ""
            : "";

        var userPrompt = $"""
            Транскрипт:
            {transcriptBuilder}

            Критерии оценки:
            {criteriaBuilder}

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
