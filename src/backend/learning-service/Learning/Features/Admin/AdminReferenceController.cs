using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Content;
using Sellevate.Learning.Features.Reference.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Admin;

/// <summary>
/// Phase 40.18. Opened to organization administrators so that a copy-on-write override is something
/// its owner can actually edit — the third of the review screen's three actions. The policy admits
/// them; <see cref="ContentAuthoringGuard"/> is what keeps them off the global library, because the
/// content RLS policy cannot (its WITH CHECK admits a null owner by design).
/// </summary>
[ApiController]
[TenantTransaction]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
public sealed class AdminReferenceController(LearningDbContext database, ILogger<AdminReferenceController> logger) : ControllerBase
{
    [HttpGet("admin/reference")]
    public async Task<ActionResult<IReadOnlyList<AdminReferenceMaterialDto>>> GetAll(
        [FromQuery] Guid? skillId,
        [FromQuery] string? category,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
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
            .ToListAsync(cancellationToken);

        return Ok(results.Select(pair => MapToDto(pair.material, pair.skill.Title)).ToList());
    }

    [HttpGet("admin/reference/categories")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCategories(CancellationToken cancellationToken = default)
    {
        var categories = await database.ReferenceMaterials
            .Where(material => material.Category != null)
            .Select(material => material.Category!)
            .Distinct()
            .OrderBy(category => category)
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }

    [HttpGet("admin/skills/{skillId:guid}/reference")]
    public async Task<ActionResult<IReadOnlyList<AdminReferenceMaterialDto>>> GetBySkill(Guid skillId, CancellationToken cancellationToken = default)
    {
        var skill = await database.Skills.FindAsync([skillId], cancellationToken);
        if (skill is null) return NotFound();

        var materials = await database.ReferenceMaterials
            .Where(material => material.SkillId == skillId)
            .OrderBy(material => material.SortOrder)
            .ToListAsync(cancellationToken);

        return Ok(materials.Select(material => MapToDto(material, skill.Title)).ToList());
    }

    /// <summary>
    /// Platform staff only: creating brand-new material is authoring the shared library, not customizing
    /// it. An organization gets its own copy through the override route; originating content from nothing
    /// is a different product question (40.19/40.20).
    /// </summary>
    [HttpPost("admin/skills/{skillId:guid}/reference")]
    public async Task<ActionResult<AdminReferenceMaterialDto>> Create(
        Guid skillId, [FromBody] CreateReferenceMaterialRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        if (!ContentAuthoringGuard.IsPlatformAdministrator(User)) return Forbid();

        var skill = await database.Skills.FindAsync([skillId], cancellationToken);
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
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Reference material created MaterialId={MaterialId} SkillId={SkillId} Title={Title} by ActorId={ActorId}",
            material.Id, skillId, material.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(MapToDto(material, skill.Title));
    }

    [HttpPut("admin/reference/{id:guid}")]
    public async Task<ActionResult<AdminReferenceMaterialDto>> Update(
        Guid id, [FromBody] CreateReferenceMaterialRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var material = await database.ReferenceMaterials.FindAsync([id], cancellationToken);
        if (material is null) return NotFound();
        if (!ContentAuthoringGuard.MayAuthor(User, material.OrganizationId)) return Forbid();

        var skill = await database.Skills.FindAsync([material.SkillId], cancellationToken);
        if (skill is null) return NotFound();

        material.Title = requestDto.Title;
        material.MarkdownContent = requestDto.MarkdownContent;
        material.SortOrder = requestDto.SortOrder;
        material.Category = requestDto.Category;
        material.Tags = requestDto.Tags;

        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Reference material updated MaterialId={MaterialId} Title={Title} by ActorId={ActorId}",
            id, material.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(MapToDto(material, skill.Title));
    }

    [HttpDelete("admin/reference/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var material = await database.ReferenceMaterials.FindAsync([id], cancellationToken);
        if (material is null) return NotFound();
        if (!ContentAuthoringGuard.MayAuthor(User, material.OrganizationId)) return Forbid();

        database.ReferenceMaterials.Remove(material);
        await database.SaveChangesAsync(cancellationToken);

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
