using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Sellevate.Learning.Infrastructure.Ai;

internal sealed class OpenAiChatService : IOpenAiChatService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<OpenAiConfiguration> _openAiOptions;
    private readonly ILogger<OpenAiChatService> _logger;

    private const string PlaceholderApiKey = "REPLACE_WITH_OPENAI_API_KEY";

    private const string StructuredReplyInstruction = @"

ФОРМАТ ОТВЕТА (строго):
Отвечай ТОЛЬКО валидным JSON-объектом без пояснений и без markdown:
{""reply"": ""<текст реплики персонажа>"", ""endCall"": true|false}
Поле ""reply"" всегда идёт первым. НЕ добавляй имя персонажа в начало реплики (""Анна:"", ""Занятая Анна:"" и т.п.) — только текст реплики.

ПОЛЕ endCall — ЗАВЕРШЕНИЕ ЗВОНКА:
endCall: true означает, что твой персонаж кладёт трубку. В reply при этом — его финальная фраза.

1. НЕМЕДЛЕННОЕ ЗАВЕРШЕНИЕ (критические ошибки пользователя):
Если пользователь допустил одну из критических ошибок — немедленно заверши разговор в рамках роли и поставь endCall: true:
- Мат, оскорбления, агрессия — сразу кладёшь трубку: reply ""Такое общение неприемлемо. Всего хорошего."", endCall: true
- Грубость, раздражение в голосе — reply ""Мне не нравится ваш тон. До свидания."", endCall: true
- Бессвязная речь, чушь, разговор не по делу — уточни один раз, если продолжается — прощайся, endCall: true
- Потеря уверенности: оправдания, мычание, ""ну... как бы... в общем..."" — ты занятой человек, не будешь ждать, прощайся
- Попытка давить или умолять: ""пожалуйста"", ""ну хотя бы"", ""я прошу вас"" — слабость, прощайся
- Слабый бессодержательный опеннер без конкретики (""хочу предложить сотрудничество"") — ""Что конкретно? У меня мало времени"", если не уточняет — прощайся
- Повтор одного и того же аргумента после отказа без новой ценности — ""Я уже сказал нет"", endCall: true
- Ложь или манипуляция — сразу заканчивай разговор, endCall: true

Будь строгим, как настоящий занятой человек. Не церемонься. Если звонок не интересен — клади трубку.

2. ШТАТНОЕ ЗАВЕРШЕНИЕ:
Когда разговор подошёл к логическому концу (соединил/не соединил, согласился/отказался окончательно) — endCall: true.

Во всех остальных случаях — endCall: false.";

    public OpenAiChatService(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenAiConfiguration> openAiOptions,
        ILogger<OpenAiChatService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _openAiOptions = openAiOptions;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            var apiKey = _openAiOptions.Value.ApiKey;
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
        var chatModel = _openAiOptions.Value.DialogModel;
        var maxTokens = _openAiOptions.Value.MaximumDialogTokenCount;

        var response = await CallOpenAiAsync(
            systemPrompt + StructuredReplyInstruction,
            conversationHistory,
            chatModel,
            maxTokens,
            BuildChatReplyResponseFormat(),
            cancellationToken);

        var replyParser = new StreamingChatReplyParser();
        replyParser.Push(response);
        var parseResult = replyParser.Complete();

        if (parseResult.UsedFallback)
            _logger.LogWarning("Chat model ignored the JSON reply contract; recovered plain-text reply ({Length} chars)", parseResult.Reply.Length);

        return new ChatMessageResult
        {
            Content = parseResult.Reply,
            IsStopSignal = parseResult.EndCall
        };
    }

    public async IAsyncEnumerable<string> StreamChatMessageAsync(
        string systemPrompt,
        List<DialogMessage> conversationHistory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("OpenAI API is not configured");

        var chatModel = _openAiOptions.Value.DialogModel;
        var maxTokens = _openAiOptions.Value.MaximumDialogTokenCount;

        var (httpClient, apiUrl) = CreateConfiguredClient();

        var messages = BuildMessages(systemPrompt + StructuredReplyInstruction, conversationHistory);
        var requestBody = new Dictionary<string, object>
        {
            ["model"] = chatModel,
            ["messages"] = messages,
            ["max_tokens"] = maxTokens,
            ["temperature"] = _openAiOptions.Value.DialogTemperature,
            ["stream"] = true,
            ["response_format"] = BuildChatReplyResponseFormat()
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };

        _logger.LogInformation("Streaming OpenAI completion with model {Model}", chatModel);

        using var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("OpenAI streaming error: {StatusCode} - {Content}", response.StatusCode, errorBody);
            throw new HttpRequestException($"OpenAI stream returned {response.StatusCode}: {errorBody}");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(contentType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            var fullBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("Provider returned non-SSE chat completion ({ContentType}); yielding it as a single delta", contentType);
            var fullContent = ExtractContentFromCompletionResponse(fullBody, _logger);
            if (!string.IsNullOrEmpty(fullContent))
                yield return fullContent;
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            if (cancellationToken.IsCancellationRequested) yield break;

            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var payload = line["data:".Length..].Trim();
            if (payload == "[DONE]") yield break;

            string? delta = null;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var first = choices[0];
                    if (first.TryGetProperty("delta", out var deltaElement) &&
                        deltaElement.TryGetProperty("content", out var contentElement))
                    {
                        delta = contentElement.GetString();
                    }
                }
            }
            catch (JsonException)
            {
                _logger.LogDebug("Skipping non-JSON SSE payload: {Payload}", payload);
                continue;
            }

            if (!string.IsNullOrEmpty(delta))
                yield return delta;
        }
    }

    private (HttpClient Client, string ApiUrl) CreateConfiguredClient()
    {
        var apiKey = _openAiOptions.Value.ApiKey;
        var baseUrl = _openAiOptions.Value.BaseUrl;
        var completionsPath = _openAiOptions.Value.ChatCompletionsPath;
        var apiUrl = baseUrl.TrimEnd('/') + completionsPath;

        var client = _httpClientFactory.CreateClient("OpenAI");
        client.DefaultRequestHeaders.Remove("Authorization");
        client.DefaultRequestHeaders.Remove("X-Auth-Token");

        if (baseUrl.Contains("f5ai"))
            client.DefaultRequestHeaders.Add("X-Auth-Token", apiKey);
        else
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        return (client, apiUrl);
    }

    private static List<object> BuildMessages(string systemPrompt, List<DialogMessage> history)
    {
        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        foreach (var message in history)
            messages.Add(new { role = message.Role, content = message.Content });
        return messages;
    }

    private object BuildChatReplyResponseFormat()
    {
        var baseUrl = _openAiOptions.Value.BaseUrl;

        var replySchema = new
        {
            type = "object",
            properties = new
            {
                reply = new { type = "string", description = "Реплика персонажа без имени в начале" },
                endCall = new { type = "boolean", description = "true, если персонаж кладёт трубку" }
            },
            required = new[] { "reply", "endCall" },
            additionalProperties = false
        };

        if (baseUrl.Contains("f5ai"))
            return new { type = "json_schema", name = "chat_reply", strict = true, schema = replySchema };

        return new { type = "json_schema", json_schema = new { name = "chat_reply", strict = true, schema = replySchema } };
    }

    private async Task<string> CallOpenAiAsync(
        string systemPrompt,
        List<DialogMessage> conversationHistory,
        string model,
        int maxTokens,
        object? responseFormat,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("OpenAI API is not configured");

        var (httpClient, apiUrl) = CreateConfiguredClient();

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = BuildMessages(systemPrompt, conversationHistory),
            ["max_tokens"] = maxTokens,
            ["temperature"] = _openAiOptions.Value.DialogTemperature
        };
        if (responseFormat != null)
            requestBody["response_format"] = responseFormat;

        var httpContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        _logger.LogInformation("Calling OpenAI API with model {Model}", model);

        var response = await httpClient.PostAsync(apiUrl, httpContent, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI API error: {StatusCode} - {Content}", response.StatusCode, responseContent);

            if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                throw new OpenAiPaymentRequiredException("AI service requires payment. Please check your API balance.");

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                throw new OpenAiRateLimitException("AI service rate limit exceeded. Please try again later.");

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new OpenAiAuthenticationException("AI service authentication failed. Please check API configuration.");

            throw new HttpRequestException($"OpenAI API returned {response.StatusCode}: {responseContent}");
        }

        _logger.LogDebug("OpenAI API response: {Response}", responseContent);

        return ExtractContentFromCompletionResponse(responseContent, _logger);
    }

    private static string ExtractContentFromCompletionResponse(string responseContent, ILogger logger)
    {
        using var responseJson = JsonDocument.Parse(responseContent);
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
            return directContent.GetString() ?? string.Empty;

        if (root.TryGetProperty("text", out var textContent))
            return textContent.GetString() ?? string.Empty;

        if (root.TryGetProperty("result", out var result))
        {
            if (result.TryGetProperty("content", out var resultContent))
                return resultContent.GetString() ?? string.Empty;
            if (result.TryGetProperty("text", out var resultText))
                return resultText.GetString() ?? string.Empty;
        }

        logger.LogError("Unable to parse OpenAI response format: {Response}", responseContent);
        throw new InvalidOperationException($"Unexpected API response format: {responseContent}");
    }
}
