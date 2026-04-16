using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;
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
public class SkillsController(ISkillTreeService skillTreeService, IExerciseService exerciseService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SkillTreeNodeDto>>> GetAllSkills()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(rawUserId, out var userId))
        {
            // Return skills without progress for unauthenticated (shouldn't happen with [Authorize])
            var skills = await skillTreeService.GetAllSkillsAsync();
            return Ok(skills);
        }

        var skillsWithProgress = await skillTreeService.GetAllSkillsWithProgressAsync(userId);
        return Ok(skillsWithProgress);
    }

    [HttpGet("{skillSlug}/lessons")]
    public async Task<ActionResult<IReadOnlyList<LessonSummaryDto>>> GetLessonsForSkill(string skillSlug)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(rawUserId, out var userId))
            return Unauthorized();

        var lessons = await exerciseService.GetLessonsForSkillAsync(userId, skillSlug);

        if (lessons.Count == 0)
            return NotFound(new { message = $"Skill '{skillSlug}' not found or has no lessons." });

        return Ok(lessons);
    }

    [HttpGet("{skillId:guid}/topics")]
    public async Task<ActionResult<IReadOnlyList<TopicDto>>> GetTopicsForSkill(Guid skillId)
    {
        var topics = await skillTreeService.GetTopicsForSkillAsync(skillId);
        return Ok(topics);
    }
}
