using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Companies.Models;
using Sellevate.Ai.Features.Companies.Services.Abstract;
using Sellevate.Ai.Features.Evaluation;

namespace Sellevate.Ai.Features.Companies;

/// <summary>
/// Scores how ready a seller is for a company, from the dialog sessions they have already held.
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
public sealed class ReadinessController : ControllerBase
{
    private readonly IReadinessService _readinessService;
    private readonly ILogger<ReadinessController> _logger;

    public ReadinessController(IReadinessService readinessService, ILogger<ReadinessController> logger)
    {
        _readinessService = readinessService;
        _logger = logger;
    }

    [HttpPost("companies/readiness")]
    public async Task<IActionResult> GenerateReadiness(
        [FromBody] GenerateReadinessRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var sessionIdCount = request.SessionIds?.Count ?? 0;
        if (sessionIdCount > AiRequestSizeLimits.MaximumSessionIdsPerRequest)
        {
            return BadRequest(new { message = "sessionIds exceeds maximum allowed size." });
        }

        try
        {
            var readiness = await _readinessService.GenerateReadinessAsync(request, cancellationToken);
            if (readiness is null)
                return NoContent();

            return Ok(readiness);
        }
        catch (OpenAiException openAiException)
        {
            _logger.LogWarning(openAiException, "AI provider error during readiness generation");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
        catch (InvalidOperationException invalidOperationException)
        {
            _logger.LogWarning(invalidOperationException, "Readiness generation failed");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
        catch (HttpRequestException httpRequestException)
        {
            _logger.LogWarning(httpRequestException, "AI provider error during readiness generation");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
    }
}
