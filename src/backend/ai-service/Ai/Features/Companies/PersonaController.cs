using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Companies.Models;
using Sellevate.Ai.Features.Companies.Services.Abstract;
using Sellevate.Ai.Features.Evaluation;

namespace Sellevate.Ai.Features.Companies;

/// <summary>
/// Generates a roleplay persona for one company from its description.
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
public sealed class PersonaController : ControllerBase
{
    private readonly IPersonaService _personaService;
    private readonly ILogger<PersonaController> _logger;

    public PersonaController(IPersonaService personaService, ILogger<PersonaController> logger)
    {
        _personaService = personaService;
        _logger = logger;
    }

    [HttpPost("companies/persona")]
    public async Task<IActionResult> GeneratePersona(
        [FromBody] GeneratePersonaRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var companyDescriptionLength = request.CompanyDescription?.Length ?? 0;
        if (companyDescriptionLength > AiRequestSizeLimits.MaximumPromptTextCharacters)
        {
            return BadRequest(new { message = "companyDescription exceeds maximum allowed size." });
        }

        try
        {
            var persona = await _personaService.GeneratePersonaAsync(request, cancellationToken);
            return Ok(persona);
        }
        catch (OpenAiException openAiException)
        {
            _logger.LogWarning(openAiException, "AI provider error during persona generation");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
        catch (InvalidOperationException invalidOperationException)
        {
            _logger.LogWarning(invalidOperationException, "Persona generation failed");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
        catch (HttpRequestException httpRequestException)
        {
            _logger.LogWarning(httpRequestException, "AI provider error during persona generation");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
    }
}
