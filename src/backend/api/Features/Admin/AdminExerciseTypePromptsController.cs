using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminExerciseTypePromptsController(AppDbContext database, ILogger<AdminExerciseTypePromptsController> logger) : ControllerBase
{
    [HttpGet("admin/exercise-type-prompts")]
    public async Task<ActionResult<List<ExerciseTypePromptDto>>> GetAll()
    {
        var prompts = await database.ExerciseTypePrompts
            .OrderBy(p => p.ExerciseType)
            .Select(p => new ExerciseTypePromptDto(p.Id, p.ExerciseType, p.SystemPrompt, p.UpdatedAt))
            .ToListAsync();

        return Ok(prompts);
    }

    [HttpGet("admin/exercise-type-prompts/{exerciseType}")]
    public async Task<ActionResult<ExerciseTypePromptDto>> GetByType(string exerciseType)
    {
        var prompt = await database.ExerciseTypePrompts
            .FirstOrDefaultAsync(p => p.ExerciseType == exerciseType);

        if (prompt is null) return NotFound();

        return Ok(new ExerciseTypePromptDto(prompt.Id, prompt.ExerciseType, prompt.SystemPrompt, prompt.UpdatedAt));
    }

    [HttpPut("admin/exercise-type-prompts/{exerciseType}")]
    public async Task<ActionResult<ExerciseTypePromptDto>> Update(
        string exerciseType, [FromBody] UpdateExerciseTypePromptRequestDto dto)
    {
        var prompt = await database.ExerciseTypePrompts
            .FirstOrDefaultAsync(p => p.ExerciseType == exerciseType);

        if (prompt is null)
        {
            prompt = new ExerciseTypePrompt
            {
                Id = Guid.NewGuid(),
                ExerciseType = exerciseType,
                SystemPrompt = dto.SystemPrompt,
                UpdatedAt = DateTime.UtcNow
            };
            database.ExerciseTypePrompts.Add(prompt);
        }
        else
        {
            prompt.SystemPrompt = dto.SystemPrompt;
            prompt.UpdatedAt = DateTime.UtcNow;
        }

        await database.SaveChangesAsync();

        logger.LogInformation("ExerciseTypePrompt updated ExerciseType={ExerciseType} by ActorId={ActorId}",
            exerciseType, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new ExerciseTypePromptDto(prompt.Id, prompt.ExerciseType, prompt.SystemPrompt, prompt.UpdatedAt));
    }
}
