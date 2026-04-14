using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Lessons.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

public record AdminExerciseDto(
    Guid Id,
    Guid LessonId,
    string Type,
    int OrderInLesson,
    JsonElement Content,
    string? CustomAiPrompt
);

public record CreateExerciseRequestDto(
    string Type,
    int OrderInLesson,
    JsonElement Content,
    string? CustomAiPrompt
);

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public class AdminExercisesController(AppDbContext db, ILogger<AdminExercisesController> logger) : ControllerBase
{
    [HttpGet("admin/lessons/{lessonId:guid}/exercises")]
    public async Task<ActionResult<List<AdminExerciseDto>>> GetByLesson(Guid lessonId)
    {
        var exercises = await db.Exercises
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
        var lessonExists = await db.Lessons.AnyAsync(l => l.Id == lessonId);
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

        db.Exercises.Add(exercise);
        await db.SaveChangesAsync();

        logger.LogInformation("Exercise created ExerciseId={ExerciseId} LessonId={LessonId} Type={Type} by ActorId={ActorId}",
            exercise.Id, lessonId, exercise.Type, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminExerciseDto(
            exercise.Id, exercise.LessonId, exercise.Type,
            exercise.OrderInLesson, dto.Content, exercise.CustomAiPrompt));
    }

    [HttpPut("admin/exercises/{id:guid}")]
    public async Task<ActionResult<AdminExerciseDto>> Update(
        Guid id, [FromBody] CreateExerciseRequestDto dto)
    {
        var exercise = await db.Exercises.FindAsync(id);
        if (exercise is null) return NotFound();

        exercise.Type = dto.Type;
        exercise.OrderInLesson = dto.OrderInLesson;
        exercise.SerializedContent = dto.Content.GetRawText();
        exercise.CustomAiPrompt = dto.CustomAiPrompt;
        exercise.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        logger.LogInformation("Exercise updated ExerciseId={ExerciseId} Type={Type} by ActorId={ActorId}",
            id, exercise.Type, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminExerciseDto(
            exercise.Id, exercise.LessonId, exercise.Type,
            exercise.OrderInLesson, dto.Content, exercise.CustomAiPrompt));
    }

    [HttpDelete("admin/exercises/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var exercise = await db.Exercises.FindAsync(id);
        if (exercise is null) return NotFound();

        db.Exercises.Remove(exercise);
        await db.SaveChangesAsync();

        logger.LogWarning("Exercise deleted ExerciseId={ExerciseId} LessonId={LessonId} Type={Type} by ActorId={ActorId}",
            id, exercise.LessonId, exercise.Type, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }
}
