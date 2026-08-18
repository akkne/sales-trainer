using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Companies.Models;
using Sellevate.Ai.Features.Companies.Services.Abstract;
using Sellevate.Ai.Features.Evaluation;

namespace Sellevate.Ai.Features.Companies;

/// <summary>
/// Turns a pasted call log into the structured fields company-service stores.
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
public sealed class ParseLogController : ControllerBase
{
    private readonly IParseLogService _parseLogService;
    private readonly ILogger<ParseLogController> _logger;

    public ParseLogController(IParseLogService parseLogService, ILogger<ParseLogController> logger)
    {
        _parseLogService = parseLogService;
        _logger = logger;
    }

    [HttpPost("companies/parse-log")]
    public async Task<IActionResult> ParseLog(
        [FromBody] ParseCallLogRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var rawTextLength = request.RawText?.Length ?? 0;
        if (rawTextLength > AiRequestSizeLimits.MaximumPromptTextCharacters)
        {
            return BadRequest(new { message = "rawText exceeds maximum allowed size." });
        }

        try
        {
            var parsed = await _parseLogService.ParseLogAsync(request.RawText ?? string.Empty, cancellationToken);
            return Ok(parsed);
        }
        catch (OpenAiException openAiException)
        {
            _logger.LogWarning(openAiException, "AI provider error during call-log parsing");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
        catch (InvalidOperationException invalidOperationException)
        {
            _logger.LogWarning(invalidOperationException, "Call log parsing failed");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
        catch (HttpRequestException httpRequestException)
        {
            _logger.LogWarning(httpRequestException, "AI provider error during call log parsing");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
    }
}
