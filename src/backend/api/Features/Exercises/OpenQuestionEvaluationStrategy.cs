using System.Text;
using System.Text.Json;

namespace SalesTrainer.Api.Features.Exercises;

public class OpenQuestionEvaluationStrategy(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => "open_question";

    public async Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer)
    {
        var question = exerciseContent.GetProperty("question").GetString() ?? "";
        var evaluationCriteria = exerciseContent.TryGetProperty("evaluationCriteria", out var criteriaElement)
            ? criteriaElement.GetString() ?? ""
            : "";
        var userResponseText = userAnswer.GetProperty("text").GetString() ?? "";

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

        var systemPrompt =
            "Ты — строгий эксперт по продажам. Оцени ответ по шкале от 0 до 10. " +
            "НЕ хвали и НЕ пиши про сильные стороны — только что улучшить. " +
            "Отвечай ТОЛЬКО в JSON: {\"rating\": 0-10, \"improvements\": \"2-3 коротких совета что добавить/улучшить\"}. " +
            "Формат improvements: \"Можно добавить X. Стоит уточнить Y. Попробуй Z.\"";

        var userPrompt = string.IsNullOrEmpty(evaluationCriteria)
            ? $"Вопрос: {question}\n\nОтвет пользователя: {userResponseText}"
            : $"Вопрос: {question}\n\nКритерии оценки: {evaluationCriteria}\n\nОтвет пользователя: {userResponseText}";

        var maxTokens = int.TryParse(configuration["OpenAI:MaxTokensOpenQuestion"], out var t) ? t : 200;

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
        var rating = aiResult.TryGetProperty("rating", out var ratingElement)
            ? ratingElement.GetInt32() : 5;
        var improvements = aiResult.TryGetProperty("improvements", out var improvementsElement)
            ? improvementsElement.GetString() : null;

        // Rating 0-10, correct if >= 8
        var isCorrect = rating >= 8;
        // Convert 0-10 rating to 0-100 score for consistency
        var score = rating * 10;

        return new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: score,
            Explanation: $"Оценка: {rating}/10",
            AiFeedback: improvements);
    }
}
