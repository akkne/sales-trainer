using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises;

internal sealed class OpenQuestionEvaluationStrategy(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    AppDbContext databaseContext) : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "open_question";

    public async Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var question = exerciseContent.GetProperty("question").GetString() ?? "";
        var perQuestionPrompt = exerciseContent.TryGetProperty("aiPrompt", out var promptElement)
            ? promptElement.GetString() ?? ""
            : "";
        var userResponseText = userAnswer.GetProperty("text").GetString() ?? "";

        var globalContext = await databaseContext.OpenQuestionGlobalContexts
            .Select(context => context.ContextText)
            .FirstOrDefaultAsync(cancellationToken) ?? "";

        var openAiApiKey = configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(openAiApiKey) || openAiApiKey.StartsWith("REPLACE_"))
        {
            return new ExerciseEvaluationResult(
                IsCorrect: true,
                Score: 80,
                Explanation: null,
                AiFeedback: "AI-оценка недоступна — ключ OpenAI не настроен.");
        }

        var httpClient = httpClientFactory.CreateClient("OpenAI");
        var openAiBaseUrl = configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com";
        var completionsPath = configuration["OpenAI:ChatCompletionsPath"] ?? "/v1/chat/completions";
        var apiUrl = openAiBaseUrl.TrimEnd('/') + completionsPath;
        var model = configuration["OpenAI:OpenQuestionModel"] ?? "gpt-4.1";

        var systemPromptBuilder = new StringBuilder();

        if (!string.IsNullOrEmpty(globalContext))
        {
            systemPromptBuilder.AppendLine(globalContext);
            systemPromptBuilder.AppendLine();
        }

        systemPromptBuilder.AppendLine(
            "Отвечай ТОЛЬКО в JSON формате: {\"rating\": 0-10, \"improvements\": \"2-3 коротких совета что улучшить в формате 'Можно добавить X. Стоит уточнить Y.'\"}. " +
            "НЕ пиши про сильные стороны — только что улучшить.");

        var userPrompt = $"Вопрос: {question}\n\nОтвет пользователя: {userResponseText}";
        if (!string.IsNullOrEmpty(perQuestionPrompt))
        {
            userPrompt += $"\n\nКритерии оценки: {perQuestionPrompt}";
        }

        var maximumTokenCount = int.TryParse(configuration["OpenAI:MaxTokensOpenQuestion"], out var tokenCount) ? tokenCount : 200;

        var requestPayload = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = systemPromptBuilder.ToString() },
                new { role = "user", content = userPrompt }
            },
            max_tokens = maximumTokenCount,
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

        var aiResult = JsonDocument.Parse(aiResponseText).RootElement;
        var rating = aiResult.TryGetProperty("rating", out var ratingElement)
            ? ratingElement.GetInt32() : 5;
        var improvements = aiResult.TryGetProperty("improvements", out var improvementsElement)
            ? improvementsElement.GetString() : null;

        var isCorrect = rating >= 8;
        var score = rating * 10;

        return new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: score,
            Explanation: $"Оценка: {rating}/10",
            AiFeedback: improvements);
    }
}
