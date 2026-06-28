using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Learning.Common.Extensions;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Features.SkillTree.Services.Abstract;

namespace Sellevate.Learning.Features.SkillTree.Endpoints;

[ApiController]
[Route("skills")]
[Authorize]
public sealed class SkillsController(ISkillTreeService skillTreeService, IExerciseService exerciseService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SkillTreeNodeDto>>> GetAllSkills(CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId))
        {
            var skills = await skillTreeService.GetAllSkillsAsync(cancellationToken);
            return Ok(skills);
        }

        var skillsWithProgress = await skillTreeService.GetAllSkillsWithProgressAsync(userId, cancellationToken);
        return Ok(skillsWithProgress);
    }

    [HttpGet("stages")]
    public async Task<ActionResult<IReadOnlyList<SkillStageDto>>> GetStages(CancellationToken cancellationToken)
    {
        var stages = await skillTreeService.GetStagesAsync(cancellationToken);
        return Ok(stages);
    }

    [HttpPut("enrolled")]
    public async Task<IActionResult> UpdateEnrolledSkills(
        [FromBody] UpdateEnrolledSkillsRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId))
            return Unauthorized();

        await skillTreeService.UpdateEnrolledSkillsAsync(
            userId,
            request.SkillSlugs ?? [],
            cancellationToken);

        return NoContent();
    }

    [HttpGet("{skillSlug}/lessons")]
    public async Task<ActionResult<IReadOnlyList<LessonSummaryDto>>> GetLessonsForSkill(string skillSlug, CancellationToken cancellationToken)
    {
        if (!User.TryResolveUserId(out var userId))
            return Unauthorized();

        var lessons = await exerciseService.GetLessonsForSkillAsync(userId, skillSlug, cancellationToken);

        if (lessons.Count == 0)
            return NotFound(new { message = $"Skill '{skillSlug}' not found or has no lessons." });

        return Ok(lessons);
    }

    [HttpGet("{skillId:guid}/topics")]
    public async Task<ActionResult<IReadOnlyList<TopicDto>>> GetTopicsForSkill(Guid skillId, CancellationToken cancellationToken)
    {
        var topics = await skillTreeService.GetTopicsForSkillAsync(skillId, cancellationToken);
        return Ok(topics);
    }
}
