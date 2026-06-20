using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Learning.Common.Extensions;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Features.SkillTree.Services.Abstract;

namespace Sellevate.Learning.Features.SkillTree.Endpoints;

[ApiController]
[Route("skill-tree")]
[Authorize]
public sealed class SkillTreeController(ISkillTreeService skillTreeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SkillTreeResponseDto>> GetSkillTree(CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId))
            return Unauthorized();

        var skillTreeResponse = await skillTreeService.GetSkillTreeForUserAsync(userId, cancellationToken);
        return Ok(skillTreeResponse);
    }
}
