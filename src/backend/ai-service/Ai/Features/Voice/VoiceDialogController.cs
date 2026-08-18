using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Voice.Models;
using Sellevate.Ai.Features.Voice.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Voice;

/// <summary>
/// The voice roleplay endpoint: one HTTP request per learner turn, answered with an unbuffered stream
/// of length-prefixed text and audio frames.
///
/// <para>
/// <b>Usage is reserved before the model is touched and refunded in <c>finally</c>.</b> The full
/// per-request cap is taken from the gate up front, the unused remainder is returned once the stream
/// ends, and the refund runs on every exit path including a client disconnect — an abandoned turn
/// that kept its whole reservation would burn a learner's day allowance on seconds nobody spoke.
/// </para>
///
/// <para>
/// <b>Headers are sent before the first chunk, so most failures cannot become a status code.</b>
/// Anything that fails before the stream starts answers properly; anything after ends the body
/// cleanly and is logged, because a torn connection tells the client less than a short reply does.
/// The one exception is a provider timeout, which is logged distinctly from a client cancel so an
/// upstream problem is not filed as normal user behaviour.
/// </para>
/// </summary>
[ApiController]
[Route("dialog/sessions")]
[Authorize]
public sealed class VoiceDialogController : ControllerBase
{
    private const int FrameLengthPrefixBytes = 4;
    private const uint FinalChunkFlag = 1u;
    private const uint StopSignalChunkFlag = 2u;

    private readonly IVoiceDialogService _voiceDialogService;
    private readonly ITtsRouter _ttsRouter;
    private readonly IVoiceUsageService _voiceUsageService;
    private readonly IOptions<VoiceFeatureConfiguration> _voiceFeatureOptions;
    private readonly ILogger<VoiceDialogController> _logger;

    public VoiceDialogController(
        IVoiceDialogService voiceDialogService,
        ITtsRouter ttsRouter,
        IVoiceUsageService voiceUsageService,
        IOptions<VoiceFeatureConfiguration> voiceFeatureOptions,
        ILogger<VoiceDialogController> logger)
    {
        _voiceDialogService = voiceDialogService;
        _ttsRouter = ttsRouter;
        _voiceUsageService = voiceUsageService;
        _voiceFeatureOptions = voiceFeatureOptions;
        _logger = logger;
    }

    [HttpGet("/dialog/voice/usage")]
    public async Task<IActionResult> GetVoiceUsage(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var usage = await _voiceUsageService.GetUsageAsync(userId, cancellationToken);
        return Ok(usage);
    }

    [HttpPost("{sessionId}/voice/stream")]
    public async Task StreamVoiceMessage(
        string sessionId,
        [FromBody] VoiceMessageRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!_ttsRouter.IsConfigured)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await Response.WriteAsJsonAsync(new { error = "Voice service is not configured" }, cancellationToken);
            return;
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Transcript))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { error = "Transcript is required" }, cancellationToken);
            return;
        }

        var maximumRequestSeconds = _voiceFeatureOptions.Value.MaxRecordingSeconds;
        if (maximumRequestSeconds <= 0)
            maximumRequestSeconds = VoiceFeatureConfiguration.DefaultMaxRecordingSeconds;

        int reservedSeconds;
        try
        {
            reservedSeconds = await _voiceUsageService.ReserveSecondsAsync(userId, maximumRequestSeconds, cancellationToken);
        }
        catch (VoiceUsageLimitException exception)
        {
            _logger.LogInformation("Voice stream blocked for user {UserId}: {Period} limit reached", userId, exception.Period);
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await Response.WriteAsJsonAsync(new
            {
                error = $"Voice {exception.Period} limit reached",
                period = exception.Period,
                usedSeconds = exception.UsedSeconds,
                limitSeconds = exception.LimitSeconds,
            }, cancellationToken);
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = AiMediaTypes.OctetStream;
        Response.Headers[HeaderNames.CacheControl] = AiProviderHttpConstants.NoCacheDirective;
        Response.Headers[AiProviderHttpConstants.AccelBufferingHeaderName] = AiProviderHttpConstants.AccelBufferingDisabled;

        using var recordingCapCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        recordingCapCancellation.CancelAfter(TimeSpan.FromSeconds(maximumRequestSeconds));
        var recordingCapToken = recordingCapCancellation.Token;

        var streamStartedAt = DateTime.UtcNow;
        var upstreamTimedOut = false;
        try
        {
            await foreach (var chunk in _voiceDialogService.StreamVoiceMessageAsync(sessionId, userId, request.Transcript, recordingCapToken))
            {
                var flags = (chunk.IsFinal ? FinalChunkFlag : 0u) | (chunk.IsStopSignal ? StopSignalChunkFlag : 0u);
                var textBytes = Encoding.UTF8.GetBytes(chunk.Text);

                await WriteUInt32BigEndianAsync(Response.Body, flags, cancellationToken);
                await WriteUInt32BigEndianAsync(Response.Body, (uint)textBytes.Length, cancellationToken);
                if (textBytes.Length > 0)
                    await Response.Body.WriteAsync(textBytes, cancellationToken);
                await WriteUInt32BigEndianAsync(Response.Body, (uint)chunk.AudioMp3.Length, cancellationToken);
                if (chunk.AudioMp3.Length > 0)
                    await Response.Body.WriteAsync(chunk.AudioMp3, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Voice stream aborted for session {SessionId}", sessionId);

            if (!Response.HasStarted)
            {
                Response.StatusCode = StatusCodes.Status409Conflict;
                await Response.WriteAsJsonAsync(new { error = exception.Message }, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && !recordingCapCancellation.IsCancellationRequested)
        {
            _logger.LogInformation("Voice stream cancelled by client for session {SessionId}", sessionId);
        }
        catch (OperationCanceledException) when (recordingCapCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Voice stream capped at {MaxSeconds}s for session {SessionId}", maximumRequestSeconds, sessionId);
        }
        catch (OperationCanceledException)
        {
            upstreamTimedOut = !cancellationToken.IsCancellationRequested;
            if (upstreamTimedOut)
                _logger.LogWarning("Voice stream upstream timeout for session {SessionId}", sessionId);
            else
                _logger.LogInformation("Voice stream cancelled for session {SessionId}", sessionId);
        }
        catch (HttpRequestException exception) when (exception.InnerException is TaskCanceledException)
        {
            upstreamTimedOut = true;
            _logger.LogWarning(exception, "Voice stream upstream timeout (HttpRequestException) for session {SessionId}", sessionId);
        }
        catch (OpenAiException exception)
        {
            _logger.LogWarning(exception, "Voice stream aborted by AI provider error for session {SessionId}", sessionId);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Voice stream aborted — AI provider unreachable for session {SessionId}", sessionId);
        }
        finally
        {
            var elapsedSeconds = (int)Math.Ceiling((DateTime.UtcNow - streamStartedAt).TotalSeconds);
            await _voiceUsageService.RefundReservationAsync(sessionId, userId, reservedSeconds, elapsedSeconds, CancellationToken.None);
        }

        if (upstreamTimedOut)
        {
            _logger.LogWarning("Upstream LLM/TTS timeout on voice stream for session {SessionId} — client received partial response", sessionId);
        }
    }

    private static async Task WriteUInt32BigEndianAsync(Stream stream, uint value, CancellationToken cancellationToken)
    {
        var buffer = new byte[FrameLengthPrefixBytes];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        await stream.WriteAsync(buffer, cancellationToken);
    }
}
