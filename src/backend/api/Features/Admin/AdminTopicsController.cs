using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.SkillTree.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

public record AdminTopicDto(
    Guid Id,
    Guid SkillId,
    string Title,
    int OrderInSkill
);

public record AdminTopicWithSkillDto(
    Guid Id,
    Guid SkillId,
    string SkillTitle,
    string Title,
    int OrderInSkill
);

public record CreateTopicRequestDto(
    string Title,
    int OrderInSkill
);

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public class AdminTopicsController(AppDbContext db, ILogger<AdminTopicsController> logger) : ControllerBase
{
    [HttpGet("admin/topics")]
    public async Task<ActionResult<List<AdminTopicWithSkillDto>>> GetAll()
    {
        var topics = await db.Topics
            .Join(db.Skills, t => t.SkillId, s => s.Id, (t, s) => new { t, s })
            .OrderBy(x => x.s.OrderInTree).ThenBy(x => x.t.OrderInSkill)
            .Select(x => new AdminTopicWithSkillDto(
                x.t.Id, x.t.SkillId, x.s.Title,
                x.t.Title, x.t.OrderInSkill))
            .ToListAsync();

        return Ok(topics);
    }

    [HttpGet("admin/skills/{skillId:guid}/topics")]
    public async Task<ActionResult<List<AdminTopicDto>>> GetBySkill(Guid skillId)
    {
        var topics = await db.Topics
            .Where(t => t.SkillId == skillId)
            .OrderBy(t => t.OrderInSkill)
            .Select(t => new AdminTopicDto(t.Id, t.SkillId, t.Title, t.OrderInSkill))
            .ToListAsync();

        return Ok(topics);
    }

    [HttpPost("admin/skills/{skillId:guid}/topics")]
    public async Task<ActionResult<AdminTopicDto>> Create(
        Guid skillId, [FromBody] CreateTopicRequestDto dto)
    {
        var skillExists = await db.Skills.AnyAsync(s => s.Id == skillId);
        if (!skillExists) return NotFound();

        var topic = new Topic
        {
            Id = Guid.NewGuid(),
            SkillId = skillId,
            Title = dto.Title,
            OrderInSkill = dto.OrderInSkill
        };

        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        logger.LogInformation("Topic created TopicId={TopicId} SkillId={SkillId} Title={Title} by ActorId={ActorId}",
            topic.Id, skillId, topic.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminTopicDto(topic.Id, topic.SkillId, topic.Title, topic.OrderInSkill));
    }

    [HttpPut("admin/topics/{id:guid}")]
    public async Task<ActionResult<AdminTopicDto>> Update(
        Guid id, [FromBody] CreateTopicRequestDto dto)
    {
        var topic = await db.Topics.FindAsync(id);
        if (topic is null) return NotFound();

        topic.Title = dto.Title;
        topic.OrderInSkill = dto.OrderInSkill;

        await db.SaveChangesAsync();

        logger.LogInformation("Topic updated TopicId={TopicId} Title={Title} by ActorId={ActorId}",
            id, topic.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminTopicDto(topic.Id, topic.SkillId, topic.Title, topic.OrderInSkill));
    }

    [HttpDelete("admin/topics/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var topic = await db.Topics.FindAsync(id);
        if (topic is null) return NotFound();

        db.Topics.Remove(topic);
        await db.SaveChangesAsync();

        logger.LogWarning("Topic deleted TopicId={TopicId} SkillId={SkillId} by ActorId={ActorId}",
            id, topic.SkillId, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }
}
