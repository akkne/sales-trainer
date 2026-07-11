using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Features.Companies.Models;
using Sellevate.Ai.Features.Companies.Services.Abstract;
using Sellevate.Ai.Features.Evaluation;

namespace Sellevate.Ai.Features.Companies;

[ApiController]
[Route("ai")]
[ServiceFilter(typeof(InternalServiceAuthFilter))]
public sealed class ParseLogController : ControllerBase
{
    private readonly IParseLogService _parseLogService;
    private readonly ILogger<ParseLogController> _logger;

    public ParseLogController(IParseLogService parseLogService, ILogger<ParseLogController> logger)
    {
        _parseLogService = parseLogService;
        _logger = logger;
    }

    // Defense-in-depth cap on the pasted raw text, mirroring BriefingController's context guard.
    // Prevents runaway LLM cost/latency even if an upstream caller fails to bound input size.
    private const int MaxRawTextLength = 16000;

    [HttpPost("companies/parse-log")]
    public async Task<IActionResult> ParseLog(
        [FromBody] ParseCallLogRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var rawTextLength = request.RawText?.Length ?? 0;
        if (rawTextLength > MaxRawTextLength)
        {
            return BadRequest(new { message = "rawText exceeds maximum allowed size." });
        }

        try
        {
            var parsed = await _parseLogService.ParseLogAsync(request.RawText ?? string.Empty, cancellationToken);
            return Ok(parsed);
        }
        catch (InvalidOperationException invalidOperationException)
        {
            _logger.LogWarning(invalidOperationException, "Call log parsing failed");
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
        catch (HttpRequestException httpRequestException)
        {
            _logger.LogWarning(httpRequestException, "AI provider error during call log parsing");
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
    }
}
