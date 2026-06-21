using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Learning.Common.Extensions;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;


namespace Sellevate.Learning.Features.Exercises;

[ApiController]
[Authorize]
public sealed class ExerciseController(IExerciseService exerciseService, ILogger<ExerciseController> logger) : ControllerBase
{
    [HttpGet("lessons")]
    public async Task<ActionResult<IReadOnlyList<LessonSummaryDto>>> GetAllLessons(CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId)) return Unauthorized();

        var lessons = await exerciseService.GetAllLessonsAsync(userId, cancellationToken);
        return Ok(lessons);
    }

    [HttpGet("topics/{topicId:guid}/lessons")]
    public async Task<ActionResult<IReadOnlyList<LessonSummaryDto>>> GetLessonsForTopic(
        Guid topicId,
        CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId)) return Unauthorized();

        var lessonSummaries = await exerciseService.GetLessonsForTopicAsync(userId, topicId, cancellationToken);
        return Ok(lessonSummaries);
    }

    [HttpGet("lessons/{lessonId:guid}/exercises")]
    public async Task<ActionResult<IReadOnlyList<ExerciseDto>>> GetExercisesForLesson(
        Guid lessonId,
        CancellationToken cancellationToken)
    {
        var exerciseDtos = await exerciseService.GetExercisesForLessonAsync(lessonId, cancellationToken);
        return Ok(exerciseDtos);
    }

    [HttpPost("exercises/{exerciseId:guid}/submit")]
    public async Task<ActionResult<ExerciseSubmissionResultDto>> SubmitExerciseAnswer(
        Guid exerciseId,
        [FromBody] SubmitExerciseRequestDto submitRequest,
        CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId)) return Unauthorized();

        try
        {
            var submissionResult = await exerciseService.SubmitExerciseAnswerAsync(
                userId, exerciseId, submitRequest.Answer, cancellationToken);
            return Ok(submissionResult);
        }
        catch (ExerciseAnswerValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (NotSupportedException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "AI evaluation service error");
            return StatusCode(503, new { message = "AI сервис временно недоступен. Попробуйте позже." });
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "AI evaluation service HTTP error");
            return StatusCode(503, new { message = "AI сервис временно недоступен. Попробуйте позже." });
        }
    }

    [HttpPost("exercises/{exerciseId:guid}/chat")]
    public async Task<ActionResult<ExerciseChatResponseDto>> SendChatMessage(
        Guid exerciseId,
        [FromBody] ExerciseChatRequestDto chatRequest,
        CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId)) return Unauthorized();

        try
        {
            var chatResponse = await exerciseService.SendChatMessageAsync(
                userId, exerciseId, chatRequest.Message, cancellationToken);
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

    [HttpPost("exercises/{exerciseId:guid}/voice/stream")]
    public async Task StreamVoiceMessage(
        Guid exerciseId,
        [FromBody] ExerciseChatRequestDto chatRequest,
        CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId))
        {
            Response.StatusCode = 401;
            return;
        }

        // Validate exercise exists and is ai_dialogue BEFORE committing 200 —
        // once we write status 200 we can no longer return a proper error code.
        try
        {
            await exerciseService.ValidateExerciseForVoiceAsync(exerciseId, cancellationToken);
        }
        catch (KeyNotFoundException exception)
        {
            Response.StatusCode = 404;
            await Response.WriteAsJsonAsync(new { message = exception.Message }, cancellationToken);
            return;
        }
        catch (NotSupportedException exception)
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(new { message = exception.Message }, cancellationToken);
            return;
        }

        Response.StatusCode = 200;
        Response.ContentType = "application/octet-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var chunk in exerciseService.StreamExerciseVoiceAsync(
                userId, exerciseId, chatRequest.Message, cancellationToken))
            {
                var flags = (chunk.IsFinal ? 1u : 0u) | (chunk.IsStopSignal ? 2u : 0u);
                var textBytes = System.Text.Encoding.UTF8.GetBytes(chunk.Text);

                await WriteUInt32BigEndianAsync(Response.Body, flags, cancellationToken);
                await WriteUInt32BigEndianAsync(Response.Body, (uint)textBytes.Length, cancellationToken);
                if (textBytes.Length > 0)
                    await Response.Body.WriteAsync(textBytes, cancellationToken);
                await WriteUInt32BigEndianAsync(Response.Body, (uint)chunk.AudioMp3.Length, cancellationToken);
                if (chunk.AudioMp3.Length > 0)
                    await Response.Body.WriteAsync(chunk.AudioMp3, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Exercise voice stream cancelled by client for exercise {ExerciseId}", exerciseId);
        }
    }

    private static async Task WriteUInt32BigEndianAsync(Stream stream, uint value, CancellationToken cancellationToken)
    {
        var buffer = new byte[4];
        buffer[0] = (byte)((value >> 24) & 0xff);
        buffer[1] = (byte)((value >> 16) & 0xff);
        buffer[2] = (byte)((value >> 8) & 0xff);
        buffer[3] = (byte)(value & 0xff);
        await stream.WriteAsync(buffer, cancellationToken);
    }
}
