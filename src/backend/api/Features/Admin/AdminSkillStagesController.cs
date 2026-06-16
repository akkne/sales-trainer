using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Admin.Models;
using SalesTrainer.Api.Features.SkillTree.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

[ApiController]
[Route("admin/skill-stages")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminSkillStagesController(AppDbContext database, ILogger<AdminSkillStagesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdminSkillStageDto>>> GetAll()
    {
        var stages = await database.SkillStages
            .OrderBy(s => s.Order)
            .Select(s => new AdminSkillStageDto(s.Id, s.Key, s.Label, s.Accent, s.Order))
            .ToListAsync();
        return Ok(stages);
    }

    [HttpPost]
    public async Task<ActionResult<AdminSkillStageDto>> Create([FromBody] CreateSkillStageRequestDto request)
    {
        var key = (request.Key ?? string.Empty).Trim().ToLowerInvariant();
        var validation = ValidateFields(key, request.Label, request.Accent);
        if (validation is not null) return BadRequest(new { message = validation });

        if (await database.SkillStages.AnyAsync(s => s.Key == key))
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
        await database.SaveChangesAsync();

        logger.LogInformation("Skill stage created Key={Key} by ActorId={ActorId}",
            stage.Key, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminSkillStageDto(stage.Id, stage.Key, stage.Label, stage.Accent, stage.Order));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminSkillStageDto>> Update(Guid id, [FromBody] UpdateSkillStageRequestDto request)
    {
        // The key (slug) is immutable: it is stored on every Skill row, so renaming
        // it would orphan the grouping. Only label, accent, and order are editable.
        var validation = ValidateFields(key: "x", request.Label, request.Accent);
        if (validation is not null) return BadRequest(new { message = validation });

        var stage = await database.SkillStages.FirstOrDefaultAsync(s => s.Id == id);
        if (stage is null) return NotFound();

        stage.Label = request.Label!.Trim();
        stage.Accent = request.Accent!.Trim();
        stage.Order = request.Order;
        await database.SaveChangesAsync();

        logger.LogInformation("Skill stage updated Key={Key} by ActorId={ActorId}",
            stage.Key, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminSkillStageDto(stage.Id, stage.Key, stage.Label, stage.Accent, stage.Order));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var stage = await database.SkillStages.FirstOrDefaultAsync(s => s.Id == id);
        if (stage is null) return NotFound();

        if (await database.Skills.AnyAsync(s => s.Stage == stage.Key))
            return BadRequest(new { message = "Cannot delete a stage that still has skills assigned; reassign them first" });

        database.SkillStages.Remove(stage);
        await database.SaveChangesAsync();

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
