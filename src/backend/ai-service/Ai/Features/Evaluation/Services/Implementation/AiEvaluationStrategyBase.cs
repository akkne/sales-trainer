using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Evaluation.Models;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Evaluation.Services.Implementation;

internal abstract class AiEvaluationStrategyBase(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenAiConfiguration> openAiOptions)
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
            throw new HttpRequestException($"OpenAI API returned {response.StatusCode}: {responseBody}");
        }

        if (responseBody.Contains("\"error\""))
        {
            try
            {
                using var errorDocument = JsonDocument.Parse(responseBody);
                if (errorDocument.RootElement.ValueKind == JsonValueKind.Object &&
                    errorDocument.RootElement.TryGetProperty("error", out var errorElement))
                {
                    var errorMessage = errorElement.ValueKind == JsonValueKind.Object &&
                                       errorElement.TryGetProperty("message", out var messageElement)
                        ? messageElement.ToString()
                        : errorElement.ToString();
                    throw new HttpRequestException($"OpenAI API error: {errorMessage}. Full response: {responseBody}");
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
            throw new InvalidOperationException($"Unexpected API response format: {responseBody}");
        }

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
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Failed to parse AI response: {aiResponseText}", exception);
        }
    }
}
