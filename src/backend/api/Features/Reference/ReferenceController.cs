using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SalesTrainer.Api.Features.Reference;

[ApiController]
[Authorize]
public class ReferenceController(ReferenceService referenceService) : ControllerBase
{
    [HttpGet("skills/{skillSlug}/reference")]
    public async Task<ActionResult<IReadOnlyList<ReferenceMaterialDto>>> GetReferenceMaterials(
        string skillSlug)
    {
        try
        {
            var referenceMaterials =
                await referenceService.GetReferenceMaterialsForSkillAsync(skillSlug);
            return Ok(referenceMaterials);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    /// <summary>
    /// Returns all reference materials across all skills.
    /// Optional filters: category (exact match) and search (title/content contains).
    /// </summary>
    [HttpGet("reference")]
    public async Task<ActionResult<IReadOnlyList<ReferenceMaterialDto>>> GetAllReferenceMaterials(
        [FromQuery] string? category,
        [FromQuery] string? search)
    {
        var materials = await referenceService.GetAllReferenceMaterialsAsync(category, search);
        return Ok(materials);
    }

    /// <summary>
    /// Returns the list of distinct categories that have at least one reference material.
    /// </summary>
    [HttpGet("reference/categories")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCategories()
    {
        var categories = await referenceService.GetAllCategoriesAsync();
        return Ok(categories);
    }
}
