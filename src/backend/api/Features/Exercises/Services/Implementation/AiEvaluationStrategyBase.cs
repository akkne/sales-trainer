using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// Base class for AI-powered exercise evaluation strategies.
/// Handles loading global type prompts and calling OpenAI API.
/// </summary>
internal abstract class AiEvaluationStrategyBase(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    AppDbContext databaseContext)
{
    protected async Task<ExerciseEvaluationResult> EvaluateWithAiAsync(
        string exerciseType,
        string userPrompt,
        string? perExercisePrompt,
        CancellationToken cancellationToken)
    {
        var openAiApiKey = configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(openAiApiKey) || openAiApiKey.StartsWith("REPLACE_"))
        {
            return new ExerciseEvaluationResult(
                IsCorrect: true,
                Score: 80,
                Explanation: null,
                AiFeedback: "AI-оценка недоступна — ключ OpenAI не настроен.");
        }

        // Load global type prompt
        var globalPrompt = await databaseContext.ExerciseTypePrompts
            .Where(p => p.ExerciseType == exerciseType)
            .Select(p => p.SystemPrompt)
            .FirstOrDefaultAsync(cancellationToken) ?? "";

        // Build system prompt
        var systemPromptBuilder = new StringBuilder();
        if (!string.IsNullOrEmpty(globalPrompt))
        {
            systemPromptBuilder.AppendLine(globalPrompt);
        }
        if (!string.IsNullOrEmpty(perExercisePrompt))
        {
            systemPromptBuilder.AppendLine();
            systemPromptBuilder.AppendLine("Дополнительные критерии:");
            systemPromptBuilder.AppendLine(perExercisePrompt);
        }

        var httpClient = httpClientFactory.CreateClient("OpenAI");
        var openAiBaseUrl = configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com";
        var completionsPath = configuration["OpenAI:ChatCompletionsPath"] ?? "/v1/chat/completions";
        var apiUrl = openAiBaseUrl.TrimEnd('/') + completionsPath;
        var model = configuration["OpenAI:OpenQuestionModel"] ?? "gpt-4.1";
        var maxTokens = int.TryParse(configuration["OpenAI:MaxTokensOpenQuestion"], out var t) ? t : 300;

        var requestPayload = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = systemPromptBuilder.ToString() },
                new { role = "user", content = userPrompt }
            },
            max_tokens = maxTokens,
            response_format = new { type = "json_object" }
        };

        var requestContent = new StringContent(
            JsonSerializer.Serialize(requestPayload),
            Encoding.UTF8,
            "application/json");

        httpClient.DefaultRequestHeaders.Remove("Authorization");
        httpClient.DefaultRequestHeaders.Remove("X-Auth-Token");

        if (openAiBaseUrl.Contains("f5ai"))
            httpClient.DefaultRequestHeaders.Add("X-Auth-Token", openAiApiKey);
        else
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openAiApiKey);

        using var response = await httpClient.PostAsync(apiUrl, requestContent, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ExerciseEvaluationResult(
                IsCorrect: true,
                Score: 50,
                Explanation: null,
                AiFeedback: "Не удалось получить AI-оценку. Попробуй ещё раз.");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseJson = JsonDocument.Parse(responseBody);
        var aiResponseText = responseJson.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";

        return ParseAiResponse(aiResponseText);
    }

    protected static ExerciseEvaluationResult ParseAiResponse(string aiResponseText)
    {
        try
        {
            var aiResult = JsonDocument.Parse(aiResponseText).RootElement;

            var passed = aiResult.TryGetProperty("passed", out var passedElement)
                && passedElement.GetBoolean();

            var rating = aiResult.TryGetProperty("rating", out var ratingElement)
                ? ratingElement.GetInt32() : 5;

            var feedback = aiResult.TryGetProperty("feedback", out var feedbackElement)
                ? feedbackElement.GetString() : null;

            var score = rating * 10;
            var isCorrect = passed || rating >= 8;

            return new ExerciseEvaluationResult(
                IsCorrect: isCorrect,
                Score: score,
                Explanation: $"Оценка: {rating}/10",
                AiFeedback: feedback);
        }
        catch
        {
            return new ExerciseEvaluationResult(
                IsCorrect: true,
                Score: 50,
                Explanation: null,
                AiFeedback: "Ошибка разбора ответа AI.");
        }
    }
}
