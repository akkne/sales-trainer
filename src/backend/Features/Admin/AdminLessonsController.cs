using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Lessons;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

public record AdminLessonDto(
    Guid Id,
    Guid SkillId,
    string Title,
    int SortOrder,
    int DifficultyLevel,
    int XpReward
);

public record CreateLessonRequestDto(
    string Title,
    int SortOrder,
    int DifficultyLevel,
    int XpReward
);

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public class AdminLessonsController(AppDbContext db) : ControllerBase
{
    [HttpGet("admin/skills/{skillId:guid}/lessons")]
    public async Task<ActionResult<List<AdminLessonDto>>> GetBySkill(Guid skillId)
    {
        var lessons = await db.Lessons
            .Where(l => l.SkillId == skillId)
            .OrderBy(l => l.SortOrder)
            .Select(l => new AdminLessonDto(
                l.Id, l.SkillId, l.Title, l.SortOrder, l.DifficultyLevel, l.XpReward))
            .ToListAsync();

        return Ok(lessons);
    }

    [HttpPost("admin/skills/{skillId:guid}/lessons")]
    public async Task<ActionResult<AdminLessonDto>> Create(
        Guid skillId, [FromBody] CreateLessonRequestDto dto)
    {
        var skillExists = await db.Skills.AnyAsync(s => s.Id == skillId);
        if (!skillExists) return NotFound();

        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            SkillId = skillId,
            Title = dto.Title,
            SortOrder = dto.SortOrder,
            DifficultyLevel = dto.DifficultyLevel,
            XpReward = dto.XpReward
        };

        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();

        return Ok(new AdminLessonDto(
            lesson.Id, lesson.SkillId, lesson.Title,
            lesson.SortOrder, lesson.DifficultyLevel, lesson.XpReward));
    }

    [HttpPut("admin/lessons/{id:guid}")]
    public async Task<ActionResult<AdminLessonDto>> Update(
        Guid id, [FromBody] CreateLessonRequestDto dto)
    {
        var lesson = await db.Lessons.FindAsync(id);
        if (lesson is null) return NotFound();

        lesson.Title = dto.Title;
        lesson.SortOrder = dto.SortOrder;
        lesson.DifficultyLevel = dto.DifficultyLevel;
        lesson.XpReward = dto.XpReward;

        await db.SaveChangesAsync();

        return Ok(new AdminLessonDto(
            lesson.Id, lesson.SkillId, lesson.Title,
            lesson.SortOrder, lesson.DifficultyLevel, lesson.XpReward));
    }

    [HttpDelete("admin/lessons/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var lesson = await db.Lessons.FindAsync(id);
        if (lesson is null) return NotFound();

        db.Lessons.Remove(lesson);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
