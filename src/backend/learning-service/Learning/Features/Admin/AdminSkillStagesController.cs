using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Admin.Models;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Admin;

[ApiController]
[Route("admin/skill-stages")]
[Authorize(Policy = AuthorizationPolicies.RequireAdministrator)]
public sealed class AdminSkillStagesController(LearningDbContext database, ILogger<AdminSkillStagesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminSkillStageDto>>> GetAll(CancellationToken cancellationToken = default)
    {
        var stages = await database.SkillStages
            .OrderBy(stage => stage.Order)
            .Select(stage => new AdminSkillStageDto(stage.Id, stage.Key, stage.Label, stage.Accent, stage.Order))
            .ToListAsync(cancellationToken);
        return Ok(stages);
    }

    [HttpPost]
    public async Task<ActionResult<AdminSkillStageDto>> Create([FromBody] CreateSkillStageRequestDto request, CancellationToken cancellationToken = default)
    {
        var key = (request.Key ?? string.Empty).Trim().ToLowerInvariant();
        var validation = ValidateFields(key, request.Label, request.Accent);
        if (validation is not null) return BadRequest(new { message = validation });

        if (await database.SkillStages.AnyAsync(stage => stage.Key == key, cancellationToken))
            return BadRequest(new { message = $"Stage with key '{key}' already exists" });

        var stage = new SkillStage
        {
            Id = Guid.NewGuid(),
            Key = key,
            Label = request.Label!.Trim(),
            Accent = request.Accent!.Trim(),
            Order = request.Order
        };
        database.SkillStages.Add(stage);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Skill stage created Key={Key} by ActorId={ActorId}",
            stage.Key, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminSkillStageDto(stage.Id, stage.Key, stage.Label, stage.Accent, stage.Order));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminSkillStageDto>> Update(Guid id, [FromBody] UpdateSkillStageRequestDto request, CancellationToken cancellationToken = default)
    {
        var validation = ValidateFields(key: "x", request.Label, request.Accent);
        if (validation is not null) return BadRequest(new { message = validation });

        var stage = await database.SkillStages.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (stage is null) return NotFound();

        stage.Label = request.Label!.Trim();
        stage.Accent = request.Accent!.Trim();
        stage.Order = request.Order;
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Skill stage updated Key={Key} by ActorId={ActorId}",
            stage.Key, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminSkillStageDto(stage.Id, stage.Key, stage.Label, stage.Accent, stage.Order));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var stage = await database.SkillStages.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (stage is null) return NotFound();

        if (await database.Skills.AnyAsync(skill => skill.Stage == stage.Key, cancellationToken))
            return BadRequest(new { message = "Cannot delete a stage that still has skills assigned; reassign them first" });

        database.SkillStages.Remove(stage);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Skill stage deleted Key={Key} by ActorId={ActorId}",
            stage.Key, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }

    private static string? ValidateFields(string key, string? label, string? accent)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "Key is required";
        if (string.IsNullOrWhiteSpace(label))
            return "Label is required";
        if (string.IsNullOrWhiteSpace(accent))
            return "Accent is required";
        return null;
    }
}
