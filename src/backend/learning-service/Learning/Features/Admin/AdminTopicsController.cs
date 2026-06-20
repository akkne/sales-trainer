using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Admin;

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminTopicsController(LearningDbContext database, ILogger<AdminTopicsController> logger) : ControllerBase
{
    [HttpGet("admin/topics")]
    public async Task<ActionResult<List<AdminTopicWithSkillDto>>> GetAll()
    {
        var topics = await database.Topics
            .Join(database.Skills, topic => topic.SkillId, skill => skill.Id, (topic, skill) => new { topic, skill })
            .OrderBy(pair => pair.skill.OrderInTree).ThenBy(pair => pair.topic.OrderInSkill)
            .Select(pair => new AdminTopicWithSkillDto(
                pair.topic.Id, pair.topic.SkillId, pair.skill.IconicName, pair.skill.Title,
                pair.topic.IconicName, pair.topic.Title, pair.topic.OrderInSkill))
            .ToListAsync();

        return Ok(topics);
    }

    [HttpGet("admin/skills/{skillIconicName}/topics")]
    public async Task<ActionResult<List<AdminTopicDto>>> GetBySkill(string skillIconicName)
    {
        var skill = await database.Skills.FirstOrDefaultAsync(candidate => candidate.IconicName == skillIconicName);
        if (skill is null) return NotFound(new { message = $"Skill '{skillIconicName}' not found." });

        var topics = await database.Topics
            .Where(topic => topic.SkillId == skill.Id)
            .OrderBy(topic => topic.OrderInSkill)
            .Select(topic => new AdminTopicDto(topic.Id, topic.SkillId, topic.IconicName, topic.Title, topic.OrderInSkill))
            .ToListAsync();

        return Ok(topics);
    }

    [HttpPost("admin/skills/{skillIconicName}/topics")]
    public async Task<ActionResult<AdminTopicDto>> Create(
        string skillIconicName, [FromBody] CreateTopicRequestDto requestDto)
    {
        var skill = await database.Skills.FirstOrDefaultAsync(candidate => candidate.IconicName == skillIconicName);
        if (skill is null) return NotFound(new { message = $"Skill '{skillIconicName}' not found." });

        var exists = await database.Topics.AnyAsync(topic => topic.IconicName == requestDto.IconicName);
        if (exists)
            return Conflict(new { message = $"Topic with iconicName '{requestDto.IconicName}' already exists." });

        var topic = new Topic
        {
            Id = Guid.NewGuid(),
            SkillId = skill.Id,
            IconicName = requestDto.IconicName,
            Title = requestDto.Title,
            OrderInSkill = requestDto.OrderInSkill
        };

        database.Topics.Add(topic);
        await database.SaveChangesAsync();

        logger.LogInformation("Topic created TopicId={TopicId} IconicName={IconicName} by ActorId={ActorId}",
            topic.Id, topic.IconicName, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminTopicDto(topic.Id, topic.SkillId, topic.IconicName, topic.Title, topic.OrderInSkill));
    }

    [HttpPut("admin/skills/{skillIconicName}/topics/{topicIconicName}")]
    public async Task<ActionResult<AdminTopicDto>> Update(
        string skillIconicName, string topicIconicName, [FromBody] UpdateTopicRequestDto requestDto)
    {
        var topic = await database.Topics.FirstOrDefaultAsync(candidate => candidate.IconicName == topicIconicName);
        if (topic is null) return NotFound();

        if (requestDto.IconicName is not null) topic.IconicName = requestDto.IconicName;
        if (requestDto.Title is not null) topic.Title = requestDto.Title;
        if (requestDto.OrderInSkill.HasValue) topic.OrderInSkill = requestDto.OrderInSkill.Value;

        await database.SaveChangesAsync();

        logger.LogInformation("Topic updated TopicId={TopicId} IconicName={IconicName} by ActorId={ActorId}",
            topic.Id, topic.IconicName, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminTopicDto(topic.Id, topic.SkillId, topic.IconicName, topic.Title, topic.OrderInSkill));
    }

    [HttpPut("admin/topics/{id:guid}")]
    public async Task<ActionResult<AdminTopicDto>> UpdateById(
        Guid id, [FromBody] UpdateTopicRequestDto requestDto)
    {
        var topic = await database.Topics.FindAsync(id);
        if (topic is null) return NotFound();

        if (requestDto.IconicName is not null && requestDto.IconicName != topic.IconicName)
        {
            var clash = await database.Topics.AnyAsync(candidate => candidate.IconicName == requestDto.IconicName && candidate.Id != id);
            if (clash)
                return Conflict(new { message = $"Topic with iconicName '{requestDto.IconicName}' already exists." });
            topic.IconicName = requestDto.IconicName;
        }
        if (requestDto.Title is not null) topic.Title = requestDto.Title;
        if (requestDto.OrderInSkill.HasValue) topic.OrderInSkill = requestDto.OrderInSkill.Value;

        await database.SaveChangesAsync();

        logger.LogInformation("Topic updated TopicId={TopicId} IconicName={IconicName} by ActorId={ActorId}",
            topic.Id, topic.IconicName, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminTopicDto(topic.Id, topic.SkillId, topic.IconicName, topic.Title, topic.OrderInSkill));
    }

    [HttpDelete("admin/topics/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var topic = await database.Topics.FindAsync(id);
        if (topic is null) return NotFound();

        database.Topics.Remove(topic);
        await database.SaveChangesAsync();

        logger.LogWarning("Topic deleted TopicId={TopicId} IconicName={IconicName} by ActorId={ActorId}",
            id, topic.IconicName, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }
}
