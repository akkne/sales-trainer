using System.Text;
using System.Text.Json;

namespace SalesTrainer.Api.Features.Exercises;

public class FreeTextEvaluationStrategy(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "free_text";

    public async Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer)
    {
        var situation = exerciseContent.GetProperty("situation").GetString() ?? "";
        var evaluationCriteria = exerciseContent.GetProperty("evaluationCriteria").GetString() ?? "";
        var userResponseText = userAnswer.GetProperty("text").GetString() ?? "";

        var openAiApiKey = configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(openAiApiKey) || openAiApiKey.StartsWith("REPLACE_"))
        {
            return new ExerciseEvaluationResult(
                IsCorrect: true,
                Score: 70,
                Explanation: null,
                AiFeedback: "AI-оценка недоступна — ключ OpenAI не настроен.");
        }

        var httpClient = httpClientFactory.CreateClient("OpenAI");
        var openAiBaseUrl = configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com";
        var completionsPath = configuration["OpenAI:ChatCompletionsPath"] ?? "/v1/chat/completions";
        var apiUrl = openAiBaseUrl.TrimEnd('/') + completionsPath;
        var model = configuration["OpenAI:FreeTextModel"] ?? "gpt-4o-mini";
        var systemPrompt =
            "Ты — тренер по продажам. Оцени ответ менеджера по продажам. " +
            "Отвечай ТОЛЬКО в JSON формате: {\"score\": 0-100, \"feedback\": \"краткий фидбек на русском\", \"isCorrect\": true/false}. " +
            "isCorrect = true если score >= 60.";

        var userPrompt =
            $"Ситуация: {situation}\n\nКритерии оценки: {evaluationCriteria}\n\nОтвет менеджера: {userResponseText}";

        var maxTokens = int.TryParse(configuration["OpenAI:MaxTokensFreeText"], out var t) ? t : 300;

        var requestPayload = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
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

        using var response = await httpClient.PostAsync(apiUrl, requestContent);

        if (!response.IsSuccessStatusCode)
        {
            return new ExerciseEvaluationResult(
                IsCorrect: true,
                Score: 50,
                Explanation: null,
                AiFeedback: "Не удалось получить AI-оценку. Попробуй ещё раз.");
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        var responseJson = JsonDocument.Parse(responseBody);
        var aiResponseText = responseJson.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";

        var aiResult = JsonDocument.Parse(aiResponseText).RootElement;
        var score = aiResult.TryGetProperty("score", out var scoreElement)
            ? scoreElement.GetInt32() : 50;
        var feedback = aiResult.TryGetProperty("feedback", out var feedbackElement)
            ? feedbackElement.GetString() : null;
        var isCorrect = score >= 60;

        return new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: score,
            Explanation: null,
            AiFeedback: feedback);
    }
}
