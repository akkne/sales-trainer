using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SalesTrainer.Api.Features.Dialog;

public class OpenAiChatService : IOpenAiChatService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiChatService> _logger;

    private const string PlaceholderApiKey = "REPLACE_WITH_OPENAI_API_KEY";

    private const string StopSignalInstruction = @"

ВАЖНО — ПРАВИЛА ЗАВЕРШЕНИЯ ДИАЛОГА:

1. НЕМЕДЛЕННОЕ ЗАВЕРШЕНИЕ (критические ошибки пользователя):
Если пользователь допустил одну из критических ошибок — немедленно заверши разговор в рамках роли и добавь [DIALOG_END]:
- Грубость, агрессия, раздражение в голосе
- Потеря уверенности: оправдания, извинения, неловкие паузы, ""ну... как бы... в общем...""
- Попытка давить или умолять: ""пожалуйста"", ""ну хотя бы"", ""я прошу вас""
- Слабый бессодержательный опеннер без конкретики (""хочу предложить сотрудничество"")
- Повтор одного и того же аргумента после отказа без новой ценности
- Ложь или манипуляция (ссылка на несуществующих людей/договорённости)
В этом случае твой персонаж естественно завершает разговор (вешает трубку, прощается) — и ты добавляешь [DIALOG_END] в конце.

2. ШТАТНОЕ ЗАВЕРШЕНИЕ:
Когда разговор подошёл к логическому концу (соединил/не соединил, согласился/отказался окончательно), добавь [DIALOG_END].

Тег [DIALOG_END] ставится на отдельной строке в конце сообщения.";

    private const string XpInstructionSuffix = @"

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

    public async Task<ChatMessageResult> SendChatMessageAsync(string systemPrompt, List<DialogMessage> conversationHistory)
    {
        var chatModel = _configuration["OpenAI:ChatModel"] ?? "gpt-4.1-mini";
        var maxTokens = int.TryParse(_configuration["OpenAI:MaxTokensChat"], out var chatTokens) ? chatTokens : 500;

        var enhancedSystemPrompt = systemPrompt + StopSignalInstruction;

        var response = await CallOpenAiAsync(enhancedSystemPrompt, conversationHistory, chatModel, maxTokens);

        var isStopSignal = response.Contains("[DIALOG_END]");
        var cleanedContent = response.Replace("[DIALOG_END]", "").Trim();

        return new ChatMessageResult
        {
            Content = cleanedContent,
            IsStopSignal = isStopSignal
        };
    }

    public async Task<FeedbackResult> GenerateFeedbackAsync(string feedbackPrompt, List<DialogMessage> conversationHistory)
    {
        var feedbackModel = _configuration["OpenAI:FeedbackModel"] ?? "gpt-4.1";
        var maxTokens = int.TryParse(_configuration["OpenAI:MaxTokensFeedback"], out var feedbackTokens) ? feedbackTokens : 1500;

        var conversationAsText = FormatConversationForFeedback(conversationHistory);
        var fullPrompt = $"{feedbackPrompt}{XpInstructionSuffix}\n\n--- Диалог ---\n{conversationAsText}";

        var emptyHistory = new List<DialogMessage>
        {
            new() { Role = "user", Content = fullPrompt, Timestamp = DateTime.UtcNow }
        };

        var response = await CallOpenAiAsync("You are an expert sales coach providing detailed feedback in Russian.", emptyHistory, feedbackModel, maxTokens);

        var xpReward = ExtractXpReward(response);
        var cleanedContent = Regex.Replace(response, @"\[XP:\d+\]", "").Trim();

        return new FeedbackResult
        {
            Content = cleanedContent,
            XpReward = xpReward
        };
    }

    private static int ExtractXpReward(string response)
    {
        var match = Regex.Match(response, @"\[XP:(\d+)\]");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var xp))
        {
            return Math.Clamp(xp, 0, 100);
        }
        return 25;
    }

    private async Task<string> CallOpenAiAsync(
        string systemPrompt,
        List<DialogMessage> conversationHistory,
        string model,
        int maxTokens)
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
            max_tokens = maxTokens,
            temperature = 0.7
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _logger.LogInformation("Calling OpenAI API with model {Model}", model);

        var response = await httpClient.PostAsync(apiUrl, httpContent);
        var responseContent = await response.Content.ReadAsStringAsync();

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
                throw new OpenAiAuthException("AI service authentication failed. Please check API configuration.");
            }

            throw new HttpRequestException($"OpenAI API returned {response.StatusCode}: {responseContent}");
        }

        _logger.LogDebug("OpenAI API response: {Response}", responseContent);

        var responseJson = JsonDocument.Parse(responseContent);
        var root = responseJson.RootElement;

        // Try standard OpenAI format: { choices: [{ message: { content: "..." } }] }
        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? string.Empty;
            }
        }

        // Try f5ai format: { message: { content: "..." } } (no choices array)
        if (root.TryGetProperty("message", out var directMessage) &&
            directMessage.TryGetProperty("content", out var messageContent))
        {
            return messageContent.GetString() ?? string.Empty;
        }

        // Try alternative format: { content: "..." } or { text: "..." }
        if (root.TryGetProperty("content", out var directContent))
        {
            return directContent.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("text", out var textContent))
        {
            return textContent.GetString() ?? string.Empty;
        }

        // Try f5ai specific format: { result: { content: "..." } }
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

public class OpenAiPaymentRequiredException(string message) : Exception(message);
public class OpenAiRateLimitException(string message) : Exception(message);
public class OpenAiAuthException(string message) : Exception(message);
