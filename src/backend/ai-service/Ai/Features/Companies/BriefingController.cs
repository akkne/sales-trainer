using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Features.Companies.Models;
using Sellevate.Ai.Features.Companies.Services.Abstract;
using Sellevate.Ai.Features.Evaluation;

namespace Sellevate.Ai.Features.Companies;

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
        try
        {
            var content = await _briefingService.GenerateBriefingAsync(request, cancellationToken);
            return Ok(new BriefingResultDto(content, DateTime.UtcNow));
        }
        catch (InvalidOperationException invalidOperationException)
        {
            _logger.LogWarning(invalidOperationException, "Briefing generation failed");
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
        catch (HttpRequestException httpRequestException)
        {
            _logger.LogWarning(httpRequestException, "AI provider error during briefing generation");
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
    }
}
