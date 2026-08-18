using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Companies.Models;
using Sellevate.Ai.Features.Companies.Services.Abstract;
using Sellevate.Ai.Features.Evaluation;

namespace Sellevate.Ai.Features.Companies;

/// <summary>
/// Composes a pre-call briefing for one company from its description, the goal, recent calls and past
/// feedback.
///
/// <para>
/// The payload guard is defence in depth on a user-controlled body: it prevents runaway provider cost and
/// latency even when an upstream caller fails to bound its own field sizes. The cap itself lives in
/// <see cref="Sellevate.Ai.Common.Constants.AiRequestSizeLimits"/> so the several routes that apply "the
/// same" bound cannot drift apart.
/// </para>
///
/// <para>
/// Every provider failure is answered as a 503 and never a 500: a rejected request, a spent quota or a bad
/// credential is upstream state, and reporting it as a server fault here would send somebody looking
/// through ai-service logs for a bug that is not in ai-service.
/// </para>
/// </summary>
[ApiController]
[Route("ai")]
[ServiceFilter(typeof(InternalServiceAuthFilter))]
public sealed class BriefingController : ControllerBase
{
    private readonly IBriefingService _briefingService;
    private readonly ILogger<BriefingController> _logger;

    public BriefingController(IBriefingService briefingService, ILogger<BriefingController> logger)
    {
        _briefingService = briefingService;
        _logger = logger;
    }

    [HttpPost("companies/briefing")]
    public async Task<IActionResult> GenerateBriefing(
        [FromBody] GenerateBriefingRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var contextLength =
            (request.CompanyDescription?.Length ?? 0) +
            (request.Goal?.Length ?? 0) +
            (request.RecentCalls?.Sum(call =>
                (call.ContactName?.Length ?? 0) +
                (call.Subject?.Length ?? 0) +
                (call.Outcome?.Length ?? 0)) ?? 0) +
            (request.FeedbackSummaries?.Sum(summary => summary?.Length ?? 0) ?? 0);

        if (contextLength > AiRequestSizeLimits.MaximumSourceMaterialCharacters)
        {
            return BadRequest(new { message = "briefing context exceeds maximum allowed size." });
        }

        try
        {
            var content = await _briefingService.GenerateBriefingAsync(request, cancellationToken);
            return Ok(new BriefingResultDto(content, DateTime.UtcNow));
        }
        catch (OpenAiException openAiException)
        {
            _logger.LogWarning(openAiException, "AI provider error during briefing generation");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
        catch (InvalidOperationException invalidOperationException)
        {
            _logger.LogWarning(invalidOperationException, "Briefing generation failed");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
        catch (HttpRequestException httpRequestException)
        {
            _logger.LogWarning(httpRequestException, "AI provider error during briefing generation");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
    }
}
