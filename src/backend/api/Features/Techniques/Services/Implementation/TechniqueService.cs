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
        string? skillIconicName,
        string? searchTerm,
        IReadOnlyCollection<string>? tags,
        CancellationToken cancellationToken = default)
    {
        var techniquesQuery = databaseContext.Techniques.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(skillIconicName))
        {
            var matchingSkillId = await databaseContext.Skills.AsNoTracking()
                .Where(skill => skill.IconicName == skillIconicName)
                .Select(skill => (Guid?)skill.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (matchingSkillId is null)
                return Array.Empty<TechniqueCardDto>();

            techniquesQuery = techniquesQuery.Where(technique =>
                technique.PrimarySkillId == matchingSkillId);
        }

        if (tags is { Count: > 0 })
        {
            foreach (var tagValue in tags)
            {
                if (string.IsNullOrWhiteSpace(tagValue))
                    continue;

                var capturedTag = tagValue;
                techniquesQuery = techniquesQuery.Where(technique => technique.Tags.Contains(capturedTag));
            }
        }

        var techniques = await techniquesQuery
            .Include(technique => technique.Coach)
            .OrderBy(technique => technique.SortOrder)
            .ThenBy(technique => technique.Name)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchLower = searchTerm.Trim().ToLowerInvariant();
            techniques = techniques.Where(technique =>
                technique.Name.ToLowerInvariant().Contains(searchLower) ||
                technique.Summary.ToLowerInvariant().Contains(searchLower) ||
                technique.Body.ToLowerInvariant().Contains(searchLower) ||
                technique.Tags.Any(existingTag => existingTag.ToLowerInvariant().Contains(searchLower)))
                .ToList();
        }

        var skillLookup = await LoadSkillLookupAsync(
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
            skillLookup,
            progressByTechniqueId)).ToList();
    }

    public async Task<TechniqueDetailDto?> GetTechniqueBySlugAsync(
        string slug,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var technique = await databaseContext.Techniques.AsNoTracking()
            .Include(loadedTechnique => loadedTechnique.Coach)
            .Include(loadedTechnique => loadedTechnique.AdditionalSkills)
            .FirstOrDefaultAsync(candidate => candidate.Slug == slug, cancellationToken);

        if (technique is null)
            return null;

        var skillIds = technique.AdditionalSkills.Select(link => link.SkillId).ToList();
        if (technique.PrimarySkillId.HasValue)
            skillIds.Add(technique.PrimarySkillId.Value);

        var skillLookup = await LoadSkillLookupAsync(skillIds.Distinct().ToArray(), cancellationToken);

        var progressByTechniqueId = await LoadProgressAsync(
            currentUserId,
            new[] { technique.Id },
            cancellationToken);

        var card = BuildCardDto(technique, skillLookup, progressByTechniqueId);

        var skillIconicNames = skillIds
            .Select(skillId => skillLookup.GetValueOrDefault(skillId)?.IconicName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .Distinct()
            .ToArray();

        return new TechniqueDetailDto(
            card,
            technique.Body,
            skillIconicNames,
            DeserializeDialogTurns(technique.DialogJson),
            DeserializeCase(technique.CaseJson),
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
        var techniqueCountsBySkill = await databaseContext.Techniques.AsNoTracking()
            .Where(technique => technique.PrimarySkillId != null)
            .GroupBy(technique => technique.PrimarySkillId!.Value)
            .Select(group => new { SkillId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var skillIdsWithTechniques = techniqueCountsBySkill
            .Select(row => row.SkillId)
            .ToArray();

        var skills = await databaseContext.Skills.AsNoTracking()
            .Where(skill => skillIdsWithTechniques.Contains(skill.Id))
            .OrderBy(skill => skill.OrderInTree)
            .ToListAsync(cancellationToken);

        var countById = techniqueCountsBySkill.ToDictionary(row => row.SkillId, row => row.Count);
        var skillFacets = skills.Select(skill => new TechniqueSkillFacetDto(
                skill.IconicName,
                skill.Title,
                countById.GetValueOrDefault(skill.Id)))
            .ToArray();

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

        return new TechniqueMetaDto(skillFacets, totalCount, userCounts);
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
            Level = 0,
            MasteryPercent = 0,
            PracticeCount = 0,
            FirstSeenAt = DateTime.UtcNow,
        });

        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, Skill>> LoadSkillLookupAsync(
        IReadOnlyCollection<Guid> skillIds,
        CancellationToken cancellationToken)
    {
        if (skillIds.Count == 0)
            return new Dictionary<Guid, Skill>();

        return await databaseContext.Skills.AsNoTracking()
            .Where(skill => skillIds.Contains(skill.Id))
            .ToDictionaryAsync(skill => skill.Id, cancellationToken);
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
        IReadOnlyDictionary<Guid, Skill> skillLookup,
        IReadOnlyDictionary<Guid, UserTechniqueProgress> progressByTechniqueId)
    {
        string? primarySkillIconicName = null;
        string? primarySkillTitle = null;
        if (technique.PrimarySkillId.HasValue &&
            skillLookup.TryGetValue(technique.PrimarySkillId.Value, out var primarySkill))
        {
            primarySkillIconicName = primarySkill.IconicName;
            primarySkillTitle = primarySkill.Title;
        }

        var progress = progressByTechniqueId.GetValueOrDefault(technique.Id);
        var masteryLevel = progress?.Level ?? 0;
        var masteryPercent = progress?.MasteryPercent ?? 0;
        var isNew = progress is null;

        return new TechniqueCardDto(
            technique.Id,
            technique.Slug,
            technique.Name,
            technique.Summary,
            technique.Tags,
            primarySkillIconicName,
            primarySkillTitle,
            technique.Difficulty,
            TechniqueLevels.ResolveDifficultyName(technique.Difficulty),
            technique.SortOrder,
            masteryLevel,
            masteryPercent,
            HasDialog: !string.IsNullOrWhiteSpace(technique.DialogJson),
            HasCase: !string.IsNullOrWhiteSpace(technique.CaseJson),
            HasCoach: technique.Coach is not null,
            IsNew: isNew);
    }

    private static TechniqueDialogTurnDto[] DeserializeDialogTurns(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<TechniqueDialogTurnDto>();

        try
        {
            var turns = JsonSerializer.Deserialize<TechniqueDialogTurnDto[]>(json, DefaultJsonOptions);
            return turns ?? Array.Empty<TechniqueDialogTurnDto>();
        }
        catch (JsonException)
        {
            return Array.Empty<TechniqueDialogTurnDto>();
        }
    }

    private static TechniqueCaseDto? DeserializeCase(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<TechniqueCaseDto>(json, DefaultJsonOptions);
        }
        catch (JsonException)
        {
            return null;
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

    private static readonly JsonSerializerOptions DefaultJsonOptions = new(JsonSerializerDefaults.Web);
}
