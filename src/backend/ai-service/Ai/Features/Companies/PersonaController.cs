using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Features.Companies.Models;
using Sellevate.Ai.Features.Companies.Services.Abstract;
using Sellevate.Ai.Features.Evaluation;

namespace Sellevate.Ai.Features.Companies;

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

    // Defense-in-depth cap on the company description, mirroring ParseLogController's raw-text guard.
    // Prevents runaway LLM cost/latency even if an upstream caller fails to bound input size.
    private const int MaxCompanyDescriptionLength = 16000;

    [HttpPost("companies/persona")]
    public async Task<IActionResult> GeneratePersona(
        [FromBody] GeneratePersonaRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var companyDescriptionLength = request.CompanyDescription?.Length ?? 0;
        if (companyDescriptionLength > MaxCompanyDescriptionLength)
        {
            return BadRequest(new { message = "companyDescription exceeds maximum allowed size." });
        }

        try
        {
            var persona = await _personaService.GeneratePersonaAsync(request, cancellationToken);
            return Ok(persona);
        }
        catch (InvalidOperationException invalidOperationException)
        {
            _logger.LogWarning(invalidOperationException, "Persona generation failed");
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
        catch (HttpRequestException httpRequestException)
        {
            _logger.LogWarning(httpRequestException, "AI provider error during persona generation");
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
    }
}
