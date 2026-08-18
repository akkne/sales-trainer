using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Infrastructure.Configuration;

namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Phase 40.33. What is left of learning-service's LLM client: an HTTP call to the service that owns
/// the provider key.
///
/// <para>
/// It replaces a 362-line in-process <c>OpenAiChatService</c> — a monolith-era copy that
/// <c>docs/DONT_FORGET.md</c> has listed since 40.27 as the reason «все LLM-вызовы проходят через
/// одну точку» was untrue. The interface it implements is unchanged, so <c>ExerciseDialogService</c>
/// and its two endpoints did not move: what changed is where the completion is produced, and
/// therefore whether it is counted.
/// </para>
///
/// <para>
/// <b>The exception hierarchy is rebuilt, not abandoned.</b> ai-service names its provider failure in
/// a closed vocabulary and this class turns the name back into the same
/// <see cref="OpenAiException"/> subtype the removed client threw, because
/// <c>ExerciseController</c> and <c>ExerciseDialogService.GenerateAiResponseAsync</c> catch those
/// types and <c>docs/LLM_FAILURE_HANDLING.md</c> pins the mapping. A caller cannot tell that the
/// provider moved a hop away, which is the property that made this refactor safe to do in one commit.
/// </para>
/// </summary>
internal sealed class AiChatClient(
    HttpClient httpClient,
    IOptions<AiServiceConfiguration> configurationOptions,
    ITenantContext tenantContext,
    ILogger<AiChatClient> logger) : IOpenAiChatService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AiServiceConfiguration _configuration = configurationOptions.Value;

    /// <summary>
    /// Always true. Whether a provider key exists is ai-service's fact and asking for it would add a
    /// round trip to every turn; a service with no key answers 503, which
    /// <c>GenerateAiResponseAsync</c> already degrades into its neutral reply. The one behaviour this
    /// changes is that an unconfigured installation now degrades after a call instead of before one.
    /// </summary>
    public bool IsConfigured => true;

    public async Task<ChatMessageResult> SendChatMessageAsync(
        string systemPrompt,
        List<DialogMessage> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(_configuration.ChatPath, systemPrompt, conversationHistory);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await TranslateFailureAsync(response, "chat completion", cancellationToken);
        }

        AiChatCompletionResponse? body;
        try
        {
            body = await response.Content.ReadFromJsonAsync<AiChatCompletionResponse>(SerializerOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // A 200 carrying a proxy's HTML error page. The removed in-process client made the same
            // call about the provider doing this, and for the same reason: an unreadable success is
            // an upstream condition, not a defect here, and must not surface as a 500.
            logger.LogWarning(exception, "AI service returned an unreadable chat completion body");
            throw new OpenAiRequestException("AI provider error");
        }

        if (body is null)
        {
            throw new OpenAiRequestException("AI provider error");
        }

        return new ChatMessageResult
        {
            Content = body.Content,
            IsStopSignal = body.IsStopSignal,
        };
    }

    public async IAsyncEnumerable<string> StreamChatMessageAsync(
        string systemPrompt,
        List<DialogMessage> conversationHistory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(_configuration.ChatStreamPath, systemPrompt, conversationHistory);
        using var response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await TranslateFailureAsync(response, "streaming chat", cancellationToken);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string? delta;
            try
            {
                delta = JsonSerializer.Deserialize<AiChatDelta>(line, SerializerOptions)?.D;
            }
            catch (JsonException)
            {
                // A malformed frame is one lost delta, not a lost turn. The removed client made the
                // same call about a malformed SSE payload.
                logger.LogDebug("Skipping unparseable chat stream frame");
                continue;
            }

            if (!string.IsNullOrEmpty(delta))
            {
                yield return delta;
            }
        }
    }

    private HttpRequestMessage BuildRequest(string path, string systemPrompt, List<DialogMessage> history)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            _configuration.BaseUrl.TrimEnd('/') + path)
        {
            Content = JsonContent.Create(
                new AiChatCompletionRequest(
                    systemPrompt,
                    history.Select(message => new AiChatMessage(message.Role, message.Content)).ToList()),
                options: SerializerOptions),
        };

        // A learner is mid-conversation, so this runs to the organization's full allowance rather
        // than stopping at the batch reserve.
        AiCallHeaders.Apply(request, tenantContext, AiCallHeaders.InteractiveWorkload);

        return request;
    }

    /// <summary>
    /// Rebuilds the exception the in-process client used to throw, from the failure ai-service named.
    /// A response without a recognised code — a proxy error page, a 502 from something between the
    /// two services — degrades to <see cref="OpenAiRequestException"/> carrying the status, which is
    /// what the old client did with an unparseable provider body.
    /// </summary>
    private async Task<Exception> TranslateFailureAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        AiChatFailure? failure = null;
        try
        {
            failure = await response.Content.ReadFromJsonAsync<AiChatFailure>(SerializerOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // Not our shape — treat as an opaque upstream failure below.
        }

        if ((int)response.StatusCode >= 500)
            logger.LogError("AI service failed on {Operation}: {StatusCode} ({Code})", operation, response.StatusCode, failure?.Code);
        else
            logger.LogWarning("AI service rejected {Operation}: {StatusCode} ({Code})", operation, response.StatusCode, failure?.Code);

        return failure?.Code switch
        {
            "payment_required" => new OpenAiPaymentRequiredException("AI service requires payment. Please check your API balance."),
            "rate_limited" => new OpenAiRateLimitException("AI service rate limit exceeded. Please try again later."),
            "provider_auth" => new OpenAiAuthenticationException("AI service authentication failed. Please check API configuration."),
            "provider_rejected" or "provider_failed" or "provider_unreachable" =>
                new OpenAiRequestException("AI provider error", failure.UpstreamStatusCode ?? (int)response.StatusCode),
            // Phase 40.33. The organization's own allowance, not the provider's. Surfaced as a rate
            // limit so the exercise chat degrades into its neutral reply instead of a 500 — the
            // learner sees a conversation that goes quiet rather than a broken screen, and the reason
            // is in the log and on the spend report.
            _ when response.StatusCode == HttpStatusCode.TooManyRequests =>
                new OpenAiRateLimitException("Organization AI quota reached."),
            _ => new OpenAiRequestException("AI provider error", (int)response.StatusCode),
        };
    }

    private sealed record AiChatMessage(string Role, string Content);

    private sealed record AiChatCompletionRequest(string SystemPrompt, List<AiChatMessage> Messages);

    private sealed record AiChatCompletionResponse(string Content, bool IsStopSignal);

    private sealed record AiChatDelta(string? D);

    private sealed record AiChatFailure(string? Code, int? UpstreamStatusCode);
}
