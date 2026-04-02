using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Reference;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

public record AdminReferenceMaterialDto(
    Guid Id,
    Guid SkillId,
    string Title,
    string MarkdownContent,
    int SortOrder
);

public record CreateReferenceMaterialRequestDto(
    string Title,
    string MarkdownContent,
    int SortOrder
);

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public class AdminReferenceController(AppDbContext db, ILogger<AdminReferenceController> logger) : ControllerBase
{
    [HttpGet("admin/skills/{skillId:guid}/reference")]
    public async Task<ActionResult<List<AdminReferenceMaterialDto>>> GetBySkill(Guid skillId)
    {
        var materials = await db.ReferenceMaterials
            .Where(r => r.SkillId == skillId)
            .OrderBy(r => r.SortOrder)
            .Select(r => new AdminReferenceMaterialDto(
                r.Id, r.SkillId, r.Title, r.MarkdownContent, r.SortOrder))
            .ToListAsync();

        return Ok(materials);
    }

    [HttpPost("admin/skills/{skillId:guid}/reference")]
    public async Task<ActionResult<AdminReferenceMaterialDto>> Create(
        Guid skillId, [FromBody] CreateReferenceMaterialRequestDto dto)
    {
        var skillExists = await db.Skills.AnyAsync(s => s.Id == skillId);
        if (!skillExists) return NotFound();

        var material = new ReferenceMaterial
        {
            Id = Guid.NewGuid(),
            SkillId = skillId,
            Title = dto.Title,
            MarkdownContent = dto.MarkdownContent,
            SortOrder = dto.SortOrder
        };

        db.ReferenceMaterials.Add(material);
        await db.SaveChangesAsync();

        logger.LogInformation("Reference material created MaterialId={MaterialId} SkillId={SkillId} Title={Title} by ActorId={ActorId}",
            material.Id, skillId, material.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminReferenceMaterialDto(
            material.Id, material.SkillId, material.Title,
            material.MarkdownContent, material.SortOrder));
    }

    [HttpPut("admin/reference/{id:guid}")]
    public async Task<ActionResult<AdminReferenceMaterialDto>> Update(
        Guid id, [FromBody] CreateReferenceMaterialRequestDto dto)
    {
        var material = await db.ReferenceMaterials.FindAsync(id);
        if (material is null) return NotFound();

        material.Title = dto.Title;
        material.MarkdownContent = dto.MarkdownContent;
        material.SortOrder = dto.SortOrder;

        await db.SaveChangesAsync();

        logger.LogInformation("Reference material updated MaterialId={MaterialId} Title={Title} by ActorId={ActorId}",
            id, material.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminReferenceMaterialDto(
            material.Id, material.SkillId, material.Title,
            material.MarkdownContent, material.SortOrder));
    }

    [HttpDelete("admin/reference/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var material = await db.ReferenceMaterials.FindAsync(id);
        if (material is null) return NotFound();

        db.ReferenceMaterials.Remove(material);
        await db.SaveChangesAsync();

        logger.LogWarning("Reference material deleted MaterialId={MaterialId} SkillId={SkillId} by ActorId={ActorId}",
            id, material.SkillId, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }
}
