using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using SalesTrainer.Api.Features.Dialog;
using SalesTrainer.Api.Infrastructure.Mongo;

namespace SalesTrainer.Api.Features.Voice;

[ApiController]
[Route("dialog/sessions")]
[Authorize]
public class VoiceDialogController : ControllerBase
{
    private readonly IVoiceDialogService _voiceDialogService;
    private readonly IVoicerTtsService _voicerTtsService;
    private readonly IConfiguration _configuration;
    private readonly MongoDbContext _mongoContext;
    private readonly ILogger<VoiceDialogController> _logger;

    public VoiceDialogController(
        IVoiceDialogService voiceDialogService,
        IVoicerTtsService voicerTtsService,
        IConfiguration configuration,
        MongoDbContext mongoContext,
        ILogger<VoiceDialogController> logger)
    {
        _voiceDialogService = voiceDialogService;
        _voicerTtsService = voicerTtsService;
        _configuration = configuration;
        _mongoContext = mongoContext;
        _logger = logger;
    }

    [HttpPost("{sessionId}/voice")]
    public async Task<IActionResult> ProcessVoiceMessage(
        string sessionId,
        [FromBody] VoiceMessageRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!_voicerTtsService.IsConfigured)
        {
            return StatusCode(503, new { error = "Voice service is not configured" });
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Transcript))
        {
            return BadRequest(new { error = "Transcript is required" });
        }

        try
        {
            var audioStream = await _voiceDialogService.ProcessVoiceMessageAsync(
                sessionId, userId, request.Transcript, cancellationToken);

            return File(audioStream, "audio/mpeg");
        }
        catch (VoicerTtsAuthenticationException exception)
        {
            _logger.LogWarning(exception, "Voice TTS authentication failed for session {SessionId}", sessionId);
            return StatusCode(503, new { error = "Voice service authentication failed" });
        }
        catch (VoicerTtsRateLimitException exception)
        {
            _logger.LogWarning(exception, "Voice TTS rate limited for session {SessionId}", sessionId);
            return StatusCode(429, new { error = "Too many voice requests, please wait a moment" });
        }
        catch (VoicerTtsInsufficientFundsException exception)
        {
            _logger.LogWarning(exception, "Voice TTS insufficient funds for session {SessionId}", sessionId);
            return StatusCode(503, new { error = "Voice service unavailable - check account balance" });
        }
        catch (VoicerTtsTimeoutException exception)
        {
            _logger.LogWarning(exception, "Voice TTS timeout for session {SessionId}", sessionId);
            return StatusCode(504, new { error = "Voice service timed out" });
        }
        catch (VoicerTtsException exception)
        {
            _logger.LogWarning(exception, "Voice TTS error for session {SessionId}", sessionId);
            return StatusCode(503, new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Voice message processing failed for session {SessionId}", sessionId);
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("{sessionId}/voice/response")]
    public async Task<IActionResult> GetLastVoiceResponse(string sessionId, CancellationToken cancellationToken)
    {
        if (!_voicerTtsService.IsConfigured)
        {
            return StatusCode(503, new { error = "Voice service is not configured" });
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var filter = Builders<DialogSession>.Filter.And(
            Builders<DialogSession>.Filter.Eq(session => session.Id, sessionId),
            Builders<DialogSession>.Filter.Eq(session => session.UserId, userId)
        );
        var session = await _mongoContext.DialogSessions.Find(filter).FirstOrDefaultAsync(cancellationToken);

        if (session == null)
        {
            return NotFound(new { error = "Session not found" });
        }

        var lastAiMessage = session.Messages.LastOrDefault(message => message.Role == "assistant");
        if (lastAiMessage == null)
        {
            return NotFound(new { error = "No AI response found" });
        }

        return Ok(new VoiceResponseDto
        {
            Content = lastAiMessage.Content,
            IsStopSignal = lastAiMessage.IsStopSignal,
            Timestamp = lastAiMessage.Timestamp
        });
    }
}
