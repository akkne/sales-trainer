using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;

namespace SalesTrainer.Api.Features.Exercises;

[ApiController]
[Authorize]
public class ExerciseController(IExerciseService exerciseService) : ControllerBase
{
    [HttpGet("lessons")]
    public async Task<ActionResult<IReadOnlyList<LessonSummaryDto>>> GetAllLessons()
    {
        var userId = ResolveCurrentUserId();
        if (userId is null) return Unauthorized();

        var lessons = await exerciseService.GetAllLessonsAsync(userId.Value);
        return Ok(lessons);
    }

    [HttpGet("skills/{skillSlug}/lessons")]
    public async Task<ActionResult<IReadOnlyList<LessonSummaryDto>>> GetLessonsForSkill(
        string skillSlug)
    {
        var userId = ResolveCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var lessonSummaries = await exerciseService.GetLessonsForSkillAsync(userId.Value, skillSlug);
            return Ok(lessonSummaries);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpGet("lessons/{lessonId:guid}/exercises")]
    public async Task<ActionResult<IReadOnlyList<ExerciseDto>>> GetExercisesForLesson(
        Guid lessonId)
    {
        var exerciseDtos = await exerciseService.GetExercisesForLessonAsync(lessonId);
        return Ok(exerciseDtos);
    }

    [HttpPost("exercises/{exerciseId:guid}/submit")]
    public async Task<ActionResult<ExerciseSubmissionResultDto>> SubmitExerciseAnswer(
        Guid exerciseId,
        [FromBody] SubmitExerciseRequestDto submitRequest)
    {
        var userId = ResolveCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var submissionResult = await exerciseService.SubmitExerciseAnswerAsync(
                userId.Value, exerciseId, submitRequest.Answer);
            return Ok(submissionResult);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (NotSupportedException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("exercises/{exerciseId:guid}/chat")]
    public async Task<ActionResult<ExerciseChatResponseDto>> SendChatMessage(
        Guid exerciseId,
        [FromBody] ExerciseChatRequestDto chatRequest)
    {
        var userId = ResolveCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var chatResponse = await exerciseService.SendChatMessageAsync(
                userId.Value, exerciseId, chatRequest.Message);
            return Ok(chatResponse);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (NotSupportedException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private Guid? ResolveCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
