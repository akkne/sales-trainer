using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Admin;

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminLessonsController(LearningDbContext database, ILogger<AdminLessonsController> logger) : ControllerBase
{
    [HttpGet("admin/lessons")]
    public async Task<ActionResult<List<AdminLessonWithTopicDto>>> GetAll()
    {
        var lessons = await database.Lessons
            .Join(database.Topics, lesson => lesson.TopicId, topic => topic.Id, (lesson, topic) => new { lesson, topic })
            .OrderBy(pair => pair.topic.IconicName).ThenBy(pair => pair.lesson.OrderInTopic)
            .Select(pair => new AdminLessonWithTopicDto(
                pair.lesson.Id, pair.lesson.TopicId, pair.topic.IconicName, pair.topic.Title,
                pair.lesson.Title, pair.lesson.OrderInTopic))
            .ToListAsync();

        return Ok(lessons);
    }

    [HttpGet("admin/topics/{topicIconicName}/lessons")]
    public async Task<ActionResult<List<AdminLessonDto>>> GetByTopic(string topicIconicName)
    {
        var topic = await database.Topics.FirstOrDefaultAsync(candidate => candidate.IconicName == topicIconicName);
        if (topic is null) return NotFound(new { message = $"Topic '{topicIconicName}' not found." });

        var lessons = await database.Lessons
            .Where(lesson => lesson.TopicId == topic.Id)
            .OrderBy(lesson => lesson.OrderInTopic)
            .Select(lesson => new AdminLessonDto(lesson.Id, lesson.TopicId, lesson.Title, lesson.OrderInTopic))
            .ToListAsync();

        return Ok(lessons);
    }

    [HttpPost("admin/topics/{topicIconicName}/lessons")]
    public async Task<ActionResult<AdminLessonDto>> Create(
        string topicIconicName, [FromBody] CreateLessonRequestDto requestDto)
    {
        var topic = await database.Topics.FirstOrDefaultAsync(candidate => candidate.IconicName == topicIconicName);
        if (topic is null) return NotFound(new { message = $"Topic '{topicIconicName}' not found." });

        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            TopicId = topic.Id,
            Title = requestDto.Title,
            OrderInTopic = requestDto.OrderInTopic
        };

        database.Lessons.Add(lesson);
        await database.SaveChangesAsync();

        logger.LogInformation("Lesson created LessonId={LessonId} TopicIconicName={TopicIconicName} Title={Title} by ActorId={ActorId}",
            lesson.Id, topicIconicName, lesson.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminLessonDto(lesson.Id, lesson.TopicId, lesson.Title, lesson.OrderInTopic));
    }

    [HttpPut("admin/lessons/{id:guid}")]
    public async Task<ActionResult<AdminLessonDto>> Update(
        Guid id, [FromBody] CreateLessonRequestDto requestDto)
    {
        var lesson = await database.Lessons.FindAsync(id);
        if (lesson is null) return NotFound();

        lesson.Title = requestDto.Title;
        lesson.OrderInTopic = requestDto.OrderInTopic;

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
