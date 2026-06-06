using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.SkillTree.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

[ApiController]
[Route("admin/skills")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminSkillsController(AppDbContext database, ILogger<AdminSkillsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdminSkillDto>>> GetAll()
    {
        var skills = await database.Skills
            .OrderBy(s => s.OrderInTree)
            .Select(s => new AdminSkillDto(s.Id, s.IconicName, s.Title, s.Description, s.OrderInTree, s.Stage))
            .ToListAsync();

        return Ok(skills);
    }

    [HttpPost]
    public async Task<ActionResult<AdminSkillDto>> Create([FromBody] CreateSkillRequestDto dto)
    {
        var exists = await database.Skills.AnyAsync(s => s.IconicName == dto.IconicName);
        if (exists)
            return Conflict(new { message = $"Skill with iconicName '{dto.IconicName}' already exists." });

        var skill = new Skill
        {
            Id = Guid.NewGuid(),
            IconicName = dto.IconicName,
            Title = dto.Title,
            Description = dto.Description,
            OrderInTree = dto.OrderInTree,
            Stage = string.IsNullOrWhiteSpace(dto.Stage) ? "general" : dto.Stage.Trim()
        };

        database.Skills.Add(skill);
        await database.SaveChangesAsync();

        logger.LogInformation("Skill created SkillId={SkillId} IconicName={IconicName} by ActorId={ActorId}",
            skill.Id, skill.IconicName, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminSkillDto(skill.Id, skill.IconicName, skill.Title, skill.Description, skill.OrderInTree, skill.Stage));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminSkillDto>> Update(
        Guid id, [FromBody] UpdateSkillRequestDto dto)
    {
        var skill = await database.Skills.FindAsync(id);
        if (skill is null) return NotFound();

        if (dto.IconicName is not null && dto.IconicName != skill.IconicName)
        {
            var exists = await database.Skills.AnyAsync(s => s.IconicName == dto.IconicName && s.Id != id);
            if (exists)
                return Conflict(new { message = $"Skill with iconicName '{dto.IconicName}' already exists." });
            skill.IconicName = dto.IconicName;
        }

        if (dto.Title is not null) skill.Title = dto.Title;
        if (dto.Description is not null) skill.Description = dto.Description;
        if (dto.OrderInTree.HasValue) skill.OrderInTree = dto.OrderInTree.Value;
        if (!string.IsNullOrWhiteSpace(dto.Stage)) skill.Stage = dto.Stage.Trim();

        await database.SaveChangesAsync();

        logger.LogInformation("Skill updated SkillId={SkillId} IconicName={IconicName} by ActorId={ActorId}",
            skill.Id, skill.IconicName, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminSkillDto(skill.Id, skill.IconicName, skill.Title, skill.Description, skill.OrderInTree, skill.Stage));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var skill = await database.Skills.FindAsync(id);
        if (skill is null) return NotFound();

        database.Skills.Remove(skill);
        await database.SaveChangesAsync();

        logger.LogWarning("Skill deleted SkillId={SkillId} Title={Title} by ActorId={ActorId}",
            id, skill.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }
}
