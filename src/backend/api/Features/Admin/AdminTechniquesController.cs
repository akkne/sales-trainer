using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Techniques;
using SalesTrainer.Api.Features.Techniques.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

public sealed record AdminTechniqueCoachDto(
    string AvatarSeed,
    string Name,
    string Role,
    string Quote,
    JsonNode? Challenges);

public sealed record AdminTechniqueDto(
    Guid Id,
    string Slug,
    string Name,
    string Summary,
    string Body,
    string[] Tags,
    Guid? PrimarySkillId,
    string? PrimarySkillIconicName,
    string? PrimarySkillTitle,
    Guid[] AdditionalSkillIds,
    int Difficulty,
    string DifficultyName,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    JsonNode? Dialog,
    JsonNode? Case,
    AdminTechniqueCoachDto? Coach);

public sealed record AdminTechniqueWriteRequestDto(
    string Slug,
    string Name,
    string Summary,
    string Body,
    string[]? Tags,
    Guid? PrimarySkillId,
    Guid[]? AdditionalSkillIds,
    int Difficulty,
    int SortOrder,
    JsonNode? Dialog,
    JsonNode? Case,
    AdminTechniqueCoachDto? Coach);

public sealed record AdminTechniqueImportResultDto(
    int CreatedCount,
    int UpdatedCount,
    int FailedCount,
    string[] Errors);

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminTechniquesController(
    AppDbContext databaseContext,
    ILogger<AdminTechniquesController> logger) : ControllerBase
{
    [HttpGet("admin/techniques")]
    public async Task<ActionResult<List<AdminTechniqueDto>>> GetAll(
        [FromQuery] string? skill,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = databaseContext.Techniques.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(skill))
        {
            var matchingSkillId = await databaseContext.Skills.AsNoTracking()
                .Where(candidate => candidate.IconicName == skill)
                .Select(candidate => (Guid?)candidate.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (matchingSkillId is null)
                return Ok(new List<AdminTechniqueDto>());

            query = query.Where(technique => technique.PrimarySkillId == matchingSkillId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(technique =>
                technique.Name.ToLower().Contains(searchLower) ||
                technique.Summary.ToLower().Contains(searchLower) ||
                technique.Body.ToLower().Contains(searchLower));
        }

        var techniques = await query
            .Include(technique => technique.Coach)
            .Include(technique => technique.AdditionalSkills)
            .OrderBy(technique => technique.SortOrder)
            .ThenBy(technique => technique.Name)
            .ToListAsync(cancellationToken);

        var skillLookup = await LoadSkillLookupAsync(techniques, cancellationToken);

        return Ok(techniques.Select(technique => MapToDto(technique, skillLookup)).ToList());
    }

    [HttpGet("admin/techniques/{id:guid}")]
    public async Task<ActionResult<AdminTechniqueDto>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var technique = await LoadTechniqueAsync(id, cancellationToken);
        if (technique is null) return NotFound();

        var skillLookup = await LoadSkillLookupAsync(new[] { technique }, cancellationToken);
        return Ok(MapToDto(technique, skillLookup));
    }

    [HttpPost("admin/techniques")]
    public async Task<ActionResult<AdminTechniqueDto>> Create(
        [FromBody] AdminTechniqueWriteRequestDto payload,
        CancellationToken cancellationToken)
    {
        var validationError = await ValidatePayloadAsync(payload, existingTechniqueId: null, cancellationToken);
        if (validationError is not null) return validationError;

        var technique = new Technique
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        ApplyPayload(technique, payload);

        databaseContext.Techniques.Add(technique);
        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Technique created TechniqueId={TechniqueId} Slug={Slug} by ActorId={ActorId}",
            technique.Id, technique.Slug, User.FindFirstValue(ClaimTypes.NameIdentifier));

        var refreshed = await LoadTechniqueAsync(technique.Id, cancellationToken);
        var skillLookup = await LoadSkillLookupAsync(new[] { refreshed! }, cancellationToken);
        return Ok(MapToDto(refreshed!, skillLookup));
    }

    [HttpPut("admin/techniques/{id:guid}")]
    public async Task<ActionResult<AdminTechniqueDto>> Update(
        Guid id,
        [FromBody] AdminTechniqueWriteRequestDto payload,
        CancellationToken cancellationToken)
    {
        var technique = await LoadTechniqueAsync(id, cancellationToken);
        if (technique is null) return NotFound();

        var validationError = await ValidatePayloadAsync(payload, existingTechniqueId: id, cancellationToken);
        if (validationError is not null) return validationError;

        databaseContext.TechniqueSkills.RemoveRange(technique.AdditionalSkills);
        if (technique.Coach is not null)
            databaseContext.TechniqueCoaches.Remove(technique.Coach);

        technique.AdditionalSkills = new List<TechniqueSkill>();
        technique.Coach = null;
        technique.UpdatedAt = DateTime.UtcNow;

        ApplyPayload(technique, payload);

        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Technique updated TechniqueId={TechniqueId} Slug={Slug} by ActorId={ActorId}",
            technique.Id, technique.Slug, User.FindFirstValue(ClaimTypes.NameIdentifier));

        var refreshed = await LoadTechniqueAsync(id, cancellationToken);
        var skillLookup = await LoadSkillLookupAsync(new[] { refreshed! }, cancellationToken);
        return Ok(MapToDto(refreshed!, skillLookup));
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

    [HttpPost("admin/techniques/import")]
    public async Task<ActionResult<AdminTechniqueImportResultDto>> Import(
        [FromBody] AdminTechniqueWriteRequestDto[] payload,
        CancellationToken cancellationToken)
    {
        var createdCount = 0;
        var updatedCount = 0;
        var failedCount = 0;
        var errors = new List<string>();

        foreach (var item in payload)
        {
            try
            {
                var existing = await databaseContext.Techniques
                    .Include(technique => technique.Coach)
                    .Include(technique => technique.AdditionalSkills)
                    .FirstOrDefaultAsync(technique => technique.Slug == item.Slug, cancellationToken);

                var isNewRecord = existing is null;
                var validationError = await ValidatePayloadAsync(
                    item,
                    existingTechniqueId: existing?.Id,
                    cancellationToken);
                if (validationError is not null)
                {
                    failedCount++;
                    errors.Add($"{item.Slug}: validation failed.");
                    continue;
                }

                if (isNewRecord)
                {
                    existing = new Technique
                    {
                        Id = Guid.NewGuid(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    };
                    ApplyPayload(existing, item);
                    databaseContext.Techniques.Add(existing);
                    createdCount++;
                }
                else
                {
                    databaseContext.TechniqueSkills.RemoveRange(existing!.AdditionalSkills);
                    if (existing.Coach is not null)
                        databaseContext.TechniqueCoaches.Remove(existing.Coach);

                    existing.AdditionalSkills = new List<TechniqueSkill>();
                    existing.Coach = null;
                    existing.UpdatedAt = DateTime.UtcNow;

                    ApplyPayload(existing, item);
                    updatedCount++;
                }

                await databaseContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                failedCount++;
                errors.Add($"{item.Slug}: {exception.Message}");
                logger.LogError(exception,
                    "Technique import row failed Slug={Slug}", item.Slug);
            }
        }

        logger.LogInformation(
            "Technique import finished Created={Created} Updated={Updated} Failed={Failed} by ActorId={ActorId}",
            createdCount, updatedCount, failedCount, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminTechniqueImportResultDto(
            createdCount, updatedCount, failedCount, errors.ToArray()));
    }

    private Task<Technique?> LoadTechniqueAsync(Guid id, CancellationToken cancellationToken)
    {
        return databaseContext.Techniques
            .Include(technique => technique.Coach)
            .Include(technique => technique.AdditionalSkills)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, SkillProjection>> LoadSkillLookupAsync(
        IReadOnlyCollection<Technique> techniques,
        CancellationToken cancellationToken)
    {
        var skillIds = techniques
            .SelectMany(technique => new[] { technique.PrimarySkillId }
                .Concat(technique.AdditionalSkills.Select(link => (Guid?)link.SkillId)))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        if (skillIds.Length == 0)
            return new Dictionary<Guid, SkillProjection>();

        return await databaseContext.Skills.AsNoTracking()
            .Where(skill => skillIds.Contains(skill.Id))
            .Select(skill => new SkillProjection(skill.Id, skill.IconicName, skill.Title))
            .ToDictionaryAsync(projection => projection.Id, cancellationToken);
    }

    private async Task<ActionResult?> ValidatePayloadAsync(
        AdminTechniqueWriteRequestDto payload,
        Guid? existingTechniqueId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Slug) || string.IsNullOrWhiteSpace(payload.Name))
            return BadRequest(new { error = "Slug and Name are required." });

        if (payload.Difficulty is < TechniqueLevels.Novice or > TechniqueLevels.Master)
            return BadRequest(new { error = "Difficulty must be between 1 and 4." });

        var slugClashExists = await databaseContext.Techniques.AnyAsync(
            candidate => candidate.Slug == payload.Slug
                         && (existingTechniqueId == null || candidate.Id != existingTechniqueId),
            cancellationToken);

        if (slugClashExists)
            return Conflict(new { error = "Slug already exists." });

        if (payload.PrimarySkillId.HasValue)
        {
            var primarySkillExists = await databaseContext.Skills.AnyAsync(
                skill => skill.Id == payload.PrimarySkillId, cancellationToken);
            if (!primarySkillExists)
                return BadRequest(new { error = "Unknown PrimarySkillId." });
        }

        return null;
    }

    private static void ApplyPayload(Technique technique, AdminTechniqueWriteRequestDto payload)
    {
        technique.Slug = payload.Slug;
        technique.Name = payload.Name;
        technique.Summary = payload.Summary ?? string.Empty;
        technique.Body = payload.Body ?? string.Empty;
        technique.Tags = payload.Tags ?? Array.Empty<string>();
        technique.PrimarySkillId = payload.PrimarySkillId;
        technique.Difficulty = payload.Difficulty;
        technique.SortOrder = payload.SortOrder;
        technique.DialogJson = SerializeNullable(payload.Dialog);
        technique.CaseJson = SerializeNullable(payload.Case);

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
                ChallengesJson = SerializeNullable(payload.Coach.Challenges),
            };
        }
    }

    private static string? SerializeNullable(JsonNode? node)
    {
        return node is null ? null : node.ToJsonString(CompactJsonOptions);
    }

    private static AdminTechniqueDto MapToDto(
        Technique technique,
        IReadOnlyDictionary<Guid, SkillProjection> skillLookup)
    {
        SkillProjection? primarySkill = null;
        if (technique.PrimarySkillId.HasValue &&
            skillLookup.TryGetValue(technique.PrimarySkillId.Value, out var resolved))
        {
            primarySkill = resolved;
        }

        return new AdminTechniqueDto(
            technique.Id,
            technique.Slug,
            technique.Name,
            technique.Summary,
            technique.Body,
            technique.Tags,
            technique.PrimarySkillId,
            primarySkill?.IconicName,
            primarySkill?.Title,
            technique.AdditionalSkills.Select(link => link.SkillId).ToArray(),
            technique.Difficulty,
            TechniqueLevels.ResolveDifficultyName(technique.Difficulty),
            technique.SortOrder,
            technique.CreatedAt,
            technique.UpdatedAt,
            ParseNullable(technique.DialogJson),
            ParseNullable(technique.CaseJson),
            technique.Coach is null
                ? null
                : new AdminTechniqueCoachDto(
                    technique.Coach.AvatarSeed,
                    technique.Coach.Name,
                    technique.Coach.Role,
                    technique.Coach.Quote,
                    ParseNullable(technique.Coach.ChallengesJson)));
    }

    private static JsonNode? ParseNullable(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        try
        {
            return JsonNode.Parse(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions CompactJsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record SkillProjection(Guid Id, string IconicName, string Title);
}
