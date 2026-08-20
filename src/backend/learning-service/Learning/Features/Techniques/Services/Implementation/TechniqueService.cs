using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Features.Content;
using Sellevate.Learning.Features.Techniques.Models;
using Sellevate.Learning.Features.Techniques.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Techniques.Services.Implementation;

/// <summary>
/// Read model for the technique library: cards, detail pages, facet counts, and the
/// first-seen marker.
///
/// <para>
/// <b>Every technique query goes through <c>ResolveOverrides</c>.</b> A tenant override row carries
/// the same slug and identity as the platform base row it replaces, so an unresolved query returns
/// both and the caller sees a duplicate — or, on a single-row lookup, whichever row the query planner
/// reached first. Resolution collapses the pair down to the row this organization should see, so a
/// new query added here must keep the call (docs/TENANCY/CONTENT_MODEL.md).
/// </para>
///
/// <para>
/// <b>Free-text search runs in memory, not in SQL.</b> The <c>searchTerm</c> filter is applied after
/// materialization because it is case-insensitive across four fields including the technique body,
/// and the library is small enough that a scan costs less than the index machinery a database-side
/// match would need. Tag and skill filters do run in SQL, so the in-memory pass only ever sees an
/// already narrowed set.
/// </para>
///
/// <para>
/// <b>Malformed embedded JSON degrades, it does not throw.</b> <c>DialogJson</c>, <c>CaseJson</c> and
/// the coach's <c>ChallengesJson</c> are author-supplied documents; a deserialization failure yields
/// an empty section so the rest of the technique still renders rather than failing the whole page.
/// </para>
/// </summary>
internal sealed class TechniqueService(LearningDbContext databaseContext) : ITechniqueService
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<TechniqueCardDto>> GetTechniqueCardsAsync(
        Guid? currentUserId,
        string? skillIconicName,
        string? searchTerm,
        IReadOnlyCollection<string>? tags,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var techniquesQuery = databaseContext.Techniques.AsNoTracking().ResolveOverrides(databaseContext);

        if (!string.IsNullOrWhiteSpace(skillIconicName))
        {
            var matchingSkillId = await databaseContext.Skills.AsNoTracking()
                .Where(skill => skill.IconicName == skillIconicName)
                .Select(skill => (Guid?)skill.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (matchingSkillId is null)
                return Array.Empty<TechniqueCardDto>();

            // A technique's skill is not just PrimarySkillId — GetTechniqueBySlugAsync already unions
            // it with AdditionalSkills for the detail page's skill chips. This filter must agree, or a
            // skill facet that GetTechniqueMetaAsync advertises (see below) can return zero cards.
            techniquesQuery = techniquesQuery.Where(technique =>
                technique.PrimarySkillId == matchingSkillId
                || technique.AdditionalSkills.Any(link => link.SkillId == matchingSkillId));
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

    /// <summary>
    /// Loads one technique by slug, or <c>null</c> when this organization cannot see it.
    ///
    /// <para>
    /// <b>Override resolution is correctness here, not tidiness (Phase 40.18).</b> An override
    /// carries the same slug as its base — that is what keeps the URL stable — so an unresolved
    /// lookup by slug matches two rows and returns whichever the planner reached first.
    /// </para>
    /// </summary>
    public async Task<TechniqueDetailDto?> GetTechniqueBySlugAsync(
        string slug,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var technique = await databaseContext.Techniques.AsNoTracking().ResolveOverrides(databaseContext)
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
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        // A technique's skills are PrimarySkillId plus AdditionalSkills, not PrimarySkillId alone —
        // GetTechniqueBySlugAsync already unions both for the detail page. Most authored techniques
        // in practice only carry AdditionalSkills, so counting PrimarySkillId alone found nothing and
        // the guidebook's skill filter rendered no chips at all.
        var techniqueSkillRows = await databaseContext.Techniques.AsNoTracking().ResolveOverrides(databaseContext)
            .Select(technique => new
            {
                technique.PrimarySkillId,
                AdditionalSkillIds = technique.AdditionalSkills.Select(link => link.SkillId).ToArray(),
            })
            .ToListAsync(cancellationToken);

        var techniqueCountsBySkill = techniqueSkillRows
            .SelectMany(row => (row.PrimarySkillId.HasValue
                    ? new[] { row.PrimarySkillId.Value }
                    : Array.Empty<Guid>())
                .Concat(row.AdditionalSkillIds)
                .Distinct())
            .GroupBy(skillId => skillId)
            .ToDictionary(group => group.Key, group => group.Count());

        var skillIdsWithTechniques = techniqueCountsBySkill.Keys.ToArray();

        var skills = await databaseContext.Skills.AsNoTracking()
            .Where(skill => skillIdsWithTechniques.Contains(skill.Id))
            .OrderBy(skill => skill.OrderInTree)
            .ToListAsync(cancellationToken);

        var skillFacets = skills.Select(skill => new TechniqueSkillFacetDto(
                skill.IconicName,
                skill.Title,
                techniqueCountsBySkill.GetValueOrDefault(skill.Id)))
            .ToArray();

        var totalCount = await databaseContext.Techniques.ResolveOverrides(databaseContext).CountAsync(cancellationToken);

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
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var technique = await databaseContext.Techniques.ResolveOverrides(databaseContext)
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
        await tenantScope.CommitAsync(cancellationToken);
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

    /// <summary>
    /// <c>System.Text.Json</c> does not enforce the non-nullable annotations on
    /// <see cref="TechniqueDialogTurnDto"/> — an author-supplied turn missing the
    /// <c>annotations</c> key (or with <c>"annotations": null</c>) deserializes with
    /// <c>Annotations == null</c> despite the declared type. Normalizing here, once, keeps every
    /// consumer of this DTO free to treat <c>Annotations</c> as always an array, never null.
    /// </summary>
    private static TechniqueDialogTurnDto[] DeserializeDialogTurns(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<TechniqueDialogTurnDto>();

        try
        {
            var turns = JsonSerializer.Deserialize<TechniqueDialogTurnDto[]>(json, DefaultJsonOptions);
            if (turns is null)
                return Array.Empty<TechniqueDialogTurnDto>();

            return turns
                .Where(turn => turn is not null)
                .Select(turn => turn! with { Annotations = NormalizeAnnotations(turn.Annotations) })
                .ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<TechniqueDialogTurnDto>();
        }
    }

    /// <summary>
    /// Same author-JSON unreliability as <see cref="DeserializeDialogTurns"/>, one level deeper: a
    /// literal <c>null</c> element in the <c>annotations</c> array (e.g. <c>[null]</c>) deserializes
    /// to a null <see cref="TechniqueDialogAnnotationDto"/>, and an annotation missing <c>label</c>
    /// deserializes with <c>Label == null</c> despite its non-nullable declared type.
    /// </summary>
    private static TechniqueDialogAnnotationDto[] NormalizeAnnotations(TechniqueDialogAnnotationDto[]? annotations)
    {
        if (annotations is null)
            return Array.Empty<TechniqueDialogAnnotationDto>();

        return annotations
            .Where(annotation => annotation is not null)
            .Select(annotation => annotation! with { Label = annotation.Label ?? string.Empty })
            .ToArray();
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

    /// <summary>
    /// Same author-JSON unreliability as <see cref="DeserializeDialogTurns"/>: a literal
    /// <c>null</c> element in the challenges array deserializes to a null
    /// <see cref="TechniqueCoachChallengeDto"/>, and a challenge missing <c>label</c> deserializes
    /// with <c>Label == null</c> despite its non-nullable declared type.
    /// </summary>
    private static TechniqueCoachChallengeDto[] DeserializeChallenges(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<TechniqueCoachChallengeDto>();

        try
        {
            var challenges = JsonSerializer.Deserialize<TechniqueCoachChallengeDto[]>(json, DefaultJsonOptions);
            if (challenges is null)
                return Array.Empty<TechniqueCoachChallengeDto>();

            return challenges
                .Where(challenge => challenge is not null)
                .Select(challenge => challenge! with { Label = challenge.Label ?? string.Empty })
                .ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<TechniqueCoachChallengeDto>();
        }
    }
}
