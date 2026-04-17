using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SalesTrainer.Api.Features.Dialog.Models;
using SalesTrainer.Api.Features.Dialog.Services.Abstract;

namespace SalesTrainer.Api.Features.Dialog.Services.Implementation;

internal sealed class OpenAiChatService : IOpenAiChatService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiChatService> _logger;

    private const string PlaceholderApiKey = "REPLACE_WITH_OPENAI_API_KEY";

    private const string StopSignalInstruction = @"

ВАЖНО — ПРАВИЛА ЗАВЕРШЕНИЯ ДИАЛОГА:

1. НЕМЕДЛЕННОЕ ЗАВЕРШЕНИЕ (критические ошибки пользователя):
Если пользователь допустил одну из критических ошибок — немедленно заверши разговор в рамках роли и добавь [DIALOG_END]:
- Мат, оскорбления, агрессия — сразу кладёшь трубку: ""Такое общение неприемлемо. Всего хорошего."" [DIALOG_END]
- Грубость, раздражение в голосе — ""Мне не нравится ваш тон. До свидания."" [DIALOG_END]
- Потеря уверенности: оправдания, мычание, ""ну... как бы... в общем..."" — ты занятой человек, не будешь ждать
- Попытка давить или умолять: ""пожалуйста"", ""ну хотя бы"", ""я прошу вас"" — слабость, прощайся
- Слабый бессодержательный опеннер без конкретики (""хочу предложить сотрудничество"") — ""Что конкретно? У меня мало времени"", если не уточняет — прощайся
- Повтор одного и того же аргумента после отказа без новой ценности — ""Я уже сказал нет"" [DIALOG_END]
- Ложь или манипуляция — сразу заканчивай разговор

Будь строгим, как настоящий занятой человек. Не церемонься. Если звонок не интересен — клади трубку.

2. ШТАТНОЕ ЗАВЕРШЕНИЕ:
Когда разговор подошёл к логическому концу (соединил/не соединил, согласился/отказался окончательно), добавь [DIALOG_END].

Тег [DIALOG_END] ставится на отдельной строке в конце сообщения.

ФОРМАТ ОТВЕТА:
Отвечай ТОЛЬКО текст реплики персонажа. НЕ добавляй имя персонажа в начале (""Анна:"", ""Занятая Анна:"" и т.п.). Просто текст ответа.";

    private const string ExperiencePointsInstructionSuffix = @"

ФОРМАТ ОТВЕТА:
Твой ответ должен состоять из ДВУХ БЛОКОВ, разделённых тегом [DETAILED]:

ПЕРВЫЙ БЛОК (до [DETAILED]) — КРАТКОЕ РЕЗЮМЕ (2-3 предложения):
Самое важное: что было хорошо ИЛИ что было критически плохо. Используй <strong> для выделения ключевых слов.

[DETAILED]

ВТОРОЙ БЛОК (после [DETAILED]) — ПОДРОБНЫЙ РАЗБОР:
Используй теги <h3>, <p>, <ul>, <li>, <strong> (жирный для ключевых моментов), <em> (курсив для примеров из диалога). НЕ используй Markdown.

Структура подробного разбора:
<h3>Общая оценка</h3>
<p>Краткое резюме: удалось ли достичь цели, что было ключевым моментом.</p>

<h3>Что сделано хорошо</h3>
<ul>
<li><strong>Критерий:</strong> <em>конкретный пример из диалога</em></li>
</ul>

<h3>Что нужно улучшить</h3>
<ul>
<li><strong>Проблема:</strong> что было не так и почему это критично</li>
</ul>

<h3>Рекомендации</h3>
<ul>
<li>Конкретная фраза или техника, которую стоит использовать</li>
</ul>

В КОНЦЕ своего ответа на отдельной строке укажи количество XP, которое заслужил пользователь за этот диалог в формате:
[XP:число]

Критерии начисления XP (от 0 до 100):
- Уверенность и профессионализм: до 30 XP
- Работа с возражениями: до 30 XP
- Достижение цели (прошёл секретаря, назначил встречу и т.д.): до 40 XP

Например: [XP:75]";

    public OpenAiChatService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenAiChatService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            var apiKey = _configuration["OpenAI:ApiKey"];
            return !string.IsNullOrWhiteSpace(apiKey) &&
                   apiKey != PlaceholderApiKey &&
                   !apiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase);
        }
    }

    public async Task<ChatMessageResult> SendChatMessageAsync(
        string systemPrompt,
        List<DialogMessage> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        var chatModel = _configuration["OpenAI:ChatModel"] ?? "gpt-4.1-mini";
        var maximumTokenCount = int.TryParse(_configuration["OpenAI:MaxTokensChat"], out var chatTokens) ? chatTokens : 500;

        var enhancedSystemPrompt = systemPrompt + StopSignalInstruction;

        var response = await CallOpenAiAsync(enhancedSystemPrompt, conversationHistory, chatModel, maximumTokenCount, cancellationToken);

        var isStopSignal = response.Contains("[DIALOG_END]");
        var cleanedContent = response.Replace("[DIALOG_END]", "").Trim();

        return new ChatMessageResult
        {
            Content = cleanedContent,
            IsStopSignal = isStopSignal
        };
    }

    public async Task<FeedbackResult> GenerateFeedbackAsync(
        string feedbackPrompt,
        List<DialogMessage> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        var feedbackModel = _configuration["OpenAI:FeedbackModel"] ?? "gpt-4.1";
        var maximumTokenCount = int.TryParse(_configuration["OpenAI:MaxTokensFeedback"], out var feedbackTokens) ? feedbackTokens : 1500;

        var conversationAsText = FormatConversationForFeedback(conversationHistory);
        var fullPrompt = $"{feedbackPrompt}{ExperiencePointsInstructionSuffix}\n\n--- Диалог ---\n{conversationAsText}";

        var emptyHistory = new List<DialogMessage>
        {
            new() { Role = "user", Content = fullPrompt, Timestamp = DateTime.UtcNow }
        };

        var response = await CallOpenAiAsync("You are an expert sales coach providing detailed feedback in Russian.", emptyHistory, feedbackModel, maximumTokenCount, cancellationToken);

        _logger.LogDebug("Feedback response from AI: {Response}", response);

        var experiencePointsReward = ExtractExperiencePointsReward(response);
        var cleanedContent = Regex.Replace(response, @"\[XP:\d+\]", "").Trim();

        var (summary, detailedContent) = ExtractSummaryAndContent(cleanedContent);

        _logger.LogInformation("Extracted feedback summary length: {SummaryLength}, content length: {ContentLength}", summary.Length, detailedContent.Length);

        return new FeedbackResult
        {
            Summary = summary,
            Content = detailedContent,
            XpReward = experiencePointsReward
        };
    }

    private static (string Summary, string Content) ExtractSummaryAndContent(string response)
    {
        const string delimiter = "[DETAILED]";
        var delimiterIndex = response.IndexOf(delimiter, StringComparison.OrdinalIgnoreCase);

        if (delimiterIndex >= 0)
        {
            var summary = response[..delimiterIndex].Trim();
            var content = response[(delimiterIndex + delimiter.Length)..].Trim();
            return (summary, content);
        }

        var summaryFallback = ExtractFirstSentences(response, 3);
        return (summaryFallback, response);
    }

    private static string ExtractFirstSentences(string text, int count)
    {
        var plainText = Regex.Replace(text, @"<[^>]+>", " ");
        plainText = Regex.Replace(plainText, @"\s+", " ").Trim();

        var sentences = Regex.Split(plainText, @"(?<=[.!?])\s+");
        var result = string.Join(" ", sentences.Take(count));

        return string.IsNullOrWhiteSpace(result) ? text : result;
    }

    private static int ExtractExperiencePointsReward(string response)
    {
        var match = Regex.Match(response, @"\[XP:(\d+)\]");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var experiencePoints))
        {
            return Math.Clamp(experiencePoints, 0, 100);
        }
        return 25;
    }

    private async Task<string> CallOpenAiAsync(
        string systemPrompt,
        List<DialogMessage> conversationHistory,
        string model,
        int maximumTokenCount,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("OpenAI API is not configured");
        }

        var apiKey = _configuration["OpenAI:ApiKey"]!;
        var baseUrl = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com";
        var completionsPath = _configuration["OpenAI:ChatCompletionsPath"] ?? "/v1/chat/completions";
        var apiUrl = baseUrl.TrimEnd('/') + completionsPath;

        var httpClient = _httpClientFactory.CreateClient("OpenAI");
        httpClient.DefaultRequestHeaders.Remove("Authorization");
        httpClient.DefaultRequestHeaders.Remove("X-Auth-Token");

        if (baseUrl.Contains("f5ai"))
            httpClient.DefaultRequestHeaders.Add("X-Auth-Token", apiKey);
        else
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        foreach (var message in conversationHistory)
        {
            messages.Add(new { role = message.Role, content = message.Content });
        }

        var requestBody = new
        {
            model,
            messages,
            max_tokens = maximumTokenCount,
            temperature = 0.7
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _logger.LogInformation("Calling OpenAI API with model {Model}", model);

        var response = await httpClient.PostAsync(apiUrl, httpContent, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI API error: {StatusCode} - {Content}", response.StatusCode, responseContent);

            if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
            {
                throw new OpenAiPaymentRequiredException("AI service requires payment. Please check your API balance.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                throw new OpenAiRateLimitException("AI service rate limit exceeded. Please try again later.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new OpenAiAuthenticationException("AI service authentication failed. Please check API configuration.");
            }

            throw new HttpRequestException($"OpenAI API returned {response.StatusCode}: {responseContent}");
        }

        _logger.LogDebug("OpenAI API response: {Response}", responseContent);

        var responseJson = JsonDocument.Parse(responseContent);
        var root = responseJson.RootElement;

        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? string.Empty;
            }
        }

        if (root.TryGetProperty("message", out var directMessage) &&
            directMessage.TryGetProperty("content", out var messageContent))
        {
            return messageContent.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("content", out var directContent))
        {
            return directContent.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("text", out var textContent))
        {
            return textContent.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("result", out var result))
        {
            if (result.TryGetProperty("content", out var resultContent))
            {
                return resultContent.GetString() ?? string.Empty;
            }
            if (result.TryGetProperty("text", out var resultText))
            {
                return resultText.GetString() ?? string.Empty;
            }
        }

        _logger.LogError("Unable to parse OpenAI response format: {Response}", responseContent);
        throw new InvalidOperationException($"Unexpected API response format: {responseContent}");
    }

    private static string FormatConversationForFeedback(List<DialogMessage> messages)
    {
        var stringBuilder = new StringBuilder();
        foreach (var message in messages)
        {
            var roleLabel = message.Role == "assistant" ? "Клиент" : "Менеджер";
            stringBuilder.AppendLine($"{roleLabel}: {message.Content}");
        }
        return stringBuilder.ToString();
    }
}
