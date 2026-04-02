using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SalesTrainer.Api.Features.SkillTree;

[ApiController]
[Route("skill-tree")]
[Authorize]
public class SkillTreeController(SkillTreeService skillTreeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SkillTreeResponseDto>> GetSkillTree()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(rawUserId, out var userId))
            return Unauthorized();

        var skillTreeResponse = await skillTreeService.GetSkillTreeForUserAsync(userId);
        return Ok(skillTreeResponse);
    }
}
