using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Features.Reference.Models;
using Sellevate.Learning.Features.Reference.Services.Abstract;

namespace Sellevate.Learning.Features.Reference;

[ApiController]
[TenantScoped]
[Authorize]
public sealed class ReferenceController(IReferenceService referenceService) : ControllerBase
{
    /// <summary>
    /// Accepts either the skill's GUID or its slug (<c>IconicName</c>), matching the sibling
    /// <c>/skills/{id}/lessons</c> endpoint's dual acceptance instead of requiring callers to
    /// know a skill's GUID just because this route happens to sit next to it.
    /// </summary>
    [HttpGet("skills/{skillIdentifier}/reference")]
    public async Task<ActionResult<IReadOnlyList<ReferenceMaterialDto>>> GetReferenceMaterials(
        string skillIdentifier,
        CancellationToken cancellationToken)
    {
        var referenceMaterials =
            await referenceService.GetReferenceMaterialsForSkillAsync(skillIdentifier, cancellationToken);
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
