using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesTrainer.Api.Features.Reference.Models;
using SalesTrainer.Api.Features.Reference.Services.Abstract;

namespace SalesTrainer.Api.Features.Reference;

[ApiController]
[Authorize]
public class ReferenceController(IReferenceService referenceService) : ControllerBase
{
    [HttpGet("skills/{skillId:guid}/reference")]
    public async Task<ActionResult<IReadOnlyList<ReferenceMaterialDto>>> GetReferenceMaterials(
        Guid skillId)
    {
        var referenceMaterials =
            await referenceService.GetReferenceMaterialsForSkillAsync(skillId);
        return Ok(referenceMaterials);
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
