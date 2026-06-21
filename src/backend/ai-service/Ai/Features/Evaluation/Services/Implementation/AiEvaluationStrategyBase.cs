using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Evaluation.Models;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Evaluation.Services.Implementation;

internal abstract class AiEvaluationStrategyBase(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenAiConfiguration> openAiOptions,
    ILogger logger)
{
    protected async Task<ExerciseEvaluationResult> EvaluateWithAiAsync(
        string userPrompt,
        string? perExercisePrompt,
        string? globalSystemPrompt,
        CancellationToken cancellationToken)
    {
        var openAiApiKey = openAiOptions.Value.ApiKey;
        if (string.IsNullOrEmpty(openAiApiKey) || openAiApiKey.StartsWith("REPLACE_"))
        {
            throw new InvalidOperationException("OpenAI API key is not configured");
        }

        var systemPromptBuilder = new StringBuilder();
        if (!string.IsNullOrEmpty(globalSystemPrompt))
        {
            systemPromptBuilder.AppendLine(globalSystemPrompt);
        }
        if (!string.IsNullOrEmpty(perExercisePrompt))
        {
            systemPromptBuilder.AppendLine();
            systemPromptBuilder.AppendLine("Дополнительные критерии:");
            systemPromptBuilder.AppendLine(perExercisePrompt);
        }

        var httpClient = httpClientFactory.CreateClient("OpenAI");
        var openAiBaseUrl = openAiOptions.Value.BaseUrl;
        var completionsPath = openAiOptions.Value.ChatCompletionsPath;
        var apiUrl = openAiBaseUrl.TrimEnd('/') + completionsPath;
        var model = openAiOptions.Value.OpenQuestionModel;
        var maxTokens = openAiOptions.Value.MaximumOpenQuestionTokenCount;

        var systemPromptWithFormat = systemPromptBuilder.ToString();
        if (string.IsNullOrEmpty(systemPromptWithFormat))
        {
            systemPromptWithFormat = "Ты — эксперт по оценке ответов на упражнения. Оценивай ответ пользователя.";
        }
        systemPromptWithFormat += "\n\nОТВЕТ СТРОГО В ФОРМАТЕ JSON: {\"passed\": true/false, \"rating\": 1-10, \"feedback\": \"текст обратной связи\"}";

        var requestPayload = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = systemPromptWithFormat },
                new { role = "user", content = userPrompt }
            },
            max_tokens = maxTokens
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

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("OpenAI evaluation API error: {StatusCode} - {Body}", response.StatusCode, RedactAndTruncate(responseBody));
            throw new HttpRequestException("AI provider error");
        }

        if (responseBody.Contains("\"error\""))
        {
            try
            {
                using var errorDocument = JsonDocument.Parse(responseBody);
                if (errorDocument.RootElement.ValueKind == JsonValueKind.Object &&
                    errorDocument.RootElement.TryGetProperty("error", out _))
                {
                    logger.LogError("OpenAI evaluation returned error body: {Body}", RedactAndTruncate(responseBody));
                    throw new HttpRequestException("AI provider error");
                }
            }
            catch (JsonException)
            {
            }
        }

        var responseJson = JsonDocument.Parse(responseBody);

        string aiResponseText;
        if (responseJson.RootElement.TryGetProperty("message", out var messageEl) &&
            messageEl.TryGetProperty("content", out var contentEl))
        {
            aiResponseText = contentEl.GetString() ?? "{}";
        }
        else if (responseJson.RootElement.TryGetProperty("choices", out var choices) &&
                 choices.GetArrayLength() > 0)
        {
            aiResponseText = choices[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "{}";
        }
        else
        {
            logger.LogError("Unexpected AI response format: {Body}", RedactAndTruncate(responseBody));
            throw new InvalidOperationException("Unexpected AI response format");
        }

        return ParseAiResponse(aiResponseText);
    }

    protected static ExerciseEvaluationResult ParseAiResponse(string aiResponseText)
    {
        try
        {
            var aiResult = JsonDocument.Parse(aiResponseText).RootElement;

            var passed = aiResult.TryGetProperty("passed", out var passedElement) && GetBooleanSafe(passedElement);

            var rating = aiResult.TryGetProperty("rating", out var ratingElement)
                ? GetInt32Safe(ratingElement, defaultValue: 5)
                : 5;
            rating = Math.Clamp(rating, 1, 10);

            var feedback = aiResult.TryGetProperty("feedback", out var feedbackElement)
                ? feedbackElement.GetString()
                : null;

            var score = rating * 10;
            var isCorrect = passed || rating >= 8;

            return new ExerciseEvaluationResult(
                IsCorrect: isCorrect,
                Score: score,
                Explanation: $"Оценка: {rating}/10",
                AiFeedback: feedback);
        }
        catch (Exception)
        {
            // Degrade gracefully: return a failed-but-valid result rather than throwing into a 503
            return new ExerciseEvaluationResult(
                IsCorrect: false,
                Score: 0,
                Explanation: "Не удалось разобрать ответ AI",
                AiFeedback: null);
        }
    }

    private static bool GetBooleanSafe(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(element.GetString(), out var b) && b,
            JsonValueKind.Number => element.TryGetInt32(out var n) && n != 0,
            _ => false
        };

    private static int GetInt32Safe(JsonElement element, int defaultValue) =>
        element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt32(out var n) ? n : defaultValue,
            JsonValueKind.String => int.TryParse(element.GetString(), out var n) ? n : defaultValue,
            _ => defaultValue
        };

    private static string RedactAndTruncate(string body)
    {
        const int maxLength = 500;
        var redacted = Regex.Replace(body, @"sk-[A-Za-z0-9\-_]{8,}", "[REDACTED]", RegexOptions.None, TimeSpan.FromSeconds(1));
        redacted = Regex.Replace(redacted, @"(?i)(Authorization|X-Auth-Token)\s*[:=]\s*\S+", "$1=[REDACTED]", RegexOptions.None, TimeSpan.FromSeconds(1));
        return redacted.Length > maxLength ? redacted[..maxLength] + "…" : redacted;
    }
}
