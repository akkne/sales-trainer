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

[ApiController]
[Route("skills")]
[Authorize]
public class SkillsController(SkillTreeService skillTreeService) : ControllerBase
{
    /// <summary>
    /// Returns every skill in the system with the current user's progress.
    /// Skills the user has not unlocked yet are returned with status "locked".
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SkillTreeNodeDto>>> GetAllSkills()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(rawUserId, out var userId))
            return Unauthorized();

        var skills = await skillTreeService.GetAllSkillsForUserAsync(userId);
        return Ok(skills);
    }
}
