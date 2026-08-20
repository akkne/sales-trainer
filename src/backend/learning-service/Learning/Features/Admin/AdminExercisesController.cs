using Sellevate.BuildingBlocks.Tenancy;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Content;
using Sellevate.Learning.Features.Exercises.Services;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Admin;

/// <summary>
/// Phase 40.18. Opened to organization administrators, because this is where a lesson override is
/// actually edited: a lesson has no body of its own, and everything a customer wants to change —
/// question text, options, correct answers, grading prompts — lives in its exercise rows
/// (docs/TENANCY/CONTENT_MODEL.md §0). Ownership is checked per row, against the owning
/// <em>lesson</em> for creation and against the exercise itself for update and delete.
/// </summary>
[ApiController]
[TenantTransaction]
[TenantScoped]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
public sealed class AdminExercisesController(LearningDbContext database, ILogger<AdminExercisesController> logger) : ControllerBase
{
    [HttpGet("admin/lessons/{lessonId:guid}/exercises")]
    public async Task<ActionResult<IReadOnlyList<AdminExerciseDto>>> GetByLesson(Guid lessonId, CancellationToken cancellationToken = default)
    {
        var exercises = await database.Exercises
            .Where(exercise => exercise.LessonId == lessonId)
            .OrderBy(exercise => exercise.OrderInLesson)
            .ToListAsync(cancellationToken);

        var result = exercises.Select(exercise => new AdminExerciseDto(
            exercise.Id,
            exercise.LessonId,
            exercise.Type,
            exercise.OrderInLesson,
            JsonSerializer.Deserialize<JsonElement>(exercise.SerializedContent),
            exercise.CustomAiPrompt
        )).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Phase 40.18. An exercise belongs to whoever owns its lesson, so its organization is copied from
    /// the lesson rather than from the caller. Left null — as it was while every lesson was global — an
    /// exercise added to an organization's override would land in the shared library and appear inside
    /// that lesson for every other customer.
    /// </summary>
    [HttpPost("admin/lessons/{lessonId:guid}/exercises")]
    public async Task<ActionResult<AdminExerciseDto>> Create(
        Guid lessonId, [FromBody] CreateExerciseRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var contentErrors = ExerciseContentValidator.Validate(requestDto.Type, requestDto.Content);
        if (contentErrors.Count > 0)
            return BadRequest(new { message = string.Join(" ", contentErrors) });

        var owningLesson = await database.Lessons
            .FirstOrDefaultAsync(lesson => lesson.Id == lessonId, cancellationToken);
        if (owningLesson is null) return NotFound();
        if (!ContentAuthoringGuard.MayAuthor(User, owningLesson.OrganizationId)) return Forbid();

        var now = DateTime.UtcNow;
        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            OrganizationId = owningLesson.OrganizationId,
            LessonId = lessonId,
            Type = requestDto.Type,
            OrderInLesson = requestDto.OrderInLesson,
            SerializedContent = requestDto.Content.GetRawText(),
            CustomAiPrompt = requestDto.CustomAiPrompt,
            CreatedAt = now,
            UpdatedAt = now
        };

        database.Exercises.Add(exercise);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Exercise created ExerciseId={ExerciseId} LessonId={LessonId} Type={Type} by ActorId={ActorId}",
            exercise.Id, lessonId, exercise.Type, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminExerciseDto(
            exercise.Id, exercise.LessonId, exercise.Type,
            exercise.OrderInLesson, requestDto.Content, exercise.CustomAiPrompt));
    }

    [HttpPost("admin/lessons/{lessonId:guid}/exercises/import")]
    public async Task<ActionResult<ExercisesImportResultDto>> Import(
        Guid lessonId, [FromBody] List<CreateExerciseRequestDto> items, CancellationToken cancellationToken = default)
    {
        var owningLesson = await database.Lessons
            .FirstOrDefaultAsync(lesson => lesson.Id == lessonId, cancellationToken);
        if (owningLesson is null) return NotFound();
        if (!ContentAuthoringGuard.MayAuthor(User, owningLesson.OrganizationId)) return Forbid();

        if (items is null || items.Count == 0)
            return BadRequest(new { message = "JSON must be a non-empty array of exercise objects." });

        if (items.Count > 500)
            return BadRequest(new { message = "Too many exercises in one import (max 500)." });

        var existing = await database.Exercises
            .Where(exercise => exercise.LessonId == lessonId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var created = 0;
        var updated = 0;
        var errors = new List<string>();

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            try
            {
                if (string.IsNullOrWhiteSpace(item.Type))
                    throw new InvalidOperationException("type is empty.");

                var contentErrors = ExerciseContentValidator.Validate(item.Type, item.Content);
                if (contentErrors.Count > 0)
                {
                    errors.Add($"Item {index + 1} ({item.Type}): {string.Join(" ", contentErrors)}");
                    continue;
                }

                var match = existing.FirstOrDefault(exercise => exercise.OrderInLesson == item.OrderInLesson);
                if (match is not null)
                {
                    match.Type = item.Type;
                    match.SerializedContent = item.Content.GetRawText();
                    match.CustomAiPrompt = item.CustomAiPrompt;
                    match.UpdatedAt = now;
                    updated++;
                }
                else
                {
                    var exercise = new Exercise
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = owningLesson.OrganizationId,
                        LessonId = lessonId,
                        Type = item.Type,
                        OrderInLesson = item.OrderInLesson,
                        SerializedContent = item.Content.GetRawText(),
                        CustomAiPrompt = item.CustomAiPrompt,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    database.Exercises.Add(exercise);
                    existing.Add(exercise);
                    created++;
                }
            }
            catch (Exception exception)
            {
                errors.Add($"Item {index + 1}: {exception.Message}");
            }
        }

        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Exercises imported LessonId={LessonId} Created={Created} Updated={Updated} Errors={ErrorCount} by ActorId={ActorId}",
            lessonId, created, updated, errors.Count, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new ExercisesImportResultDto(created, updated, errors));
    }

    [HttpPut("admin/exercises/{id:guid}")]
    public async Task<ActionResult<AdminExerciseDto>> Update(
        Guid id, [FromBody] CreateExerciseRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var contentErrors = ExerciseContentValidator.Validate(requestDto.Type, requestDto.Content);
        if (contentErrors.Count > 0)
            return BadRequest(new { message = string.Join(" ", contentErrors) });

        var exercise = await database.Exercises.FindAsync([id], cancellationToken);
        if (exercise is null) return NotFound();
        if (!ContentAuthoringGuard.MayAuthor(User, exercise.OrganizationId)) return Forbid();

        exercise.Type = requestDto.Type;
        exercise.OrderInLesson = requestDto.OrderInLesson;
        exercise.SerializedContent = requestDto.Content.GetRawText();
        exercise.CustomAiPrompt = requestDto.CustomAiPrompt;
        exercise.UpdatedAt = DateTime.UtcNow;

        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Exercise updated ExerciseId={ExerciseId} Type={Type} by ActorId={ActorId}",
            id, exercise.Type, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminExerciseDto(
            exercise.Id, exercise.LessonId, exercise.Type,
            exercise.OrderInLesson, requestDto.Content, exercise.CustomAiPrompt));
    }

    [HttpDelete("admin/exercises/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var exercise = await database.Exercises.FindAsync([id], cancellationToken);
        if (exercise is null) return NotFound();
        if (!ContentAuthoringGuard.MayAuthor(User, exercise.OrganizationId)) return Forbid();

        database.Exercises.Remove(exercise);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Exercise deleted ExerciseId={ExerciseId} LessonId={LessonId} Type={Type} by ActorId={ActorId}",
            id, exercise.LessonId, exercise.Type, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }
}
