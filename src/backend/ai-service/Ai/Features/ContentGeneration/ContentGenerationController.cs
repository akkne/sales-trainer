using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Features.ContentGeneration.Models;
using Sellevate.Ai.Features.ContentGeneration.Services.Abstract;
using Sellevate.Ai.Features.ContentGeneration.Services.Implementation;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Evaluation;

namespace Sellevate.Ai.Features.ContentGeneration;

/// <summary>
/// Phase 40.27. The two LLM halves of the admin content pipeline, as internal service-to-service
/// routes — the same seam and the same guard as <c>POST /ai/evaluate</c>, and like it deliberately
/// <b>not</b> exposed through the gateway (docs/AI_SERVICE.md).
///
/// <para>
/// <b>Why the compute is here and the state is in learning-service.</b> This service's bounded
/// context is "everything that talks to an LLM or a speech API", and the single point through which
/// every LLM call passes is what makes per-organization spend enforcement possible at all (roadmap
/// 40.33). The job row, its state machine and the content it eventually writes belong to the service
/// that owns <c>Lessons</c>, <c>Exercises</c> and <c>LessonVersions</c>. Splitting it the other way
/// round would put the most expensive call in the product outside the meter and would give
/// learning-service's content tables a second writer in another database.
/// </para>
///
/// <para>
/// <b>Both routes are stateless and store nothing.</b> There is no job here, no organization column
/// and no tenant read: the caller supplies the material or the structure, and gets a document back.
/// The organization these documents belong to is a fact of the learning-service row that holds them.
/// </para>
///
/// <para>
/// Phase 40.28. <c>structure</c> now answers two questions in one completion: what is in the material
/// and whether there is enough of it (<see cref="StructuredMaterialDto"/>). It still does not
/// <i>refuse</i> anything — a refusal is a state of a learning-service run, not an HTTP status here,
/// because the customer has to be able to add material and carry on rather than start over.
/// </para>
/// </summary>
[ApiController]
[Route("ai/content")]
[ServiceFilter(typeof(InternalServiceAuthFilter))]
public sealed class ContentGenerationController(
    IMaterialStructuringService materialStructuringService,
    IExerciseGenerationService exerciseGenerationService,
    ILogger<ContentGenerationController> logger) : ControllerBase
{
    [HttpPost("structure")]
    public async Task<IActionResult> Structure(
        [FromBody] StructureMaterialRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Material))
        {
            return BadRequest(new { message = "material is required." });
        }

        if (request.Material.Length > AiRequestSizeLimits.MaximumSourceMaterialCharacters)
        {
            return BadRequest(new { message = "material exceeds maximum allowed size." });
        }

        try
        {
            return Ok(await materialStructuringService.ExtractAsync(request, cancellationToken));
        }
        catch (OpenAiException openAiException)
        {
            logger.LogWarning(openAiException, "AI provider error while structuring uploaded material");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
        catch (InvalidOperationException invalidOperationException)
        {
            logger.LogWarning(invalidOperationException, "Structuring uploaded material failed");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
        catch (HttpRequestException httpRequestException)
        {
            logger.LogWarning(httpRequestException, "AI provider unreachable while structuring uploaded material");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(
        [FromBody] GenerateExercisesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.Structure is null)
        {
            return BadRequest(new { message = "structure is required." });
        }

        try
        {
            return Ok(await exerciseGenerationService.GenerateAsync(request, cancellationToken));
        }
        catch (OpenAiException openAiException)
        {
            logger.LogWarning(openAiException, "AI provider error while generating exercises");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
        catch (InvalidOperationException invalidOperationException)
        {
            logger.LogWarning(invalidOperationException, "Generating exercises failed");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
        catch (HttpRequestException httpRequestException)
        {
            logger.LogWarning(httpRequestException, "AI provider unreachable while generating exercises");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
    }
}
