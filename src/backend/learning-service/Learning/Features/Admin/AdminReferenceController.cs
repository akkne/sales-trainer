using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Features.Reference.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Admin;

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminReferenceController(LearningDbContext database, ILogger<AdminReferenceController> logger) : ControllerBase
{
    [HttpGet("admin/reference")]
    public async Task<ActionResult<List<AdminReferenceMaterialDto>>> GetAll(
        [FromQuery] Guid? skillId,
        [FromQuery] string? category,
        [FromQuery] string? search)
    {
        var query = from material in database.ReferenceMaterials
                    join skill in database.Skills on material.SkillId equals skill.Id
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
        var categories = await database.ReferenceMaterials
            .Where(material => material.Category != null)
            .Select(material => material.Category!)
            .Distinct()
            .OrderBy(category => category)
            .ToListAsync();

        return Ok(categories);
    }

    [HttpGet("admin/skills/{skillId:guid}/reference")]
    public async Task<ActionResult<List<AdminReferenceMaterialDto>>> GetBySkill(Guid skillId)
    {
        var skill = await database.Skills.FindAsync(skillId);
        if (skill is null) return NotFound();

        var materials = await database.ReferenceMaterials
            .Where(material => material.SkillId == skillId)
            .OrderBy(material => material.SortOrder)
            .ToListAsync();

        return Ok(materials.Select(material => MapToDto(material, skill.Title)).ToList());
    }

    [HttpPost("admin/skills/{skillId:guid}/reference")]
    public async Task<ActionResult<AdminReferenceMaterialDto>> Create(
        Guid skillId, [FromBody] CreateReferenceMaterialRequestDto requestDto)
    {
        var skill = await database.Skills.FindAsync(skillId);
        if (skill is null) return NotFound();

        var material = new ReferenceMaterial
        {
            Id = Guid.NewGuid(),
            SkillId = skillId,
            Title = requestDto.Title,
            MarkdownContent = requestDto.MarkdownContent,
            SortOrder = requestDto.SortOrder,
            Category = requestDto.Category,
            Tags = requestDto.Tags
        };

        database.ReferenceMaterials.Add(material);
        await database.SaveChangesAsync();

        logger.LogInformation("Reference material created MaterialId={MaterialId} SkillId={SkillId} Title={Title} by ActorId={ActorId}",
            material.Id, skillId, material.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(MapToDto(material, skill.Title));
    }

    [HttpPut("admin/reference/{id:guid}")]
    public async Task<ActionResult<AdminReferenceMaterialDto>> Update(
        Guid id, [FromBody] CreateReferenceMaterialRequestDto requestDto)
    {
        var material = await database.ReferenceMaterials.FindAsync(id);
        if (material is null) return NotFound();

        var skill = await database.Skills.FindAsync(material.SkillId);
        if (skill is null) return NotFound();

        material.Title = requestDto.Title;
        material.MarkdownContent = requestDto.MarkdownContent;
        material.SortOrder = requestDto.SortOrder;
        material.Category = requestDto.Category;
        material.Tags = requestDto.Tags;

        await database.SaveChangesAsync();

        logger.LogInformation("Reference material updated MaterialId={MaterialId} Title={Title} by ActorId={ActorId}",
            id, material.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(MapToDto(material, skill.Title));
    }

    [HttpDelete("admin/reference/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var material = await database.ReferenceMaterials.FindAsync(id);
        if (material is null) return NotFound();

        database.ReferenceMaterials.Remove(material);
        await database.SaveChangesAsync();

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
