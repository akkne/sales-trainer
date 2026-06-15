using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Exercises.Services;
using SalesTrainer.Api.Features.Lessons.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminExercisesController(AppDbContext database, ILogger<AdminExercisesController> logger) : ControllerBase
{
    [HttpGet("admin/lessons/{lessonId:guid}/exercises")]
    public async Task<ActionResult<List<AdminExerciseDto>>> GetByLesson(Guid lessonId)
    {
        var exercises = await database.Exercises
            .Where(e => e.LessonId == lessonId)
            .OrderBy(e => e.OrderInLesson)
            .ToListAsync();

        var result = exercises.Select(e => new AdminExerciseDto(
            e.Id,
            e.LessonId,
            e.Type,
            e.OrderInLesson,
            JsonSerializer.Deserialize<JsonElement>(e.SerializedContent),
            e.CustomAiPrompt
        )).ToList();

        return Ok(result);
    }

    [HttpPost("admin/lessons/{lessonId:guid}/exercises")]
    public async Task<ActionResult<AdminExerciseDto>> Create(
        Guid lessonId, [FromBody] CreateExerciseRequestDto dto)
    {
        var contentErrors = ExerciseContentValidator.Validate(dto.Type, dto.Content);
        if (contentErrors.Count > 0)
            return BadRequest(new { message = string.Join(" ", contentErrors) });

        var lessonExists = await database.Lessons.AnyAsync(l => l.Id == lessonId);
        if (!lessonExists) return NotFound();

        var now = DateTime.UtcNow;
        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            LessonId = lessonId,
            Type = dto.Type,
            OrderInLesson = dto.OrderInLesson,
            SerializedContent = dto.Content.GetRawText(),
            CustomAiPrompt = dto.CustomAiPrompt,
            CreatedAt = now,
            UpdatedAt = now
        };

        database.Exercises.Add(exercise);
        await database.SaveChangesAsync();

        logger.LogInformation("Exercise created ExerciseId={ExerciseId} LessonId={LessonId} Type={Type} by ActorId={ActorId}",
            exercise.Id, lessonId, exercise.Type, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminExerciseDto(
            exercise.Id, exercise.LessonId, exercise.Type,
            exercise.OrderInLesson, dto.Content, exercise.CustomAiPrompt));
    }

    [HttpPost("admin/lessons/{lessonId:guid}/exercises/import")]
    public async Task<ActionResult<ExercisesImportResultDto>> Import(
        Guid lessonId, [FromBody] List<CreateExerciseRequestDto> items)
    {
        var lessonExists = await database.Lessons.AnyAsync(l => l.Id == lessonId);
        if (!lessonExists) return NotFound();

        if (items is null || items.Count == 0)
            return BadRequest(new { message = "JSON must be a non-empty array of exercise objects." });

        if (items.Count > 500)
            return BadRequest(new { message = "Too many exercises in one import (max 500)." });

        var existing = await database.Exercises
            .Where(e => e.LessonId == lessonId)
            .ToListAsync();

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

                var match = existing.FirstOrDefault(e => e.OrderInLesson == item.OrderInLesson);
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

        await database.SaveChangesAsync();

        logger.LogInformation(
            "Exercises imported LessonId={LessonId} Created={Created} Updated={Updated} Errors={ErrorCount} by ActorId={ActorId}",
            lessonId, created, updated, errors.Count, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new ExercisesImportResultDto(created, updated, errors));
    }

    [HttpPut("admin/exercises/{id:guid}")]
    public async Task<ActionResult<AdminExerciseDto>> Update(
        Guid id, [FromBody] CreateExerciseRequestDto dto)
    {
        var contentErrors = ExerciseContentValidator.Validate(dto.Type, dto.Content);
        if (contentErrors.Count > 0)
            return BadRequest(new { message = string.Join(" ", contentErrors) });

        var exercise = await database.Exercises.FindAsync(id);
        if (exercise is null) return NotFound();

        exercise.Type = dto.Type;
        exercise.OrderInLesson = dto.OrderInLesson;
        exercise.SerializedContent = dto.Content.GetRawText();
        exercise.CustomAiPrompt = dto.CustomAiPrompt;
        exercise.UpdatedAt = DateTime.UtcNow;

        await database.SaveChangesAsync();

        logger.LogInformation("Exercise updated ExerciseId={ExerciseId} Type={Type} by ActorId={ActorId}",
            id, exercise.Type, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminExerciseDto(
            exercise.Id, exercise.LessonId, exercise.Type,
            exercise.OrderInLesson, dto.Content, exercise.CustomAiPrompt));
    }

    [HttpDelete("admin/exercises/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var exercise = await database.Exercises.FindAsync(id);
        if (exercise is null) return NotFound();

        database.Exercises.Remove(exercise);
        await database.SaveChangesAsync();

        logger.LogWarning("Exercise deleted ExerciseId={ExerciseId} LessonId={LessonId} Type={Type} by ActorId={ActorId}",
            id, exercise.LessonId, exercise.Type, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }
}
