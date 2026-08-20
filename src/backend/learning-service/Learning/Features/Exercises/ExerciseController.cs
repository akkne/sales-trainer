using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Extensions;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Infrastructure.Ai;


namespace Sellevate.Learning.Features.Exercises;

/// <summary>
/// The learner-facing surface of the library: lessons, exercise bodies, answer submission, and the
/// two interactive <c>ai_dialogue</c> transports.
///
/// <para>
/// <b>Every route acts on the caller identified by the token; none takes a user id.</b> One learner
/// therefore cannot read or advance another's progress through this controller at all, rather than
/// being stopped by a check that could be forgotten on a new route.
/// </para>
///
/// <para>
/// <b>An unavailable AI provider is a 503, never a 500.</b> Grading and chat both depend on
/// ai-service, and its being busy, rate-limited or unreachable is upstream state rather than a defect
/// here — so those exceptions are logged as warnings and mapped, and the learner is told to retry in
/// their own language instead of seeing a stack trace.
/// </para>
/// </summary>
[ApiController]
[TenantScoped]
[Authorize]
public sealed class ExerciseController(IExerciseService exerciseService, ILogger<ExerciseController> logger) : ControllerBase
{
    /// <summary>
    /// Shown to the learner whenever ai-service cannot answer. Deliberately identical across grading,
    /// chat and voice: from the learner's side the three failures are the same event.
    /// </summary>
    private const string AiServiceUnavailableMessage = "AI сервис временно недоступен. Попробуйте позже.";

    /// <summary>
    /// Frame flag bits of the voice stream. Values are read by the client's binary parser and cannot
    /// be renumbered without shipping a new client.
    /// </summary>
    private const uint VoiceFrameFinalFlag = 1u;

    /// <inheritdoc cref="VoiceFrameFinalFlag"/>
    private const uint VoiceFrameStopSignalFlag = 2u;

    private const int VoiceFrameLengthPrefixByteCount = 4;

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
        catch (OpenAiException exception)
        {
            logger.LogWarning(exception, "AI provider error during evaluation of exercise {ExerciseId}", exerciseId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = AiServiceUnavailableMessage });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "AI evaluation service error");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = AiServiceUnavailableMessage });
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "AI evaluation service HTTP error");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = AiServiceUnavailableMessage });
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
        catch (OpenAiException exception)
        {
            logger.LogWarning(exception, "AI provider error during exercise chat for exercise {ExerciseId}", exerciseId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = AiServiceUnavailableMessage });
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "AI provider unreachable during exercise chat for exercise {ExerciseId}", exerciseId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = AiServiceUnavailableMessage });
        }
    }

    /// <summary>
    /// Streams the AI partner's spoken reply as a sequence of length-prefixed binary frames.
    ///
    /// <para>
    /// <b>Frame layout, big-endian throughout:</b> a 4-byte flag word (see
    /// <see cref="VoiceFrameFinalFlag"/>), then a 4-byte text length followed by that many UTF-8 bytes,
    /// then a 4-byte audio length followed by that many MP3 bytes. Either payload may be zero-length —
    /// text arrives ahead of its audio so the learner sees the reply while it is still being
    /// synthesized. The client reads frames until the one carrying the final flag.
    /// </para>
    ///
    /// <para>
    /// <b>The exercise is validated before the status line is committed.</b> Once a 200 has been
    /// written the response cannot carry an error code any more, so a missing exercise or a wrong type
    /// has to be discovered first; after that point a provider failure can only end the stream cleanly,
    /// which leaves the learner with a short reply rather than a torn connection.
    /// </para>
    ///
    /// <para>
    /// Buffering is disabled on the response because a proxy that accumulates the body would defeat the
    /// point of streaming it.
    /// </para>
    /// </summary>
    [HttpPost("exercises/{exerciseId:guid}/voice/stream")]
    public async Task StreamVoiceMessage(
        Guid exerciseId,
        [FromBody] ExerciseChatRequestDto chatRequest,
        CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        try
        {
            await exerciseService.ValidateExerciseForVoiceAsync(exerciseId, cancellationToken);
        }
        catch (KeyNotFoundException exception)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            await Response.WriteAsJsonAsync(new { message = exception.Message }, cancellationToken);
            return;
        }
        catch (NotSupportedException exception)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { message = exception.Message }, cancellationToken);
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "application/octet-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await foreach (var chunk in exerciseService.StreamExerciseVoiceAsync(
                userId, exerciseId, chatRequest.Message, cancellationToken))
            {
                var frameFlags = (chunk.IsFinal ? VoiceFrameFinalFlag : 0u)
                                 | (chunk.IsStopSignal ? VoiceFrameStopSignalFlag : 0u);
                var textBytes = System.Text.Encoding.UTF8.GetBytes(chunk.Text);

                await WriteUInt32BigEndianAsync(Response.Body, frameFlags, cancellationToken);
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
        catch (OpenAiException exception)
        {
            logger.LogWarning(exception, "Exercise voice stream aborted by AI provider error for exercise {ExerciseId}", exerciseId);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Exercise voice stream aborted — AI provider unreachable for exercise {ExerciseId}", exerciseId);
        }
    }

    private static async Task WriteUInt32BigEndianAsync(Stream stream, uint value, CancellationToken cancellationToken)
    {
        var buffer = new byte[VoiceFrameLengthPrefixByteCount];
        buffer[0] = (byte)((value >> 24) & 0xff);
        buffer[1] = (byte)((value >> 16) & 0xff);
        buffer[2] = (byte)((value >> 8) & 0xff);
        buffer[3] = (byte)(value & 0xff);
        await stream.WriteAsync(buffer, cancellationToken);
    }
}
