using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Voice.Models;
using Sellevate.Ai.Features.Voice.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Voice;

[ApiController]
[Route("dialog/sessions")]
[Authorize]
public sealed class VoiceDialogController : ControllerBase
{
    private readonly IVoiceDialogService _voiceDialogService;
    private readonly ITtsRouter _ttsRouter;
    private readonly IVoiceUsageService _voiceUsageService;
    private readonly IOptions<VoiceFeatureConfiguration> _voiceConfig;
    private readonly ILogger<VoiceDialogController> _logger;

    public VoiceDialogController(
        IVoiceDialogService voiceDialogService,
        ITtsRouter ttsRouter,
        IVoiceUsageService voiceUsageService,
        IOptions<VoiceFeatureConfiguration> voiceConfig,
        ILogger<VoiceDialogController> logger)
    {
        _voiceDialogService = voiceDialogService;
        _ttsRouter = ttsRouter;
        _voiceUsageService = voiceUsageService;
        _voiceConfig = voiceConfig;
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
            Response.StatusCode = 503;
            await Response.WriteAsJsonAsync(new { error = "Voice service is not configured" }, cancellationToken);
            return;
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            Response.StatusCode = 401;
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Transcript))
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(new { error = "Transcript is required" }, cancellationToken);
            return;
        }

        // AI1: enforce server-side per-request cap before we even touch the gate.
        var maxRequestSeconds = _voiceConfig.Value.MaxRecordingSeconds;
        if (maxRequestSeconds <= 0) maxRequestSeconds = 60; // safety default

        // AI1: atomically reserve usage in Redis; throws VoiceUsageLimitException if over limit.
        int reservedSeconds;
        try
        {
            reservedSeconds = await _voiceUsageService.ReserveSecondsAsync(userId, maxRequestSeconds, cancellationToken);
        }
        catch (VoiceUsageLimitException exception)
        {
            _logger.LogInformation("Voice stream blocked for user {UserId}: {Period} limit reached", userId, exception.Period);
            Response.StatusCode = 429;
            await Response.WriteAsJsonAsync(new
            {
                error = $"Voice {exception.Period} limit reached",
                period = exception.Period,
                usedSeconds = exception.UsedSeconds,
                limitSeconds = exception.LimitSeconds,
            }, cancellationToken);
            return;
        }

        Response.StatusCode = 200;
        Response.ContentType = "application/octet-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        // AI1: cap the stream at MaxRecordingSeconds using a linked CancellationToken.
        using var capCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        capCts.CancelAfter(TimeSpan.FromSeconds(maxRequestSeconds));
        var capToken = capCts.Token;

        var streamStartedAt = DateTime.UtcNow;
        var upstreamTimedOut = false;
        try
        {
            await foreach (var chunk in _voiceDialogService.StreamVoiceMessageAsync(sessionId, userId, request.Transcript, capToken))
            {
                var flags = (chunk.IsFinal ? 1u : 0u) | (chunk.IsStopSignal ? 2u : 0u);
                var textBytes = System.Text.Encoding.UTF8.GetBytes(chunk.Text);

                await WriteUInt32BeAsync(Response.Body, flags, cancellationToken);
                await WriteUInt32BeAsync(Response.Body, (uint)textBytes.Length, cancellationToken);
                if (textBytes.Length > 0)
                    await Response.Body.WriteAsync(textBytes, cancellationToken);
                await WriteUInt32BeAsync(Response.Body, (uint)chunk.AudioMp3.Length, cancellationToken);
                if (chunk.AudioMp3.Length > 0)
                    await Response.Body.WriteAsync(chunk.AudioMp3, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Voice stream aborted for session {SessionId}", sessionId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && !capCts.IsCancellationRequested)
        {
            // AI6: client disconnected — not an error, just stop.
            _logger.LogInformation("Voice stream cancelled by client for session {SessionId}", sessionId);
        }
        catch (OperationCanceledException) when (capCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // AI1: server-side cap fired — stream exceeded MaxRecordingSeconds.
            _logger.LogInformation("Voice stream capped at {MaxSeconds}s for session {SessionId}", maxRequestSeconds, sessionId);
        }
        catch (OperationCanceledException)
        {
            // Both tokens cancelled simultaneously or upstream timeout via Polly.
            upstreamTimedOut = !cancellationToken.IsCancellationRequested;
            if (upstreamTimedOut)
                _logger.LogWarning("Voice stream upstream timeout for session {SessionId}", sessionId);
            else
                _logger.LogInformation("Voice stream cancelled for session {SessionId}", sessionId);
        }
        catch (HttpRequestException ex) when (ex.InnerException is TaskCanceledException)
        {
            // AI6: Polly retry exhausted / upstream read timeout — distinct from client cancel.
            upstreamTimedOut = true;
            _logger.LogWarning(ex, "Voice stream upstream timeout (HttpRequestException) for session {SessionId}", sessionId);
        }
        finally
        {
            var elapsed = (int)Math.Ceiling((DateTime.UtcNow - streamStartedAt).TotalSeconds);
            // AI1: refund the unused portion of the reservation; record only actual usage.
            await _voiceUsageService.RefundReservationAsync(sessionId, userId, reservedSeconds, elapsed, CancellationToken.None);
        }

        // AI6: if the upstream timed out and we haven't written headers yet, return 504.
        // (Headers are already sent for a streaming response, so we can only log.)
        if (upstreamTimedOut)
        {
            _logger.LogWarning("Upstream LLM/TTS timeout on voice stream for session {SessionId} — client received partial response", sessionId);
        }
    }

    private static async Task WriteUInt32BeAsync(Stream stream, uint value, CancellationToken ct)
    {
        var buffer = new byte[4];
        buffer[0] = (byte)((value >> 24) & 0xff);
        buffer[1] = (byte)((value >> 16) & 0xff);
        buffer[2] = (byte)((value >> 8) & 0xff);
        buffer[3] = (byte)(value & 0xff);
        await stream.WriteAsync(buffer, ct);
    }
}
