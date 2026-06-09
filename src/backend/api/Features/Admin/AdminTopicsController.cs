using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.SkillTree.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminTopicsController(AppDbContext database, ILogger<AdminTopicsController> logger) : ControllerBase
{
    [HttpGet("admin/topics")]
    public async Task<ActionResult<List<AdminTopicWithSkillDto>>> GetAll()
    {
        var topics = await database.Topics
            .Join(database.Skills, t => t.SkillId, s => s.Id, (t, s) => new { t, s })
            .OrderBy(x => x.s.OrderInTree).ThenBy(x => x.t.OrderInSkill)
            .Select(x => new AdminTopicWithSkillDto(
                x.t.Id, x.t.SkillId, x.s.IconicName, x.s.Title,
                x.t.IconicName, x.t.Title, x.t.OrderInSkill))
            .ToListAsync();

        return Ok(topics);
    }

    [HttpGet("admin/skills/{skillIconicName}/topics")]
    public async Task<ActionResult<List<AdminTopicDto>>> GetBySkill(string skillIconicName)
    {
        var skill = await database.Skills.FirstOrDefaultAsync(s => s.IconicName == skillIconicName);
        if (skill is null) return NotFound(new { message = $"Skill '{skillIconicName}' not found." });

        var topics = await database.Topics
            .Where(t => t.SkillId == skill.Id)
            .OrderBy(t => t.OrderInSkill)
            .Select(t => new AdminTopicDto(t.Id, t.SkillId, t.IconicName, t.Title, t.OrderInSkill))
            .ToListAsync();

        return Ok(topics);
    }

    [HttpPost("admin/skills/{skillIconicName}/topics")]
    public async Task<ActionResult<AdminTopicDto>> Create(
        string skillIconicName, [FromBody] CreateTopicRequestDto dto)
    {
        var skill = await database.Skills.FirstOrDefaultAsync(s => s.IconicName == skillIconicName);
        if (skill is null) return NotFound(new { message = $"Skill '{skillIconicName}' not found." });

        var exists = await database.Topics.AnyAsync(t => t.IconicName == dto.IconicName);
        if (exists)
            return Conflict(new { message = $"Topic with iconicName '{dto.IconicName}' already exists." });

        var topic = new Topic
        {
            Id = Guid.NewGuid(),
            SkillId = skill.Id,
            IconicName = dto.IconicName,
            Title = dto.Title,
            OrderInSkill = dto.OrderInSkill
        };

        database.Topics.Add(topic);
        await database.SaveChangesAsync();

        logger.LogInformation("Topic created TopicId={TopicId} IconicName={IconicName} by ActorId={ActorId}",
            topic.Id, topic.IconicName, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminTopicDto(topic.Id, topic.SkillId, topic.IconicName, topic.Title, topic.OrderInSkill));
    }

    [HttpPut("admin/skills/{skillIconicName}/topics/{topicIconicName}")]
    public async Task<ActionResult<AdminTopicDto>> Update(
        string skillIconicName, string topicIconicName, [FromBody] UpdateTopicRequestDto dto)
    {
        var topic = await database.Topics.FirstOrDefaultAsync(t => t.IconicName == topicIconicName);
        if (topic is null) return NotFound();

        if (dto.IconicName is not null) topic.IconicName = dto.IconicName;
        if (dto.Title is not null) topic.Title = dto.Title;
        if (dto.OrderInSkill.HasValue) topic.OrderInSkill = dto.OrderInSkill.Value;

        await database.SaveChangesAsync();

        logger.LogInformation("Topic updated TopicId={TopicId} IconicName={IconicName} by ActorId={ActorId}",
            topic.Id, topic.IconicName, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminTopicDto(topic.Id, topic.SkillId, topic.IconicName, topic.Title, topic.OrderInSkill));
    }

    [HttpPut("admin/topics/{id:guid}")]
    public async Task<ActionResult<AdminTopicDto>> UpdateById(
        Guid id, [FromBody] UpdateTopicRequestDto dto)
    {
        var topic = await database.Topics.FindAsync(id);
        if (topic is null) return NotFound();

        if (dto.IconicName is not null && dto.IconicName != topic.IconicName)
        {
            var clash = await database.Topics.AnyAsync(t => t.IconicName == dto.IconicName && t.Id != id);
            if (clash)
                return Conflict(new { message = $"Topic with iconicName '{dto.IconicName}' already exists." });
            topic.IconicName = dto.IconicName;
        }
        if (dto.Title is not null) topic.Title = dto.Title;
        if (dto.OrderInSkill.HasValue) topic.OrderInSkill = dto.OrderInSkill.Value;

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
