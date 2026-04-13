using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SalesTrainer.Api.Features.Reference;

[ApiController]
[Authorize]
public class ReferenceController(IReferenceService referenceService) : ControllerBase
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

    [HttpGet("reference")]
    public async Task<ActionResult<IReadOnlyList<ReferenceMaterialDto>>> GetAllReferenceMaterials(
        [FromQuery] string? category,
        [FromQuery] string? search)
    {
        var materials = await referenceService.GetAllReferenceMaterialsAsync(category, search);
        return Ok(materials);
    }

    [HttpGet("reference/categories")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCategories()
    {
        var categories = await referenceService.GetAllCategoriesAsync();
        return Ok(categories);
    }
}
