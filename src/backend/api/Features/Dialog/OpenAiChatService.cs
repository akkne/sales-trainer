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

    private const string OpenAiApiBaseUrl = "https://api.openai.com/v1/chat/completions";
    private const string PlaceholderApiKey = "REPLACE_WITH_OPENAI_API_KEY";

    private const string StopSignalInstruction = @"

ВАЖНО: Когда разговор подошёл к логическому завершению (клиент согласился/отказался, секретарь соединил/не соединил, сделка закрыта/не закрыта), добавь в КОНЕЦ своего сообщения на отдельной строке тег:
[DIALOG_END]

Этот тег означает, что пользователь может завершить диалог и получить обратную связь.";

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
        var httpClient = _httpClientFactory.CreateClient("OpenAI");
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

        var response = await httpClient.PostAsync(OpenAiApiBaseUrl, httpContent);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
            throw new HttpRequestException($"OpenAI API returned {response.StatusCode}: {responseContent}");
        }

        var responseJson = JsonDocument.Parse(responseContent);
        var assistantMessage = responseJson.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return assistantMessage ?? string.Empty;
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
