using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.SkillTree.Models;
using SalesTrainer.Api.Features.Techniques.Models;
using SalesTrainer.Api.Features.Techniques.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Techniques.Services.Implementation;

internal sealed class TechniqueService(AppDbContext databaseContext) : ITechniqueService
{
    public async Task<IReadOnlyList<TechniqueCardDto>> GetTechniqueCardsAsync(
        Guid? currentUserId,
        string? categorySlug,
        string? searchTerm,
        string? tag,
        CancellationToken cancellationToken = default)
    {
        var techniquesQuery = databaseContext.Techniques.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(categorySlug))
            techniquesQuery = techniquesQuery.Where(technique => technique.CategorySlug == categorySlug);

        if (!string.IsNullOrWhiteSpace(tag))
            techniquesQuery = techniquesQuery.Where(technique => technique.Tags.Contains(tag!));

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchLower = searchTerm.ToLower();
            techniquesQuery = techniquesQuery.Where(technique =>
                technique.Name.ToLower().Contains(searchLower) ||
                technique.Summary.ToLower().Contains(searchLower) ||
                technique.Body.ToLower().Contains(searchLower));
        }

        var techniques = await techniquesQuery
            .OrderBy(technique => technique.SortOrder)
            .ThenBy(technique => technique.Name)
            .ToListAsync(cancellationToken);

        var categories = await databaseContext.TechniqueCategories.AsNoTracking()
            .ToDictionaryAsync(category => category.Slug, cancellationToken);

        var skillNameById = await LoadSkillIconicNamesAsync(
            techniques.Where(technique => technique.PrimarySkillId.HasValue)
                .Select(technique => technique.PrimarySkillId!.Value)
                .Distinct()
                .ToArray(),
            cancellationToken);

        var progressByTechniqueId = await LoadProgressAsync(
            currentUserId,
            techniques.Select(technique => technique.Id).ToArray(),
            cancellationToken);

        return techniques.Select(technique => BuildCardDto(
            technique,
            categories,
            skillNameById,
            progressByTechniqueId)).ToList();
    }

    public async Task<TechniqueDetailDto?> GetTechniqueBySlugAsync(
        string slug,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var technique = await databaseContext.Techniques.AsNoTracking()
            .Include(loadedTechnique => loadedTechnique.DialogTurns)
            .Include(loadedTechnique => loadedTechnique.Cases)
            .Include(loadedTechnique => loadedTechnique.Coach)
            .Include(loadedTechnique => loadedTechnique.AdditionalSkills)
            .FirstOrDefaultAsync(candidate => candidate.Slug == slug, cancellationToken);

        if (technique is null)
            return null;

        var categories = await databaseContext.TechniqueCategories.AsNoTracking()
            .ToDictionaryAsync(category => category.Slug, cancellationToken);

        var skillIds = technique.AdditionalSkills.Select(link => link.SkillId).ToList();
        if (technique.PrimarySkillId.HasValue)
            skillIds.Add(technique.PrimarySkillId.Value);

        var skillNameById = await LoadSkillIconicNamesAsync(skillIds.Distinct().ToArray(), cancellationToken);

        var progressByTechniqueId = await LoadProgressAsync(
            currentUserId,
            new[] { technique.Id },
            cancellationToken);

        var card = BuildCardDto(technique, categories, skillNameById, progressByTechniqueId);

        var skillIconicNames = skillIds
            .Select(skillId => skillNameById.GetValueOrDefault(skillId))
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .Distinct()
            .ToArray();

        return new TechniqueDetailDto(
            card,
            technique.Body,
            skillIconicNames,
            technique.DialogTurns
                .OrderBy(turn => turn.OrderIndex)
                .Select(turn => new TechniqueDialogTurnDto(
                    turn.OrderIndex,
                    turn.Side,
                    turn.Text,
                    DeserializeAnnotations(turn.AnnotationsJson)))
                .ToArray(),
            technique.Cases
                .OrderBy(techniqueCase => techniqueCase.OrderIndex)
                .Select(techniqueCase => new TechniqueCaseDto(
                    techniqueCase.OrderIndex,
                    techniqueCase.Title,
                    techniqueCase.Body,
                    DeserializeJsonObject(techniqueCase.MetricsJson)))
                .ToArray(),
            technique.Coach is null
                ? null
                : new TechniqueCoachDto(
                    technique.Coach.AvatarSeed,
                    technique.Coach.Name,
                    technique.Coach.Role,
                    technique.Coach.Quote,
                    DeserializeChallenges(technique.Coach.ChallengesJson)));
    }

    public async Task<TechniqueMetaDto> GetTechniqueMetaAsync(
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var categories = await databaseContext.TechniqueCategories.AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .Select(category => new TechniqueCategoryDto(
                category.Slug,
                category.Label,
                category.Color,
                category.SortOrder))
            .ToArrayAsync(cancellationToken);

        var totalCount = await databaseContext.Techniques.CountAsync(cancellationToken);

        var userCounts = new TechniqueUserCountsDto(0, 0, totalCount);

        if (currentUserId.HasValue)
        {
            var progressRecords = await databaseContext.UserTechniqueProgressRecords.AsNoTracking()
                .Where(progress => progress.UserId == currentUserId.Value)
                .ToListAsync(cancellationToken);

            var masteredCount = progressRecords.Count(progress => progress.Level >= TechniqueLevels.MasteredThresholdLevel);
            var masterCount = progressRecords.Count(progress => progress.Level >= TechniqueLevels.MasterThresholdLevel);
            var seenCount = progressRecords.Count;
            var unseenCount = Math.Max(0, totalCount - seenCount);

            userCounts = new TechniqueUserCountsDto(masteredCount, masterCount, unseenCount);
        }

        return new TechniqueMetaDto(categories, totalCount, userCounts);
    }

    public async Task MarkTechniqueSeenAsync(
        string slug,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var technique = await databaseContext.Techniques
            .FirstOrDefaultAsync(candidate => candidate.Slug == slug, cancellationToken);

        if (technique is null)
            return;

        var existingProgress = await databaseContext.UserTechniqueProgressRecords
            .FirstOrDefaultAsync(
                progress => progress.UserId == currentUserId && progress.TechniqueId == technique.Id,
                cancellationToken);

        if (existingProgress is not null)
            return;

        databaseContext.UserTechniqueProgressRecords.Add(new UserTechniqueProgress
        {
            Id = Guid.NewGuid(),
            UserId = currentUserId,
            TechniqueId = technique.Id,
            Level = TechniqueLevels.Novice,
            MasteryPercent = 0,
            PracticeCount = 0,
            FirstSeenAt = DateTime.UtcNow,
        });

        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadSkillIconicNamesAsync(
        IReadOnlyCollection<Guid> skillIds,
        CancellationToken cancellationToken)
    {
        if (skillIds.Count == 0)
            return new Dictionary<Guid, string>();

        return await databaseContext.Skills.AsNoTracking()
            .Where(skill => skillIds.Contains(skill.Id))
            .ToDictionaryAsync(skill => skill.Id, skill => skill.IconicName, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, UserTechniqueProgress>> LoadProgressAsync(
        Guid? currentUserId,
        IReadOnlyCollection<Guid> techniqueIds,
        CancellationToken cancellationToken)
    {
        if (!currentUserId.HasValue || techniqueIds.Count == 0)
            return new Dictionary<Guid, UserTechniqueProgress>();

        return await databaseContext.UserTechniqueProgressRecords.AsNoTracking()
            .Where(progress => progress.UserId == currentUserId.Value
                               && techniqueIds.Contains(progress.TechniqueId))
            .ToDictionaryAsync(progress => progress.TechniqueId, cancellationToken);
    }

    private static TechniqueCardDto BuildCardDto(
        Technique technique,
        IReadOnlyDictionary<string, TechniqueCategory> categories,
        IReadOnlyDictionary<Guid, string> skillNameById,
        IReadOnlyDictionary<Guid, UserTechniqueProgress> progressByTechniqueId)
    {
        var categoryLabel = technique.CategorySlug;
        var categoryColor = "var(--ink-3)";
        if (categories.TryGetValue(technique.CategorySlug, out var category))
        {
            categoryLabel = category.Label;
            categoryColor = category.Color;
        }

        string? primarySkillIconicName = null;
        if (technique.PrimarySkillId.HasValue &&
            skillNameById.TryGetValue(technique.PrimarySkillId.Value, out var iconicName))
        {
            primarySkillIconicName = iconicName;
        }

        var progress = progressByTechniqueId.GetValueOrDefault(technique.Id);
        var level = progress?.Level ?? 0;
        var masteryPercent = progress?.MasteryPercent ?? 0;
        var levelName = level == 0
            ? "Novice"
            : TechniqueLevels.ResolveLevelName(level, masteryPercent);
        var isNew = progress is null;

        return new TechniqueCardDto(
            technique.Id,
            technique.Slug,
            technique.Name,
            technique.Summary,
            technique.CategorySlug,
            categoryLabel,
            categoryColor,
            technique.Tags,
            primarySkillIconicName,
            technique.SortOrder,
            level,
            levelName,
            masteryPercent,
            isNew);
    }

    private static TechniqueDialogAnnotationDto[] DeserializeAnnotations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<TechniqueDialogAnnotationDto>();

        try
        {
            return JsonSerializer.Deserialize<TechniqueDialogAnnotationDto[]>(json, DefaultJsonOptions)
                   ?? Array.Empty<TechniqueDialogAnnotationDto>();
        }
        catch (JsonException)
        {
            return Array.Empty<TechniqueDialogAnnotationDto>();
        }
    }

    private static TechniqueCoachChallengeDto[] DeserializeChallenges(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<TechniqueCoachChallengeDto>();

        try
        {
            return JsonSerializer.Deserialize<TechniqueCoachChallengeDto[]>(json, DefaultJsonOptions)
                   ?? Array.Empty<TechniqueCoachChallengeDto>();
        }
        catch (JsonException)
        {
            return Array.Empty<TechniqueCoachChallengeDto>();
        }
    }

    private static JsonObject? DeserializeJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions DefaultJsonOptions = new(JsonSerializerDefaults.Web);
}
