using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Learning.Features.Reference.Models;
using Sellevate.Learning.Features.Reference.Services.Abstract;

namespace Sellevate.Learning.Features.Reference;

[ApiController]
[Authorize]
public sealed class ReferenceController(IReferenceService referenceService) : ControllerBase
{
    [HttpGet("skills/{skillId:guid}/reference")]
    public async Task<ActionResult<IReadOnlyList<ReferenceMaterialDto>>> GetReferenceMaterials(
        Guid skillId,
        CancellationToken cancellationToken)
    {
        var referenceMaterials =
            await referenceService.GetReferenceMaterialsForSkillAsync(skillId, cancellationToken);
        return Ok(referenceMaterials);
    }

    [HttpGet("reference")]
    public async Task<ActionResult<IReadOnlyList<ReferenceMaterialDto>>> GetAllReferenceMaterials(
        [FromQuery] string? category,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var materials = await referenceService.GetAllReferenceMaterialsAsync(category, search, cancellationToken);
        return Ok(materials);
    }

    [HttpGet("reference/categories")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await referenceService.GetAllCategoriesAsync(cancellationToken);
        return Ok(categories);
    }
}
