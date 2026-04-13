using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesTrainer.Api.Features.SkillTree.Models;
using SalesTrainer.Api.Features.SkillTree.Services.Abstract;

namespace SalesTrainer.Api.Features.SkillTree;

[ApiController]
[Route("skill-tree")]
[Authorize]
public class SkillTreeController(ISkillTreeService skillTreeService) : ControllerBase
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
public class SkillsController(ISkillTreeService skillTreeService) : ControllerBase
{
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

    [HttpPut("enrolled")]
    public async Task<IActionResult> UpdateEnrolledSkills(
        [FromBody] UpdateEnrolledSkillsRequestDto request)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(rawUserId, out var userId))
            return Unauthorized();

        await skillTreeService.UpdateEnrolledSkillsAsync(userId, request.SkillSlugs);
        return NoContent();
    }
}
