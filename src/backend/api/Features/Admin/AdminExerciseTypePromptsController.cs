using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

public record ExerciseTypePromptDto(
    Guid Id,
    string ExerciseType,
    string SystemPrompt,
    DateTime UpdatedAt
);

public record UpdateExerciseTypePromptRequestDto(
    string SystemPrompt
);

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public class AdminExerciseTypePromptsController(AppDbContext db, ILogger<AdminExerciseTypePromptsController> logger) : ControllerBase
{
    [HttpGet("admin/exercise-type-prompts")]
    public async Task<ActionResult<List<ExerciseTypePromptDto>>> GetAll()
    {
        var prompts = await db.ExerciseTypePrompts
            .OrderBy(p => p.ExerciseType)
            .Select(p => new ExerciseTypePromptDto(p.Id, p.ExerciseType, p.SystemPrompt, p.UpdatedAt))
            .ToListAsync();

        return Ok(prompts);
    }

    [HttpGet("admin/exercise-type-prompts/{exerciseType}")]
    public async Task<ActionResult<ExerciseTypePromptDto>> GetByType(string exerciseType)
    {
        var prompt = await db.ExerciseTypePrompts
            .FirstOrDefaultAsync(p => p.ExerciseType == exerciseType);

        if (prompt is null) return NotFound();

        return Ok(new ExerciseTypePromptDto(prompt.Id, prompt.ExerciseType, prompt.SystemPrompt, prompt.UpdatedAt));
    }

    [HttpPut("admin/exercise-type-prompts/{exerciseType}")]
    public async Task<ActionResult<ExerciseTypePromptDto>> Update(
        string exerciseType, [FromBody] UpdateExerciseTypePromptRequestDto dto)
    {
        var prompt = await db.ExerciseTypePrompts
            .FirstOrDefaultAsync(p => p.ExerciseType == exerciseType);

        if (prompt is null)
        {
            // Create new if doesn't exist
            prompt = new ExerciseTypePrompt
            {
                Id = Guid.NewGuid(),
                ExerciseType = exerciseType,
                SystemPrompt = dto.SystemPrompt,
                UpdatedAt = DateTime.UtcNow
            };
            db.ExerciseTypePrompts.Add(prompt);
        }
        else
        {
            prompt.SystemPrompt = dto.SystemPrompt;
            prompt.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        logger.LogInformation("ExerciseTypePrompt updated ExerciseType={ExerciseType} by ActorId={ActorId}",
            exerciseType, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new ExerciseTypePromptDto(prompt.Id, prompt.ExerciseType, prompt.SystemPrompt, prompt.UpdatedAt));
    }
}
