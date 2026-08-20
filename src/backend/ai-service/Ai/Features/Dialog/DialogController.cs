using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Features.Dialog.Constants;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.Ai.Infrastructure.Learning;

namespace Sellevate.Ai.Features.Dialog;

/// <summary>
/// The learner-facing dialog surface: browse bundles and modes, start a conversation, exchange turns,
/// complete it for feedback.
///
/// <para>
/// <b>Every provider failure is answered with a status code, never an unhandled exception.</b> Before
/// that mapping existed a provider 400 escaped as a 500 and was logged as a defect in this service.
/// The mapping lives in one place — see <see cref="ExecuteWithProviderFailureHandlingAsync"/> — because
/// it was previously repeated per action and had already begun to disagree with itself.
/// </para>
///
/// <para>
/// <b>An unconfigured provider is a shape, not an error.</b> The list endpoints answer empty and the
/// write endpoints answer 503, so a deployment with no key renders a working page with nothing to
/// practise rather than a broken one.
/// </para>
///
/// <para>
/// The caller's identity comes from the token and never from the route or body, so no action can be
/// aimed at another learner's session.
/// </para>
/// </summary>
[ApiController]
[Route("dialog")]
[Authorize]
public sealed class DialogController : ControllerBase
{
    private readonly IDialogService _dialogService;
    private readonly IScenarioValidationService _scenarioValidationService;
    private readonly AiDbContext _databaseContext;
    private readonly ISkillLookupClient _skillLookupClient;
    private readonly ILogger<DialogController> _logger;

    public DialogController(
        IDialogService dialogService,
        IScenarioValidationService scenarioValidationService,
        AiDbContext databaseContext,
        ISkillLookupClient skillLookupClient,
        ILogger<DialogController> logger)
    {
        _dialogService = dialogService;
        _scenarioValidationService = scenarioValidationService;
        _databaseContext = databaseContext;
        _skillLookupClient = skillLookupClient;
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
        var skillLookup = await _skillLookupClient.GetSkillSummariesAsync(cancellationToken);
        var bundleDtos = bundles.Select(bundle => DialogBundleDto.FromEntity(bundle, skillLookup)).ToList();
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
            return NotFound(new { message = DialogMessages.BundleNotFound });
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
            return NotFound(new { message = DialogMessages.CompanyCallModeNotSeeded });
        }

        return Ok(new CompanyCallModeIdentifierDto
        {
            BundleId = mode.BundleId,
            ModeId = mode.Id
        });
    }

    [HttpGet("custom-scenario-mode")]
    public async Task<IActionResult> GetCustomScenarioMode(CancellationToken cancellationToken = default)
    {
        var mode = await _dialogService.GetCustomScenarioModeAsync(cancellationToken);
        if (mode == null)
        {
            return NotFound(new { message = DialogMessages.CustomScenarioModeNotSeeded });
        }

        return Ok(new DialogModeIdentifierDto
        {
            BundleId = mode.BundleId,
            ModeId = mode.Id
        });
    }

    /// <summary>
    /// Pre-flight sales-relevance check for a user-authored scenario, so the compose dialog can
    /// reject off-topic text before the user is dropped into a conversation. Advisory only —
    /// POST /dialog/sessions re-checks server-side.
    /// </summary>
    [HttpPost("scenario/validate")]
    public async Task<IActionResult> ValidateScenario(
        [FromBody] ValidateScenarioRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_dialogService.IsOpenAiConfigured)
        {
            return ServiceNotConfigured();
        }

        if (GetUserIdFromClaims() == null)
        {
            return Unauthorized();
        }

        try
        {
            var verdict = await _scenarioValidationService.ValidateAsync(request.Scenario ?? "", cancellationToken);
            return Ok(new ValidateScenarioResponseDto
            {
                IsValid = verdict.IsValid,
                RejectionReason = verdict.RejectionReason
            });
        }
        catch (ScenarioValidationUnavailableException unavailableException)
        {
            return ScenarioCheckUnavailable(unavailableException);
        }
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
            return ServiceNotConfigured();
        }

        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            return Unauthorized();
        }

        return await ExecuteWithProviderFailureHandlingAsync(async () =>
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

            var customScenarioContext = string.IsNullOrWhiteSpace(request.CustomScenario)
                ? null
                : new CustomScenarioContext { Scenario = request.CustomScenario };

            var session = await _dialogService.StartSessionAsync(
                userId.Value, request.BundleId, request.ModeId, companyCallContext, customScenarioContext, cancellationToken);
            return Ok(DialogSessionDto.FromEntity(session));
        },
        additionalHandler: exception => exception switch
        {
            ScenarioRejectedException rejected => UnprocessableEntity(new { message = rejected.Message }),
            ScenarioValidationUnavailableException unavailable => ScenarioCheckUnavailable(unavailable),
            _ => null,
        });
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
            return NotFound(new { message = DialogMessages.SessionNotFound });
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
            return ServiceNotConfigured();
        }

        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            return Unauthorized();
        }

        return await ExecuteWithProviderFailureHandlingAsync(async () =>
        {
            var aiMessage = await _dialogService.SendMessageAsync(sessionId, userId.Value, request.Content, cancellationToken);
            return Ok(DialogMessageDto.FromEntity(aiMessage));
        });
    }

    [HttpPost("sessions/{sessionId}/complete")]
    public async Task<IActionResult> CompleteSession(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!_dialogService.IsOpenAiConfigured)
        {
            return ServiceNotConfigured();
        }

        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            return Unauthorized();
        }

        return await ExecuteWithProviderFailureHandlingAsync(async () =>
        {
            var result = await _dialogService.CompleteSessionAsync(sessionId, userId.Value, cancellationToken);

            if (result == null)
            {
                return NoContent();
            }

            return Ok(new
            {
                summary = result.Feedback.Summary,
                content = result.Feedback.Content,
                score = result.Feedback.Score,
                generatedAt = result.Feedback.GeneratedAt,
                xpEarned = result.XpEarned
            });
        });
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
            return NotFound(new { message = DialogMessages.SessionNotFound });
        }

        return NoContent();
    }

    private ObjectResult ServiceNotConfigured()
        => StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new { message = AiProviderFailureMessages.NotConfigured });

    private ObjectResult ScenarioCheckUnavailable(Exception unavailableException)
    {
        _logger.LogWarning(unavailableException, "Scenario relevance check is unavailable");
        return StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new { message = DialogMessages.ScenarioCheckUnavailable });
    }

    /// <summary>
    /// Runs <paramref name="action"/> and maps a provider failure onto the status code that failure
    /// means, per docs/LLM_FAILURE_HANDLING.md. The mapping is here rather than repeated per action
    /// because the copies had already started to differ, and an inconsistent status code for one
    /// upstream failure is a client that retries the wrong things.
    ///
    /// <para>
    /// <paramref name="additionalHandler"/> is consulted first, for failures only one action can raise.
    /// Returning <see langword="null"/> from it falls through to the shared mapping.
    /// </para>
    ///
    /// <para>
    /// <see cref="InvalidOperationException"/> is mapped to 400 deliberately: the service raises it for
    /// caller mistakes — an unknown mode, a finished session, a context sent with the wrong mode.
    /// </para>
    /// </summary>
    private async Task<IActionResult> ExecuteWithProviderFailureHandlingAsync(
        Func<Task<IActionResult>> action,
        Func<Exception, IActionResult?>? additionalHandler = null)
    {
        try
        {
            return await action();
        }
        catch (Exception exception)
        {
            var handled = additionalHandler?.Invoke(exception);
            if (handled is not null)
            {
                return handled;
            }

            switch (exception)
            {
                case OpenAiPaymentRequiredException:
                    return StatusCode(
                        StatusCodes.Status402PaymentRequired,
                        new { message = AiProviderFailureMessages.PaymentRequired });

                case OpenAiRateLimitException:
                    return StatusCode(
                        StatusCodes.Status429TooManyRequests,
                        new { message = AiProviderFailureMessages.RateLimited });

                case OpenAiAuthenticationException:
                    return StatusCode(
                        StatusCodes.Status503ServiceUnavailable,
                        new { message = AiProviderFailureMessages.AuthenticationFailed });

                case OpenAiRequestException openAiRequestException:
                    _logger.LogWarning(
                        openAiRequestException,
                        "AI provider request failed (upstream status {UpstreamStatus})",
                        openAiRequestException.StatusCode);
                    return StatusCode(
                        StatusCodes.Status502BadGateway,
                        new { message = AiProviderFailureMessages.TemporarilyUnavailable });

                case HttpRequestException httpRequestException:
                    _logger.LogWarning(httpRequestException, "AI provider is unreachable");
                    return StatusCode(
                        StatusCodes.Status503ServiceUnavailable,
                        new { message = AiProviderFailureMessages.TemporarilyUnavailable });

                case InvalidOperationException invalidOperationException:
                    return BadRequest(new { message = invalidOperationException.Message });

                default:
                    throw;
            }
        }
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
