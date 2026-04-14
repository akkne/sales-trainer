using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.SkillTree.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

public record AdminSkillDto(
    Guid Id,
    string Title,
    string? Description,
    int OrderInTree
);

public record CreateSkillRequestDto(
    string Title,
    string? Description,
    int OrderInTree
);

[ApiController]
[Route("admin/skills")]
[Authorize(Policy = "RequireAdmin")]
public class AdminSkillsController(AppDbContext db, ILogger<AdminSkillsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdminSkillDto>>> GetAll()
    {
        var skills = await db.Skills
            .OrderBy(s => s.OrderInTree)
            .Select(s => new AdminSkillDto(s.Id, s.Title, s.Description, s.OrderInTree))
            .ToListAsync();

        return Ok(skills);
    }

    [HttpPost]
    public async Task<ActionResult<AdminSkillDto>> Create([FromBody] CreateSkillRequestDto dto)
    {
        var skill = new Skill
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Description = dto.Description,
            OrderInTree = dto.OrderInTree
        };

        db.Skills.Add(skill);
        await db.SaveChangesAsync();

        logger.LogInformation("Skill created SkillId={SkillId} Title={Title} by ActorId={ActorId}",
            skill.Id, skill.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminSkillDto(skill.Id, skill.Title, skill.Description, skill.OrderInTree));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminSkillDto>> Update(
        Guid id, [FromBody] CreateSkillRequestDto dto)
    {
        var skill = await db.Skills.FindAsync(id);
        if (skill is null) return NotFound();

        skill.Title = dto.Title;
        skill.Description = dto.Description;
        skill.OrderInTree = dto.OrderInTree;

        await db.SaveChangesAsync();

        logger.LogInformation("Skill updated SkillId={SkillId} Title={Title} by ActorId={ActorId}",
            skill.Id, skill.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminSkillDto(skill.Id, skill.Title, skill.Description, skill.OrderInTree));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var skill = await db.Skills.FindAsync(id);
        if (skill is null) return NotFound();

        db.Skills.Remove(skill);
        await db.SaveChangesAsync();

        logger.LogWarning("Skill deleted SkillId={SkillId} Title={Title} by ActorId={ActorId}",
            id, skill.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }
}
