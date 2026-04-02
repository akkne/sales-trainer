using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SalesTrainer.Api.Features.Reference;

[ApiController]
[Route("skills/{skillSlug}/reference")]
[Authorize]
public class ReferenceController(ReferenceService referenceService) : ControllerBase
{
    [HttpGet]
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
}
