using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.SkillTree;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

public record AdminSkillDto(
    Guid Id,
    string Title,
    string Slug,
    string IconName,
    int SortOrder,
    Guid? PrerequisiteSkillId,
    string[] ApplicableSalesTypes
);

public record CreateSkillRequestDto(
    string Title,
    string Slug,
    string IconName,
    int SortOrder,
    Guid? PrerequisiteSkillId,
    string[] ApplicableSalesTypes
);

[ApiController]
[Route("admin/skills")]
[Authorize(Policy = "RequireAdmin")]
public class AdminSkillsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdminSkillDto>>> GetAll()
    {
        var skills = await db.Skills
            .OrderBy(s => s.SortOrder)
            .Select(s => new AdminSkillDto(
                s.Id, s.Title, s.Slug, s.IconName, s.SortOrder,
                s.PrerequisiteSkillId, s.ApplicableSalesTypes))
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
            Slug = dto.Slug,
            IconName = dto.IconName,
            SortOrder = dto.SortOrder,
            PrerequisiteSkillId = dto.PrerequisiteSkillId,
            ApplicableSalesTypes = dto.ApplicableSalesTypes
        };

        db.Skills.Add(skill);
        await db.SaveChangesAsync();

        return Ok(new AdminSkillDto(
            skill.Id, skill.Title, skill.Slug, skill.IconName,
            skill.SortOrder, skill.PrerequisiteSkillId, skill.ApplicableSalesTypes));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminSkillDto>> Update(
        Guid id, [FromBody] CreateSkillRequestDto dto)
    {
        var skill = await db.Skills.FindAsync(id);
        if (skill is null) return NotFound();

        skill.Title = dto.Title;
        skill.Slug = dto.Slug;
        skill.IconName = dto.IconName;
        skill.SortOrder = dto.SortOrder;
        skill.PrerequisiteSkillId = dto.PrerequisiteSkillId;
        skill.ApplicableSalesTypes = dto.ApplicableSalesTypes;

        await db.SaveChangesAsync();

        return Ok(new AdminSkillDto(
            skill.Id, skill.Title, skill.Slug, skill.IconName,
            skill.SortOrder, skill.PrerequisiteSkillId, skill.ApplicableSalesTypes));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var skill = await db.Skills.FindAsync(id);
        if (skill is null) return NotFound();

        db.Skills.Remove(skill);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
