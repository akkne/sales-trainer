using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Techniques.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

public sealed record AdminTechniqueDialogTurnDto(
    int OrderIndex,
    string Side,
    string Text,
    string? AnnotationsJson);

public sealed record AdminTechniqueCaseDto(
    int OrderIndex,
    string Title,
    string Body,
    string? MetricsJson);

public sealed record AdminTechniqueCoachDto(
    string AvatarSeed,
    string Name,
    string Role,
    string Quote,
    string? ChallengesJson);

public sealed record AdminTechniqueDto(
    Guid Id,
    string Slug,
    string Name,
    string Summary,
    string Body,
    string CategorySlug,
    string[] Tags,
    Guid? PrimarySkillId,
    Guid[] AdditionalSkillIds,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    AdminTechniqueDialogTurnDto[] DialogTurns,
    AdminTechniqueCaseDto[] Cases,
    AdminTechniqueCoachDto? Coach);

public sealed record AdminTechniqueWriteRequestDto(
    string Slug,
    string Name,
    string Summary,
    string Body,
    string CategorySlug,
    string[] Tags,
    Guid? PrimarySkillId,
    Guid[] AdditionalSkillIds,
    int SortOrder,
    AdminTechniqueDialogTurnDto[] DialogTurns,
    AdminTechniqueCaseDto[] Cases,
    AdminTechniqueCoachDto? Coach);

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminTechniquesController(
    AppDbContext databaseContext,
    ILogger<AdminTechniquesController> logger) : ControllerBase
{
    [HttpGet("admin/techniques")]
    public async Task<ActionResult<List<AdminTechniqueDto>>> GetAll(
        [FromQuery] string? category,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = databaseContext.Techniques.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(technique => technique.CategorySlug == category);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(technique =>
                technique.Name.ToLower().Contains(searchLower) ||
                technique.Summary.ToLower().Contains(searchLower) ||
                technique.Body.ToLower().Contains(searchLower));
        }

        var techniques = await query
            .Include(technique => technique.DialogTurns)
            .Include(technique => technique.Cases)
            .Include(technique => technique.Coach)
            .Include(technique => technique.AdditionalSkills)
            .OrderBy(technique => technique.SortOrder)
            .ThenBy(technique => technique.Name)
            .ToListAsync(cancellationToken);

        return Ok(techniques.Select(MapToDto).ToList());
    }

    [HttpGet("admin/technique-categories")]
    public async Task<ActionResult<List<TechniqueCategoryDto>>> GetCategories(
        CancellationToken cancellationToken)
    {
        var categories = await databaseContext.TechniqueCategories.AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .Select(category => new TechniqueCategoryDto(
                category.Slug, category.Label, category.Color, category.SortOrder))
            .ToListAsync(cancellationToken);
        return Ok(categories);
    }

    [HttpGet("admin/techniques/{id:guid}")]
    public async Task<ActionResult<AdminTechniqueDto>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var technique = await LoadTechniqueAsync(id, cancellationToken);
        if (technique is null) return NotFound();
        return Ok(MapToDto(technique));
    }

    [HttpPost("admin/techniques")]
    public async Task<ActionResult<AdminTechniqueDto>> Create(
        [FromBody] AdminTechniqueWriteRequestDto payload,
        CancellationToken cancellationToken)
    {
        if (await databaseContext.Techniques.AnyAsync(
                candidate => candidate.Slug == payload.Slug, cancellationToken))
        {
            return Conflict(new { error = "Slug already exists." });
        }

        if (!await databaseContext.TechniqueCategories.AnyAsync(
                category => category.Slug == payload.CategorySlug, cancellationToken))
        {
            return BadRequest(new { error = "Unknown category slug." });
        }

        var technique = new Technique
        {
            Id = Guid.NewGuid(),
            Slug = payload.Slug,
            Name = payload.Name,
            Summary = payload.Summary,
            Body = payload.Body,
            CategorySlug = payload.CategorySlug,
            Tags = payload.Tags,
            PrimarySkillId = payload.PrimarySkillId,
            SortOrder = payload.SortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        ApplyNestedFromPayload(technique, payload);

        databaseContext.Techniques.Add(technique);
        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Technique created TechniqueId={TechniqueId} Slug={Slug} by ActorId={ActorId}",
            technique.Id, technique.Slug, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(MapToDto(technique));
    }

    [HttpPut("admin/techniques/{id:guid}")]
    public async Task<ActionResult<AdminTechniqueDto>> Update(
        Guid id,
        [FromBody] AdminTechniqueWriteRequestDto payload,
        CancellationToken cancellationToken)
    {
        var technique = await LoadTechniqueAsync(id, cancellationToken);
        if (technique is null) return NotFound();

        if (technique.Slug != payload.Slug &&
            await databaseContext.Techniques.AnyAsync(
                candidate => candidate.Slug == payload.Slug, cancellationToken))
        {
            return Conflict(new { error = "Slug already exists." });
        }

        if (!await databaseContext.TechniqueCategories.AnyAsync(
                category => category.Slug == payload.CategorySlug, cancellationToken))
        {
            return BadRequest(new { error = "Unknown category slug." });
        }

        technique.Slug = payload.Slug;
        technique.Name = payload.Name;
        technique.Summary = payload.Summary;
        technique.Body = payload.Body;
        technique.CategorySlug = payload.CategorySlug;
        technique.Tags = payload.Tags;
        technique.PrimarySkillId = payload.PrimarySkillId;
        technique.SortOrder = payload.SortOrder;
        technique.UpdatedAt = DateTime.UtcNow;

        databaseContext.TechniqueDialogTurns.RemoveRange(technique.DialogTurns);
        databaseContext.TechniqueCases.RemoveRange(technique.Cases);
        databaseContext.TechniqueSkills.RemoveRange(technique.AdditionalSkills);
        if (technique.Coach is not null)
            databaseContext.TechniqueCoaches.Remove(technique.Coach);

        technique.DialogTurns = new List<TechniqueDialogTurn>();
        technique.Cases = new List<TechniqueCase>();
        technique.AdditionalSkills = new List<TechniqueSkill>();
        technique.Coach = null;

        ApplyNestedFromPayload(technique, payload);

        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Technique updated TechniqueId={TechniqueId} Slug={Slug} by ActorId={ActorId}",
            technique.Id, technique.Slug, User.FindFirstValue(ClaimTypes.NameIdentifier));

        var refreshed = await LoadTechniqueAsync(id, cancellationToken);
        return Ok(MapToDto(refreshed!));
    }

    [HttpDelete("admin/techniques/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var technique = await databaseContext.Techniques
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (technique is null) return NotFound();

        databaseContext.Techniques.Remove(technique);
        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Technique deleted TechniqueId={TechniqueId} by ActorId={ActorId}",
            id, User.FindFirstValue(ClaimTypes.NameIdentifier));
        return NoContent();
    }

    private Task<Technique?> LoadTechniqueAsync(Guid id, CancellationToken cancellationToken)
    {
        return databaseContext.Techniques
            .Include(technique => technique.DialogTurns)
            .Include(technique => technique.Cases)
            .Include(technique => technique.Coach)
            .Include(technique => technique.AdditionalSkills)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
    }

    private static void ApplyNestedFromPayload(Technique technique, AdminTechniqueWriteRequestDto payload)
    {
        foreach (var turn in payload.DialogTurns ?? Array.Empty<AdminTechniqueDialogTurnDto>())
        {
            technique.DialogTurns.Add(new TechniqueDialogTurn
            {
                Id = Guid.NewGuid(),
                TechniqueId = technique.Id,
                OrderIndex = turn.OrderIndex,
                Side = turn.Side,
                Text = turn.Text,
                AnnotationsJson = NormalizeJson(turn.AnnotationsJson),
            });
        }

        foreach (var techniqueCase in payload.Cases ?? Array.Empty<AdminTechniqueCaseDto>())
        {
            technique.Cases.Add(new TechniqueCase
            {
                Id = Guid.NewGuid(),
                TechniqueId = technique.Id,
                OrderIndex = techniqueCase.OrderIndex,
                Title = techniqueCase.Title,
                Body = techniqueCase.Body,
                MetricsJson = NormalizeJson(techniqueCase.MetricsJson),
            });
        }

        foreach (var skillId in (payload.AdditionalSkillIds ?? Array.Empty<Guid>()).Distinct())
        {
            technique.AdditionalSkills.Add(new TechniqueSkill
            {
                TechniqueId = technique.Id,
                SkillId = skillId,
            });
        }

        if (payload.Coach is not null)
        {
            technique.Coach = new TechniqueCoach
            {
                Id = Guid.NewGuid(),
                TechniqueId = technique.Id,
                AvatarSeed = payload.Coach.AvatarSeed,
                Name = payload.Coach.Name,
                Role = payload.Coach.Role,
                Quote = payload.Coach.Quote,
                ChallengesJson = NormalizeJson(payload.Coach.ChallengesJson),
            };
        }
    }

    private static string? NormalizeJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            using var parsed = JsonDocument.Parse(raw);
            return parsed.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AdminTechniqueDto MapToDto(Technique technique)
    {
        return new AdminTechniqueDto(
            technique.Id,
            technique.Slug,
            technique.Name,
            technique.Summary,
            technique.Body,
            technique.CategorySlug,
            technique.Tags,
            technique.PrimarySkillId,
            technique.AdditionalSkills.Select(link => link.SkillId).ToArray(),
            technique.SortOrder,
            technique.CreatedAt,
            technique.UpdatedAt,
            technique.DialogTurns
                .OrderBy(turn => turn.OrderIndex)
                .Select(turn => new AdminTechniqueDialogTurnDto(
                    turn.OrderIndex, turn.Side, turn.Text, turn.AnnotationsJson))
                .ToArray(),
            technique.Cases
                .OrderBy(techniqueCase => techniqueCase.OrderIndex)
                .Select(techniqueCase => new AdminTechniqueCaseDto(
                    techniqueCase.OrderIndex,
                    techniqueCase.Title,
                    techniqueCase.Body,
                    techniqueCase.MetricsJson))
                .ToArray(),
            technique.Coach is null
                ? null
                : new AdminTechniqueCoachDto(
                    technique.Coach.AvatarSeed,
                    technique.Coach.Name,
                    technique.Coach.Role,
                    technique.Coach.Quote,
                    technique.Coach.ChallengesJson));
    }
}
