using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesTrainer.Api.Features.Voice.Models;
using SalesTrainer.Api.Features.Voice.Services.Abstract;

namespace SalesTrainer.Api.Features.Voice;

[ApiController]
[Route("dialog/sessions")]
[Authorize]
public class VoiceDialogController : ControllerBase
{
    private readonly IVoiceDialogService _voiceDialogService;
    private readonly IVoicerTtsService _voicerTtsService;
    private readonly IVoiceUsageService _voiceUsageService;
    private readonly ILogger<VoiceDialogController> _logger;

    public VoiceDialogController(
        IVoiceDialogService voiceDialogService,
        IVoicerTtsService voicerTtsService,
        IVoiceUsageService voiceUsageService,
        ILogger<VoiceDialogController> logger)
    {
        _voiceDialogService = voiceDialogService;
        _voicerTtsService = voicerTtsService;
        _voiceUsageService = voiceUsageService;
        _logger = logger;
    }

    [HttpGet("/dialog/voice/usage")]
    public async Task<IActionResult> GetVoiceUsage(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }
        var usage = await _voiceUsageService.GetUsageAsync(userId, cancellationToken);
        return Ok(usage);
    }

    [HttpPost("{sessionId}/voice/stream")]
    public async Task StreamVoiceMessage(
        string sessionId,
        [FromBody] VoiceMessageRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!_voicerTtsService.IsConfigured)
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

        try
        {
            await _voiceUsageService.EnsureWithinLimitsAsync(userId, cancellationToken);
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

        // Frame format per chunk:
        //   uint32 flags (bit 0 = isFinal, bit 1 = isStopSignal), big-endian
        //   uint32 text byte length, big-endian
        //   text (utf-8)
        //   uint32 audio byte length, big-endian
        //   audio (mp3)
        var streamStartedAt = DateTime.UtcNow;
        try
        {
            await foreach (var chunk in _voiceDialogService.StreamVoiceMessageAsync(sessionId, userId, request.Transcript, cancellationToken))
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
            // Status headers may already be flushed — best-effort error chunk.
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Voice stream cancelled by client for session {SessionId}", sessionId);
        }
        finally
        {
            // Record wall-clock duration of the stream so daily/monthly caps reflect
            // real usage (LLM thinking + TTS + playback download time). Includes
            // barge-in turns — that's intentional, the user still consumed compute.
            var elapsed = (int)Math.Ceiling((DateTime.UtcNow - streamStartedAt).TotalSeconds);
            if (elapsed > 0)
            {
                await _voiceUsageService.RecordSessionSecondsAsync(sessionId, userId, elapsed, CancellationToken.None);
            }
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
