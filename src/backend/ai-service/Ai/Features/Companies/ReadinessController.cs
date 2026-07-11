using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Features.Companies.Models;
using Sellevate.Ai.Features.Companies.Services.Abstract;
using Sellevate.Ai.Features.Evaluation;

namespace Sellevate.Ai.Features.Companies;

[ApiController]
[Route("ai")]
[ServiceFilter(typeof(InternalServiceAuthFilter))]
public sealed class ReadinessController : ControllerBase
{
    // Defense-in-depth cap on the number of session ids a single readiness request may pull
    // feedback for, mirroring PersonaController's/BriefingController's input-size guards.
    private const int MaxSessionIds = 50;

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
        if (sessionIdCount > MaxSessionIds)
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
        catch (InvalidOperationException invalidOperationException)
        {
            _logger.LogWarning(invalidOperationException, "Readiness generation failed");
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
        catch (HttpRequestException httpRequestException)
        {
            _logger.LogWarning(httpRequestException, "AI provider error during readiness generation");
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
    }
}
