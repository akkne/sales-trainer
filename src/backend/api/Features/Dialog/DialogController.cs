using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Gamification;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Dialog;

[ApiController]
[Route("dialog")]
[Authorize]
public class DialogController : ControllerBase
{
    private readonly DialogService _dialogService;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DialogController> _logger;

    public DialogController(DialogService dialogService, AppDbContext dbContext, ILogger<DialogController> logger)
    {
        _dialogService = dialogService;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("bundles")]
    public async Task<IActionResult> GetBundles()
    {
        if (!_dialogService.IsOpenAiConfigured)
        {
            return Ok(Array.Empty<DialogBundleDto>());
        }

        var bundles = await _dialogService.GetActiveBundlesAsync();
        var bundleDtos = bundles.Select(DialogBundleDto.FromEntity).ToList();
        return Ok(bundleDtos);
    }

    [HttpGet("bundles/{bundleId:guid}/modes")]
    public async Task<IActionResult> GetModesForBundle(Guid bundleId)
    {
        if (!_dialogService.IsOpenAiConfigured)
        {
            return Ok(Array.Empty<DialogModeDto>());
        }

        var bundle = await _dialogService.GetBundleByIdAsync(bundleId);
        if (bundle == null)
        {
            return NotFound(new { message = "Bundle not found" });
        }

        var modes = await _dialogService.GetActiveModesForBundleAsync(bundleId);
        var modeDtos = modes.Select(DialogModeDto.FromEntity).ToList();
        return Ok(modeDtos);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetUserSessions()
    {
        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            return Unauthorized();
        }

        var sessions = await _dialogService.GetUserSessionsAsync(userId.Value);

        var bundleIds = sessions.Select(s => s.BundleId).Distinct().ToList();
        var modeIds = sessions.Select(s => s.ModeId).Distinct().ToList();

        var bundles = await _dbContext.DialogBundles
            .Where(b => bundleIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Title);

        var modes = await _dbContext.DialogModes
            .Where(m => modeIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Title);

        var sessionDtos = sessions.Select(session =>
            DialogSessionSummaryDto.FromEntity(
                session,
                bundles.GetValueOrDefault(session.BundleId, ""),
                modes.GetValueOrDefault(session.ModeId, "")
            )
        ).ToList();

        return Ok(sessionDtos);
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> StartSession([FromBody] StartSessionRequestDto request)
    {
        if (!_dialogService.IsOpenAiConfigured)
        {
            return StatusCode(503, new { message = "AI service is not configured" });
        }

        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var session = await _dialogService.StartSessionAsync(userId.Value, request.BundleId, request.ModeId);
            return Ok(DialogSessionDto.FromEntity(session));
        }
        catch (OpenAiPaymentRequiredException)
        {
            return StatusCode(402, new { message = "AI service requires payment. Please check your API balance." });
        }
        catch (OpenAiRateLimitException)
        {
            return StatusCode(429, new { message = "AI service rate limit exceeded. Please try again later." });
        }
        catch (OpenAiAuthException)
        {
            return StatusCode(503, new { message = "AI service authentication failed." });
        }
        catch (InvalidOperationException invalidOperationException)
        {
            return BadRequest(new { message = invalidOperationException.Message });
        }
    }

    [HttpGet("sessions/{sessionId}")]
    public async Task<IActionResult> GetSession(string sessionId)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            return Unauthorized();
        }

        var session = await _dialogService.GetSessionForUserAsync(sessionId, userId.Value);
        if (session == null)
        {
            return NotFound(new { message = "Session not found" });
        }

        return Ok(DialogSessionDto.FromEntity(session));
    }

    [HttpPost("sessions/{sessionId}/messages")]
    public async Task<IActionResult> SendMessage(string sessionId, [FromBody] SendMessageRequestDto request)
    {
        if (!_dialogService.IsOpenAiConfigured)
        {
            return StatusCode(503, new { message = "AI service is not configured" });
        }

        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var aiMessage = await _dialogService.SendMessageAsync(sessionId, userId.Value, request.Content);
            return Ok(DialogMessageDto.FromEntity(aiMessage));
        }
        catch (OpenAiPaymentRequiredException)
        {
            return StatusCode(402, new { message = "AI service requires payment. Please check your API balance." });
        }
        catch (OpenAiRateLimitException)
        {
            return StatusCode(429, new { message = "AI service rate limit exceeded. Please try again later." });
        }
        catch (OpenAiAuthException)
        {
            return StatusCode(503, new { message = "AI service authentication failed." });
        }
        catch (InvalidOperationException invalidOperationException)
        {
            return BadRequest(new { message = invalidOperationException.Message });
        }
    }

    [HttpPost("sessions/{sessionId}/complete")]
    public async Task<IActionResult> CompleteSession(string sessionId)
    {
        if (!_dialogService.IsOpenAiConfigured)
        {
            return StatusCode(503, new { message = "AI service is not configured" });
        }

        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _dialogService.CompleteSessionAsync(sessionId, userId.Value);

            if (result.XpEarned > 0)
            {
                var userXp = new UserXp
                {
                    UserId = userId.Value,
                    Amount = result.XpEarned,
                    Source = "dialog",
                    EarnedAt = DateTime.UtcNow
                };
                _dbContext.UserXpRecords.Add(userXp);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Awarded {Xp} XP to user {UserId} for dialog session {SessionId}",
                    result.XpEarned, userId.Value, sessionId);
            }

            return Ok(new
            {
                summary = result.Feedback.Summary,
                content = result.Feedback.Content,
                generatedAt = result.Feedback.GeneratedAt,
                xpEarned = result.XpEarned
            });
        }
        catch (OpenAiPaymentRequiredException)
        {
            return StatusCode(402, new { message = "AI service requires payment. Please check your API balance." });
        }
        catch (OpenAiRateLimitException)
        {
            return StatusCode(429, new { message = "AI service rate limit exceeded. Please try again later." });
        }
        catch (OpenAiAuthException)
        {
            return StatusCode(503, new { message = "AI service authentication failed." });
        }
        catch (InvalidOperationException invalidOperationException)
        {
            return BadRequest(new { message = invalidOperationException.Message });
        }
    }

    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(string sessionId)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            return Unauthorized();
        }

        var deleted = await _dialogService.DeleteSessionAsync(sessionId, userId.Value);
        if (!deleted)
        {
            return NotFound(new { message = "Session not found" });
        }

        return NoContent();
    }

    private Guid? GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }
}
