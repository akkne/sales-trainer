using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Features.ContentAdaptation.Models;
using Sellevate.Ai.Features.ContentAdaptation.Services.Abstract;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Evaluation;

namespace Sellevate.Ai.Features.ContentAdaptation;

/// <summary>
/// Phase 40.32. The two per-exercise LLM calls of the batch adaptation block, as internal
/// service-to-service routes — the same seam, the same guard and the same route prefix as 40.27's
/// pipeline, and like it deliberately <b>not</b> exposed through the gateway (docs/AI_SERVICE.md).
///
/// <para>
/// <b>Stateless, like everything else under <c>/ai/content</c>.</b> There is no batch here, no
/// organization column and no tenant read: the caller supplies one exercise and the customer's
/// profile, and gets one document back. The batch, its lease, its queue and — above all — the
/// decision to apply anything live in learning-service, which owns <c>Exercises</c>. Splitting it
/// the other way round would put the block's expensive half outside the meter roadmap 40.33 needs
/// and would give the content tables a second writer in another database.
/// </para>
///
/// <para>
/// <b>Neither route writes anything anywhere, and that is structural rather than a promise.</b> The
/// rewrite returns a proposed body; nothing in this service can store it, apply it, or reach the
/// lesson it came from.
/// </para>
/// </summary>
[ApiController]
[Route("ai/content")]
[ServiceFilter(typeof(InternalServiceAuthFilter))]
public sealed class ContentAdaptationController(
    IExerciseRewriteService exerciseRewriteService,
    IExerciseReviewService exerciseReviewService,
    ILogger<ContentAdaptationController> logger) : ControllerBase
{
    [HttpPost("rewrite")]
    public async Task<IActionResult> Rewrite(
        [FromBody] AdaptExerciseRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ExerciseType))
        {
            return BadRequest(new { message = "exerciseType is required." });
        }

        try
        {
            return Ok(await exerciseRewriteService.RewriteAsync(request, cancellationToken));
        }
        catch (OpenAiException openAiException)
        {
            logger.LogWarning(openAiException, "AI provider error while rewriting an exercise");
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
        catch (InvalidOperationException invalidOperationException)
        {
            logger.LogWarning(invalidOperationException, "Rewriting an exercise failed");
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
        catch (HttpRequestException httpRequestException)
        {
            logger.LogWarning(httpRequestException, "AI provider unreachable while rewriting an exercise");
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
    }

    [HttpPost("review")]
    public async Task<IActionResult> Review(
        [FromBody] AdaptExerciseRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ExerciseType))
        {
            return BadRequest(new { message = "exerciseType is required." });
        }

        try
        {
            return Ok(await exerciseReviewService.ReviewAsync(request, cancellationToken));
        }
        catch (OpenAiException openAiException)
        {
            logger.LogWarning(openAiException, "AI provider error while reviewing an exercise");
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
        catch (InvalidOperationException invalidOperationException)
        {
            logger.LogWarning(invalidOperationException, "Reviewing an exercise failed");
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
        catch (HttpRequestException httpRequestException)
        {
            logger.LogWarning(httpRequestException, "AI provider unreachable while reviewing an exercise");
            return StatusCode(503, new { message = "AI service unavailable. Please try again later." });
        }
    }
}
