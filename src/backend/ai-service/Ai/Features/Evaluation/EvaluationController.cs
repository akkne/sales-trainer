using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Evaluation.Models;
using Sellevate.Ai.Features.Evaluation.Services.Abstract;

namespace Sellevate.Ai.Features.Evaluation;

/// <summary>
/// The internal grading route learning-service calls when a learner submits an answer.
///
/// <para>
/// Every provider failure is answered as a 503 and never a 500: a rejected request, a spent quota or a
/// bad credential is upstream state, and reporting it as a server fault here would send somebody
/// looking through ai-service logs for a bug that is not in ai-service.
/// </para>
/// </summary>
[ApiController]
[Route("ai")]
[ServiceFilter(typeof(InternalServiceAuthFilter))]
public sealed class EvaluationController : ControllerBase
{
    private readonly IExerciseEvaluationService _evaluationService;
    private readonly ILogger<EvaluationController> _logger;

    public EvaluationController(IExerciseEvaluationService evaluationService, ILogger<EvaluationController> logger)
    {
        _evaluationService = evaluationService;
        _logger = logger;
    }

    [HttpPost("evaluate")]
    public async Task<IActionResult> Evaluate(
        [FromBody] EvaluateExerciseRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ExerciseType))
        {
            return BadRequest(new { message = "exerciseType is required." });
        }

        var userAnswerJson = request.UserAnswer.GetRawText();
        if (userAnswerJson.Length > AiRequestSizeLimits.MaximumPromptTextCharacters)
        {
            return BadRequest(new { message = "userAnswer exceeds maximum allowed size." });
        }

        try
        {
            var result = await _evaluationService.EvaluateAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (NotSupportedException notSupportedException)
        {
            return BadRequest(new { message = notSupportedException.Message });
        }
        catch (OpenAiException openAiException)
        {
            _logger.LogWarning(openAiException, "AI provider error during evaluation");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
        catch (InvalidOperationException invalidOperationException)
        {
            _logger.LogWarning(invalidOperationException, "Evaluation failed for exercise type {ExerciseType}", request.ExerciseType);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
        catch (HttpRequestException httpRequestException)
        {
            _logger.LogWarning(httpRequestException, "AI provider error during evaluation for exercise type {ExerciseType}", request.ExerciseType);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = AiProviderFailureMessages.ServiceUnavailable });
        }
    }
}
