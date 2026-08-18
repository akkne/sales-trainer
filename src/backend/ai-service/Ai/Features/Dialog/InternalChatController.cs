using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Features.Evaluation;
using Sellevate.Ai.Features.Voice.Services.Abstract;

namespace Sellevate.Ai.Features.Dialog;

/// <summary>
/// Phase 40.33. The generic chat and speech calls ai-service owes the rest of the platform, and the
/// reason learning-service no longer carries a provider client of its own.
///
/// <para>
/// <b>Why this exists at all.</b> <c>docs/DONT_FORGET.md</c> has carried an item since 40.27:
/// learning-service kept a copy of <c>OpenAiChatService</c> and a copy of <c>YandexTtsService</c>,
/// used by <c>ExerciseDialogService</c> for <c>ai_dialogue</c> exercises — a monolith-era leftover
/// that 40.27 deliberately did not extend. While those copies lived, «все LLM-вызовы проходят через
/// одну точку» was false, and a quota enforced at that point was a quota with a door beside it: the
/// interactive exercise dialogue and every sentence of its synthesized speech were unmetered, and
/// nothing in the code said so. This block closes the door by moving the calls rather than by
/// duplicating the meter, because a meter duplicated is a meter that will disagree with itself.
/// </para>
///
/// <para>
/// <b>The orchestration stays where it was.</b> learning-service still holds the exercise row, the
/// turn cap, the Redis transcript and the sentence chunking — all of which need learning-db and none
/// of which talk to a provider. What crosses the wire is the completion and the synthesis, which is
/// exactly the split <c>POST /ai/evaluate</c> has used since the extraction: "the system prompt text
/// is passed **into** the call by the caller; the AI service performs only the LLM call".
/// </para>
///
/// <para>
/// <b>Provider failures keep their status codes.</b> The contract in
/// <c>docs/LLM_FAILURE_HANDLING.md</c> is a per-service exception hierarchy mapped onto HTTP; here
/// the mapping travels one hop further, so the caller can rebuild the same exception it used to throw
/// itself. The body names the failure in a closed vocabulary rather than echoing the provider — the
/// raw body is redacted and dropped inside <c>OpenAiChatService</c> and must not start travelling
/// again on the way out.
/// </para>
///
/// <para>
/// Internal and un-gatewayed, like <c>/ai/evaluate</c> and <c>/ai/content/*</c>. The organization
/// arrives in <c>X-Organization-Id</c>, which is what makes these calls meterable at all.
/// </para>
/// </summary>
[ApiController]
[Route("ai/chat")]
[ServiceFilter(typeof(InternalServiceAuthFilter))]
public sealed class InternalChatController(
    IOpenAiChatService chatService,
    ITtsRouter ttsRouter,
    ILogger<InternalChatController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Complete(
        [FromBody] InternalChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var result = await chatService.SendChatMessageAsync(
                request.SystemPrompt ?? string.Empty,
                ToDialogHistory(request.Messages),
                cancellationToken);

            return Ok(new InternalChatCompletionResponse(result.Content, result.IsStopSignal));
        }
        catch (OpenAiException exception)
        {
            return TranslateProviderFailure(exception);
        }
    }

    /// <summary>
    /// Newline-delimited JSON, one <c>{"d":"…"}</c> object per content delta.
    ///
    /// <para>
    /// NDJSON rather than re-emitting SSE: the caller is a service, not a browser, and SSE's framing
    /// buys event names and reconnection ids that nothing here uses while costing a second parser
    /// alongside the one <c>OpenAiChatService</c> already runs against the provider. A line is a
    /// delta; end of stream is end of body.
    /// </para>
    ///
    /// <para>
    /// A provider failure after the first byte cannot become a status code, so the stream ends
    /// cleanly and the caller gets a short reply — the same rule both voice controllers already
    /// follow.
    /// </para>
    ///
    /// <para>
    /// A cancelled enumeration is swallowed on purpose: the caller went away, which is not a failure, and
    /// the stream is never closed with a fabricated reply to make it look complete.
    /// </para>
    /// </summary>
    [HttpPost("stream")]
    public async Task StreamComplete(
        [FromBody] InternalChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var history = ToDialogHistory(request.Messages);
        var systemPrompt = request.SystemPrompt ?? string.Empty;

        Response.ContentType = AiMediaTypes.NewlineDelimitedJson;
        Response.Headers[HeaderNames.CacheControl] = AiProviderHttpConstants.NoCacheDirective;
        Response.Headers[AiProviderHttpConstants.AccelBufferingHeaderName] = AiProviderHttpConstants.AccelBufferingDisabled;

        try
        {
            await foreach (var delta in chatService.StreamChatMessageAsync(systemPrompt, history, cancellationToken))
            {
                var line = JsonSerializer.Serialize(new InternalChatDelta(delta)) + "\n";
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(line), cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is OpenAiException or HttpRequestException)
        {
            if (!Response.HasStarted)
            {
                var failure = TranslateProviderFailure(exception);
                Response.StatusCode = failure.StatusCode ?? StatusCodes.Status503ServiceUnavailable;
                await Response.WriteAsJsonAsync(failure.Value, CancellationToken.None);
                return;
            }

            logger.LogWarning(exception, "Internal chat stream ended early on a provider failure");
        }
    }

    /// <summary>
    /// Synthesis for a caller that owns the text but not the provider. Goes through the same
    /// <c>CachingTtsRouter</c> the roleplay uses, so a phrase an exercise and a dialog both produce
    /// is synthesized once for the whole installation instead of once per service.
    /// </summary>
    [HttpPost("/ai/tts")]
    public async Task<IActionResult> Synthesize(
        [FromBody] InternalSynthesisRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { error = "Text is required." });
        }

        if (!ttsRouter.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "No TTS provider is configured." });
        }

        await using var audio = await ttsRouter.SynthesizeSpeechAsync(request.Text, request.VoiceId, cancellationToken);
        using var buffer = new MemoryStream();
        await audio.CopyToAsync(buffer, cancellationToken);

        return File(buffer.ToArray(), AiMediaTypes.WavAudio);
    }

    private static List<DialogMessage> ToDialogHistory(IEnumerable<InternalChatMessage>? messages) =>
        (messages ?? [])
        .Select(message => new DialogMessage
        {
            Role = message.Role,
            Content = message.Content,
            Timestamp = DateTime.UtcNow,
        })
        .ToList();

    /// <summary>
    /// The mapping table from <c>docs/LLM_FAILURE_HANDLING.md</c>, carried one hop. <c>code</c> is a
    /// closed vocabulary so the caller rebuilds the right exception type rather than guessing from a
    /// status code that several failures share.
    /// </summary>
    private ObjectResult TranslateProviderFailure(Exception exception)
    {
        var (statusCode, code) = exception switch
        {
            OpenAiPaymentRequiredException => (StatusCodes.Status402PaymentRequired, "payment_required"),
            OpenAiRateLimitException => (StatusCodes.Status429TooManyRequests, "rate_limited"),
            OpenAiAuthenticationException => (StatusCodes.Status503ServiceUnavailable, "provider_auth"),
            OpenAiRequestException requestException => (
                StatusCodes.Status503ServiceUnavailable,
                requestException.StatusCode is >= 500 ? "provider_failed" : "provider_rejected"),
            _ => (StatusCodes.Status503ServiceUnavailable, "provider_unreachable"),
        };

        var upstreamStatusCode = (exception as OpenAiRequestException)?.StatusCode;

        return StatusCode(statusCode, new InternalChatFailure(code, upstreamStatusCode));
    }
}

public sealed record InternalChatMessage(string Role, string Content);

public sealed record InternalChatCompletionRequest(string? SystemPrompt, List<InternalChatMessage>? Messages);

public sealed record InternalChatCompletionResponse(string Content, bool IsStopSignal);

public sealed record InternalChatDelta(string D);

public sealed record InternalSynthesisRequest(string Text, string? VoiceId);

/// <summary>A provider failure, named. Never carries the provider's own body — see <c>RedactAndTruncate</c>.</summary>
public sealed record InternalChatFailure(string Code, int? UpstreamStatusCode);
