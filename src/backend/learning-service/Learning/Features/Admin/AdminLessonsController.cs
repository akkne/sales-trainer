using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.Lessons.Services.Implementation;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Admin;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.RequirePlatformAdministrator)]
public sealed class AdminLessonsController(LearningDbContext database, ILogger<AdminLessonsController> logger) : ControllerBase
{
    [HttpGet("admin/lessons")]
    public async Task<ActionResult<IReadOnlyList<AdminLessonWithTopicDto>>> GetAll(CancellationToken cancellationToken = default)
    {
        var lessons = await database.Lessons
            .Join(database.Topics, lesson => lesson.TopicId, topic => topic.Id, (lesson, topic) => new { lesson, topic })
            .OrderBy(pair => pair.topic.IconicName).ThenBy(pair => pair.lesson.OrderInTopic)
            .Select(pair => new AdminLessonWithTopicDto(
                pair.lesson.Id, pair.lesson.TopicId, pair.topic.IconicName, pair.topic.Title,
                pair.lesson.Title, pair.lesson.OrderInTopic))
            .ToListAsync(cancellationToken);

        return Ok(lessons);
    }

    [HttpGet("admin/topics/{topicIconicName}/lessons")]
    public async Task<ActionResult<IReadOnlyList<AdminLessonDto>>> GetByTopic(string topicIconicName, CancellationToken cancellationToken = default)
    {
        var topic = await database.Topics.FirstOrDefaultAsync(candidate => candidate.IconicName == topicIconicName, cancellationToken);
        if (topic is null) return NotFound(new { message = $"Topic '{topicIconicName}' not found." });

        var lessons = await database.Lessons
            .Where(lesson => lesson.TopicId == topic.Id)
            .OrderBy(lesson => lesson.OrderInTopic)
            .Select(lesson => new AdminLessonDto(
                lesson.Id, lesson.TopicId, lesson.Title, lesson.OrderInTopic, lesson.Slug, lesson.IsArchived))
            .ToListAsync(cancellationToken);

        return Ok(lessons);
    }

    [HttpPost("admin/topics/{topicIconicName}/lessons")]
    public async Task<ActionResult<AdminLessonDto>> Create(
        string topicIconicName, [FromBody] CreateLessonRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var topic = await database.Topics.FirstOrDefaultAsync(candidate => candidate.IconicName == topicIconicName, cancellationToken);
        if (topic is null) return NotFound(new { message = $"Topic '{topicIconicName}' not found." });

        var lessonId = Guid.NewGuid();

        if (!TryResolveSlug(requestDto.Slug, lessonId, out var slug))
        {
            return BadRequest(new { message = "Slug must be lowercase latin letters, digits and single hyphens." });
        }

        var lesson = new Lesson
        {
            Id = lessonId,
            TopicId = topic.Id,
            Title = requestDto.Title,
            OrderInTopic = requestDto.OrderInTopic,
            Slug = slug
        };

        database.Lessons.Add(lesson);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Lesson created LessonId={LessonId} TopicIconicName={TopicIconicName} Title={Title} Slug={Slug} by ActorId={ActorId}",
            lesson.Id, topicIconicName, lesson.Title, lesson.Slug, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminLessonDto(
            lesson.Id, lesson.TopicId, lesson.Title, lesson.OrderInTopic, lesson.Slug, lesson.IsArchived));
    }

    [HttpPut("admin/lessons/{id:guid}")]
    public async Task<ActionResult<AdminLessonDto>> Update(
        Guid id, [FromBody] CreateLessonRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var lesson = await database.Lessons.FindAsync([id], cancellationToken);
        if (lesson is null) return NotFound();

        // Phase 40.15: an omitted slug leaves the existing one alone rather than regenerating it.
        // Regenerating would silently change the lesson's stable identifier on every title edit,
        // which is the one thing a slug must not do.
        if (requestDto.Slug is not null)
        {
            if (!LessonSlugGenerator.TryNormalize(requestDto.Slug, out var normalizedSlug))
            {
                return BadRequest(new { message = "Slug must be lowercase latin letters, digits and single hyphens." });
            }

            lesson.Slug = normalizedSlug;
        }

        lesson.Title = requestDto.Title;
        lesson.OrderInTopic = requestDto.OrderInTopic;

        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Lesson updated LessonId={LessonId} Title={Title} Slug={Slug} by ActorId={ActorId}",
            id, lesson.Title, lesson.Slug, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminLessonDto(
            lesson.Id, lesson.TopicId, lesson.Title, lesson.OrderInTopic, lesson.Slug, lesson.IsArchived));
    }

    /// <summary>
    /// Phase 40.15. An explicit slug is validated, never rewritten; an absent one becomes a
    /// collision-free machine slug derived from the lesson's own id.
    /// </summary>
    private static bool TryResolveSlug(string? requestedSlug, Guid lessonId, out string slug)
    {
        if (requestedSlug is null)
        {
            slug = LessonSlugGenerator.GenerateFromLessonId(lessonId);
            return true;
        }

        return LessonSlugGenerator.TryNormalize(requestedSlug, out slug);
    }

    [HttpDelete("admin/lessons/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var lesson = await database.Lessons.FindAsync([id], cancellationToken);
        if (lesson is null) return NotFound();

        database.Lessons.Remove(lesson);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Lesson deleted LessonId={LessonId} TopicId={TopicId} by ActorId={ActorId}",
            id, lesson.TopicId, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }
}
