using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Admin;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.RequireAdministrator)]
public sealed class AdminExerciseTypePromptsController(LearningDbContext database, ILogger<AdminExerciseTypePromptsController> logger) : ControllerBase
{
    [HttpGet("admin/exercise-type-prompts")]
    public async Task<ActionResult<IReadOnlyList<ExerciseTypePromptDto>>> GetAll(CancellationToken cancellationToken = default)
    {
        var prompts = await database.ExerciseTypePrompts
            .OrderBy(prompt => prompt.ExerciseType)
            .Select(prompt => new ExerciseTypePromptDto(prompt.Id, prompt.ExerciseType, prompt.SystemPrompt, prompt.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(prompts);
    }

    [HttpGet("admin/exercise-type-prompts/{exerciseType}")]
    public async Task<ActionResult<ExerciseTypePromptDto>> GetByType(string exerciseType, CancellationToken cancellationToken = default)
    {
        var prompt = await database.ExerciseTypePrompts
            .FirstOrDefaultAsync(candidate => candidate.ExerciseType == exerciseType, cancellationToken);

        if (prompt is null) return NotFound();

        return Ok(new ExerciseTypePromptDto(prompt.Id, prompt.ExerciseType, prompt.SystemPrompt, prompt.UpdatedAt));
    }

    [HttpPut("admin/exercise-type-prompts/{exerciseType}")]
    public async Task<ActionResult<ExerciseTypePromptDto>> Update(
        string exerciseType, [FromBody] UpdateExerciseTypePromptRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var prompt = await database.ExerciseTypePrompts
            .FirstOrDefaultAsync(candidate => candidate.ExerciseType == exerciseType, cancellationToken);

        if (prompt is null)
        {
            prompt = new ExerciseTypePrompt
            {
                Id = Guid.NewGuid(),
                ExerciseType = exerciseType,
                SystemPrompt = requestDto.SystemPrompt,
                UpdatedAt = DateTime.UtcNow
            };
            database.ExerciseTypePrompts.Add(prompt);
        }
        else
        {
            prompt.SystemPrompt = requestDto.SystemPrompt;
            prompt.UpdatedAt = DateTime.UtcNow;
        }

        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("ExerciseTypePrompt updated ExerciseType={ExerciseType} by ActorId={ActorId}",
            exerciseType, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new ExerciseTypePromptDto(prompt.Id, prompt.ExerciseType, prompt.SystemPrompt, prompt.UpdatedAt));
    }
}
