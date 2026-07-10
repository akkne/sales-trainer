using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Infrastructure.Data;

namespace Sellevate.Ai.Features.Dialog;

[ApiController]
[Route("dialog")]
[Authorize]
public sealed class DialogController : ControllerBase
{
    private readonly IDialogService _dialogService;
    private readonly AiDbContext _databaseContext;
    private readonly ILogger<DialogController> _logger;

    public DialogController(IDialogService dialogService, AiDbContext databaseContext, ILogger<DialogController> logger)
    {
        _dialogService = dialogService;
        _databaseContext = databaseContext;
        _logger = logger;
    }

    [HttpGet("bundles")]
    public async Task<IActionResult> GetBundles(CancellationToken cancellationToken = default)
    {
        if (!_dialogService.IsOpenAiConfigured)
        {
            return Ok(Array.Empty<DialogBundleDto>());
        }

        var bundles = await _dialogService.GetActiveBundlesAsync(cancellationToken);
        var bundleDtos = bundles.Select(DialogBundleDto.FromEntity).ToList();
        return Ok(bundleDtos);
    }

    [HttpGet("bundles/{bundleId:guid}/modes")]
    public async Task<IActionResult> GetModesForBundle(Guid bundleId, CancellationToken cancellationToken = default)
    {
        if (!_dialogService.IsOpenAiConfigured)
        {
            return Ok(Array.Empty<DialogModeDto>());
        }

        var bundle = await _dialogService.GetBundleByIdAsync(bundleId, cancellationToken);
        if (bundle == null)
        {
            return NotFound(new { message = "Bundle not found" });
        }

        var modes = await _dialogService.GetActiveModesForBundleAsync(bundleId, cancellationToken);
        var modeDtos = modes.Select(DialogModeDto.FromEntity).ToList();
        return Ok(modeDtos);
    }

    [HttpGet("company-call-mode")]
    public async Task<IActionResult> GetCompanyCallMode(CancellationToken cancellationToken = default)
    {
        var mode = await _dialogService.GetCompanyCallModeAsync(cancellationToken);
        if (mode == null)
        {
            return NotFound(new { message = "Company-call mode is not seeded yet." });
        }

        return Ok(new CompanyCallModeIdentifierDto
        {
            BundleId = mode.BundleId,
            ModeId = mode.Id
        });
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetUserSessions(CancellationToken cancellationToken = default)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            return Unauthorized();
        }

        var sessions = await _dialogService.GetUserSessionsAsync(userId.Value, cancellationToken);

        var bundleIds = sessions.Select(session => session.BundleId).Distinct().ToList();
        var modeIds = sessions.Select(session => session.ModeId).Distinct().ToList();

        var bundles = await _databaseContext.DialogBundles
            .Where(bundle => bundleIds.Contains(bundle.Id))
            .ToDictionaryAsync(bundle => bundle.Id, bundle => bundle.Title, cancellationToken);

        var modes = await _databaseContext.DialogModes
            .Where(mode => modeIds.Contains(mode.Id))
            .ToDictionaryAsync(mode => mode.Id, mode => mode.Title, cancellationToken);

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
    public async Task<IActionResult> StartSession(
        [FromBody] StartSessionRequestDto request,
        CancellationToken cancellationToken = default)
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
            CompanyCallContext? companyCallContext = null;
            if (request.CompanyContext != null)
            {
                companyCallContext = new CompanyCallContext
                {
                    CompanyName = request.CompanyContext.CompanyName,
                    CompanyDescription = request.CompanyContext.CompanyDescription,
                    CallGoal = request.CompanyContext.CallGoal,
                    PersonaName = request.CompanyContext.PersonaName,
                    PersonaPosition = request.CompanyContext.PersonaPosition,
                    PersonaPersonality = request.CompanyContext.PersonaPersonality,
                    PersonaDifficulty = request.CompanyContext.PersonaDifficulty
                };
            }

            var session = await _dialogService.StartSessionAsync(userId.Value, request.BundleId, request.ModeId, companyCallContext, cancellationToken);
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
        catch (OpenAiAuthenticationException)
        {
            return StatusCode(503, new { message = "AI service authentication failed." });
        }
        catch (InvalidOperationException invalidOperationException)
        {
            return BadRequest(new { message = invalidOperationException.Message });
        }
    }

    [HttpGet("sessions/{sessionId}")]
    public async Task<IActionResult> GetSession(string sessionId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            return Unauthorized();
        }

        var session = await _dialogService.GetSessionForUserAsync(sessionId, userId.Value, cancellationToken);
        if (session == null)
        {
            return NotFound(new { message = "Session not found" });
        }

        return Ok(DialogSessionDto.FromEntity(session));
    }

    [HttpPost("sessions/{sessionId}/messages")]
    public async Task<IActionResult> SendMessage(
        string sessionId,
        [FromBody] SendMessageRequestDto request,
        CancellationToken cancellationToken = default)
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
            var aiMessage = await _dialogService.SendMessageAsync(sessionId, userId.Value, request.Content, cancellationToken);
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
        catch (OpenAiAuthenticationException)
        {
            return StatusCode(503, new { message = "AI service authentication failed." });
        }
        catch (InvalidOperationException invalidOperationException)
        {
            return BadRequest(new { message = invalidOperationException.Message });
        }
    }

    [HttpPost("sessions/{sessionId}/complete")]
    public async Task<IActionResult> CompleteSession(string sessionId, CancellationToken cancellationToken = default)
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
            var result = await _dialogService.CompleteSessionAsync(sessionId, userId.Value, cancellationToken);

            if (result == null)
            {
                // Empty call — session abandoned, no feedback to show, no XP.
                return NoContent();
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
        catch (OpenAiAuthenticationException)
        {
            return StatusCode(503, new { message = "AI service authentication failed." });
        }
        catch (InvalidOperationException invalidOperationException)
        {
            return BadRequest(new { message = invalidOperationException.Message });
        }
    }

    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(string sessionId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            return Unauthorized();
        }

        var deleted = await _dialogService.DeleteSessionAsync(sessionId, userId.Value, cancellationToken);
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
