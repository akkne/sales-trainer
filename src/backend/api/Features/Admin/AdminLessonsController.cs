using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Lessons.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminLessonsController(AppDbContext database, ILogger<AdminLessonsController> logger) : ControllerBase
{
    [HttpGet("admin/lessons")]
    public async Task<ActionResult<List<AdminLessonWithTopicDto>>> GetAll()
    {
        var lessons = await database.Lessons
            .Join(database.Topics, l => l.TopicId, t => t.Id, (l, t) => new { l, t })
            .OrderBy(x => x.t.IconicName).ThenBy(x => x.l.OrderInTopic)
            .Select(x => new AdminLessonWithTopicDto(
                x.l.Id, x.l.TopicId, x.t.IconicName, x.t.Title,
                x.l.Title, x.l.OrderInTopic))
            .ToListAsync();

        return Ok(lessons);
    }

    [HttpGet("admin/topics/{topicIconicName}/lessons")]
    public async Task<ActionResult<List<AdminLessonDto>>> GetByTopic(string topicIconicName)
    {
        var topic = await database.Topics.FirstOrDefaultAsync(t => t.IconicName == topicIconicName);
        if (topic is null) return NotFound(new { message = $"Topic '{topicIconicName}' not found." });

        var lessons = await database.Lessons
            .Where(l => l.TopicId == topic.Id)
            .OrderBy(l => l.OrderInTopic)
            .Select(l => new AdminLessonDto(l.Id, l.TopicId, l.Title, l.OrderInTopic))
            .ToListAsync();

        return Ok(lessons);
    }

    [HttpPost("admin/topics/{topicIconicName}/lessons")]
    public async Task<ActionResult<AdminLessonDto>> Create(
        string topicIconicName, [FromBody] CreateLessonRequestDto dto)
    {
        var topic = await database.Topics.FirstOrDefaultAsync(t => t.IconicName == topicIconicName);
        if (topic is null) return NotFound(new { message = $"Topic '{topicIconicName}' not found." });

        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            TopicId = topic.Id,
            Title = dto.Title,
            OrderInTopic = dto.OrderInTopic
        };

        database.Lessons.Add(lesson);
        await database.SaveChangesAsync();

        logger.LogInformation("Lesson created LessonId={LessonId} TopicIconicName={TopicIconicName} Title={Title} by ActorId={ActorId}",
            lesson.Id, topicIconicName, lesson.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminLessonDto(lesson.Id, lesson.TopicId, lesson.Title, lesson.OrderInTopic));
    }

    [HttpPut("admin/lessons/{id:guid}")]
    public async Task<ActionResult<AdminLessonDto>> Update(
        Guid id, [FromBody] CreateLessonRequestDto dto)
    {
        var lesson = await database.Lessons.FindAsync(id);
        if (lesson is null) return NotFound();

        lesson.Title = dto.Title;
        lesson.OrderInTopic = dto.OrderInTopic;

        await database.SaveChangesAsync();

        logger.LogInformation("Lesson updated LessonId={LessonId} Title={Title} by ActorId={ActorId}",
            id, lesson.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminLessonDto(lesson.Id, lesson.TopicId, lesson.Title, lesson.OrderInTopic));
    }

    [HttpDelete("admin/lessons/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var lesson = await database.Lessons.FindAsync(id);
        if (lesson is null) return NotFound();

        database.Lessons.Remove(lesson);
        await database.SaveChangesAsync();

        logger.LogWarning("Lesson deleted LessonId={LessonId} TopicId={TopicId} by ActorId={ActorId}",
            id, lesson.TopicId, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }
}
