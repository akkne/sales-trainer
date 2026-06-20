using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Features.Evaluation.Models;
using Sellevate.Ai.Features.Evaluation.Services.Abstract;

namespace Sellevate.Ai.Features.Evaluation;

[ApiController]
[Route("ai")]
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

        try
        {
            var result = await _evaluationService.EvaluateAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (NotSupportedException notSupportedException)
        {
            return BadRequest(new { message = notSupportedException.Message });
        }
        catch (InvalidOperationException invalidOperationException)
        {
            _logger.LogWarning(invalidOperationException, "Evaluation failed for exercise type {ExerciseType}", request.ExerciseType);
            return StatusCode(503, new { message = invalidOperationException.Message });
        }
    }
}
