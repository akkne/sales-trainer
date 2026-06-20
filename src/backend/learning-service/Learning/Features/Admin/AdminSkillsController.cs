using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Admin;

[ApiController]
[Route("admin/skills")]
[Authorize(Policy = AuthorizationPolicies.RequireAdministrator)]
public sealed class AdminSkillsController(LearningDbContext database, ILogger<AdminSkillsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminSkillDto>>> GetAll(CancellationToken cancellationToken = default)
    {
        var skills = await database.Skills
            .OrderBy(skill => skill.OrderInTree)
            .Select(skill => new AdminSkillDto(skill.Id, skill.IconicName, skill.Title, skill.Description, skill.OrderInTree, skill.Stage))
            .ToListAsync(cancellationToken);

        return Ok(skills);
    }

    [HttpPost]
    public async Task<ActionResult<AdminSkillDto>> Create([FromBody] CreateSkillRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var exists = await database.Skills.AnyAsync(skill => skill.IconicName == requestDto.IconicName, cancellationToken);
        if (exists)
            return Conflict(new { message = $"Skill with iconicName '{requestDto.IconicName}' already exists." });

        var skill = new Skill
        {
            Id = Guid.NewGuid(),
            IconicName = requestDto.IconicName,
            Title = requestDto.Title,
            Description = requestDto.Description,
            OrderInTree = requestDto.OrderInTree,
            Stage = string.IsNullOrWhiteSpace(requestDto.Stage) ? "general" : requestDto.Stage.Trim()
        };

        database.Skills.Add(skill);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Skill created SkillId={SkillId} IconicName={IconicName} by ActorId={ActorId}",
            skill.Id, skill.IconicName, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminSkillDto(skill.Id, skill.IconicName, skill.Title, skill.Description, skill.OrderInTree, skill.Stage));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminSkillDto>> Update(
        Guid id, [FromBody] UpdateSkillRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var skill = await database.Skills.FindAsync([id], cancellationToken);
        if (skill is null) return NotFound();

        if (requestDto.IconicName is not null && requestDto.IconicName != skill.IconicName)
        {
            var exists = await database.Skills.AnyAsync(candidate => candidate.IconicName == requestDto.IconicName && candidate.Id != id, cancellationToken);
            if (exists)
                return Conflict(new { message = $"Skill with iconicName '{requestDto.IconicName}' already exists." });
            skill.IconicName = requestDto.IconicName;
        }

        if (requestDto.Title is not null) skill.Title = requestDto.Title;
        if (requestDto.Description is not null) skill.Description = requestDto.Description;
        if (requestDto.OrderInTree.HasValue) skill.OrderInTree = requestDto.OrderInTree.Value;
        if (!string.IsNullOrWhiteSpace(requestDto.Stage)) skill.Stage = requestDto.Stage.Trim();

        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Skill updated SkillId={SkillId} IconicName={IconicName} by ActorId={ActorId}",
            skill.Id, skill.IconicName, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminSkillDto(skill.Id, skill.IconicName, skill.Title, skill.Description, skill.OrderInTree, skill.Stage));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var skill = await database.Skills.FindAsync([id], cancellationToken);
        if (skill is null) return NotFound();

        database.Skills.Remove(skill);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Skill deleted SkillId={SkillId} Title={Title} by ActorId={ActorId}",
            id, skill.Title, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }
}
