using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Reference.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

public record AdminReferenceMaterialDto(
    Guid Id,
    Guid SkillId,
    string SkillTitle,
    string Title,
    string MarkdownContent,
    int SortOrder,
    string? Category,
    string[] Tags
);

public record CreateReferenceMaterialRequestDto(
    string Title,
    string MarkdownContent,
    int SortOrder,
    string? Category = null,
    string? Tags = null
);

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public class AdminReferenceController(AppDbContext db, ILogger<AdminReferenceController> logger) : ControllerBase
{
    [HttpGet("admin/reference")]
    public async Task<ActionResult<List<AdminReferenceMaterialDto>>> GetAll(
        [FromQuery] Guid? skillId,
        [FromQuery] string? category,
        [FromQuery] string? search)
    {
        var query = from material in db.ReferenceMaterials
                    join skill in db.Skills on material.SkillId equals skill.Id
                    select new { material, skill };

        if (skillId.HasValue)
            query = query.Where(pair => pair.material.SkillId == skillId.Value);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(pair => pair.material.Category == category);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(pair =>
                pair.material.Title.ToLower().Contains(searchLower) ||
                pair.material.MarkdownContent.ToLower().Contains(searchLower));
        }

        var results = await query
            .OrderBy(pair => pair.skill.OrderInTree)
            .ThenBy(pair => pair.material.SortOrder)
            .ThenBy(pair => pair.material.Title)
            .ToListAsync();

        return Ok(results.Select(pair => MapToDto(pair.material, pair.skill.Title)).ToList());
    }

    [HttpGet("admin/reference/categories")]
    public async Task<ActionResult<List<string>>> GetCategories()
    {
        var categories = await db.ReferenceMaterials
            .Where(m => m.Category != null)
            .Select(m => m.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return Ok(categories);
    }

    [HttpGet("admin/skills/{skillId:guid}/reference")]
    public async Task<ActionResult<List<AdminReferenceMaterialDto>>> GetBySkill(Guid skillId)
    {
        var skill = await db.Skills.FindAsync(skillId);
        if (skill is null) return NotFound();

        var materials = await db.ReferenceMaterials
            .Where(r => r.SkillId == skillId)
            .OrderBy(r => r.SortOrder)
            .ToListAsync();

        return Ok(materials.Select(m => MapToDto(m, skill.Title)).ToList());
    }

    [HttpPost("admin/skills/{skillId:guid}/reference")]
    public async Task<ActionResult<AdminReferenceMaterialDto>> Create(
        Guid skillId, [FromBody] CreateReferenceMaterialRequestDto dto)
    {
        var skill = await db.Skills.FindAsync(skillId);
        if (skill is null) return NotFound();

        var material = new ReferenceMaterial
        {
            Id = Guid.NewGuid(),
            SkillId = skillId,
            Title = dto.Title,
            MarkdownContent = dto.MarkdownContent,
            SortOrder = dto.SortOrder,
            Category = dto.Category,
            Tags = dto.Tags
        };

        db.ReferenceMaterials.Add(material);
        await db.SaveChangesAsync();

        logger.LogInformation("Reference material created MaterialId={MaterialId} SkillId={SkillId} Title={Title} by ActorId={ActorId}",
            material.Id, skillId, material.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(MapToDto(material, skill.Title));
    }

    [HttpPut("admin/reference/{id:guid}")]
    public async Task<ActionResult<AdminReferenceMaterialDto>> Update(
        Guid id, [FromBody] CreateReferenceMaterialRequestDto dto)
    {
        var material = await db.ReferenceMaterials.FindAsync(id);
        if (material is null) return NotFound();

        var skill = await db.Skills.FindAsync(material.SkillId);
        if (skill is null) return NotFound();

        material.Title = dto.Title;
        material.MarkdownContent = dto.MarkdownContent;
        material.SortOrder = dto.SortOrder;
        material.Category = dto.Category;
        material.Tags = dto.Tags;

        await db.SaveChangesAsync();

        logger.LogInformation("Reference material updated MaterialId={MaterialId} Title={Title} by ActorId={ActorId}",
            id, material.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(MapToDto(material, skill.Title));
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

    private static AdminReferenceMaterialDto MapToDto(ReferenceMaterial material, string skillTitle) =>
        new(
            material.Id,
            material.SkillId,
            skillTitle,
            material.Title,
            material.MarkdownContent,
            material.SortOrder,
            material.Category,
            material.Tags != null
                ? material.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>()
        );
}
