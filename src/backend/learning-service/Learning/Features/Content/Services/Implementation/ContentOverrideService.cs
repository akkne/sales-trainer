using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Content.Models;
using Sellevate.Learning.Features.Content.Services.Abstract;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.Lessons.Services.Abstract;
using Sellevate.Learning.Features.Lessons.Services.Implementation;
using Sellevate.Learning.Features.Reference.Models;
using Sellevate.Learning.Features.Techniques.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Content.Services.Implementation;

/// <summary>
/// Phase 40.18. Implements docs/TENANCY/CONTENT_MODEL.md §2.6.
///
/// <para>
/// <b>Nothing here merges anything.</b> That is the roadmap's most categorical instruction in this
/// block and it is worth restating where the code is: a lesson is prose and grading criteria, and a
/// three-way merge of those produces text that reads as if a person wrote it and then scores a
/// salesperson against a rule nobody chose. The queue hands the two versions to a human and offers
/// three answers — take the base, keep ours, edit ours — and every one of them is a decision, never
/// a computation.
/// </para>
/// </summary>
internal sealed class ContentOverrideService(
    LearningDbContext databaseContext,
    ILessonVersionService lessonVersionService,
    ILogger<ContentOverrideService> logger) : IContentOverrideService
{
    public async Task<ContentOverrideResult> CreateOverrideAsync(
        string kind,
        Guid baseId,
        Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        if (databaseContext.TenantContext.OrganizationId is not { } organizationId)
        {
            return new ContentOverrideResult(ContentOverrideOutcome.NoOrganization, null);
        }

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var result = kind switch
        {
            ContentOverrideKinds.Lesson =>
                await CreateLessonOverrideAsync(baseId, organizationId, actorId, cancellationToken),
            ContentOverrideKinds.Technique =>
                await CreateTechniqueOverrideAsync(baseId, organizationId, cancellationToken),
            ContentOverrideKinds.ReferenceMaterial =>
                await CreateReferenceMaterialOverrideAsync(baseId, organizationId, cancellationToken),
            _ => new ContentOverrideResult(ContentOverrideOutcome.SourceNotFound, null),
        };

        await tenantScope.CommitAsync(cancellationToken);

        if (result.Outcome == ContentOverrideOutcome.Created)
        {
            logger.LogInformation(
                "Content override created Kind={Kind} BaseId={BaseId} OverrideId={OverrideId} OrganizationId={OrganizationId} ActorId={ActorId}",
                kind, baseId, result.Override!.OverrideId, organizationId, actorId);
        }

        return result;
    }

    public async Task<IReadOnlyList<ContentOverrideDto>> GetOverridesAsync(
        bool staleOnly,
        CancellationToken cancellationToken = default)
    {
        if (databaseContext.TenantContext.OrganizationId is not { } organizationId)
        {
            return [];
        }

        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var overrides = new List<ContentOverrideDto>();
        overrides.AddRange(await DescribeLessonOverridesAsync(organizationId, cancellationToken));
        overrides.AddRange(await DescribeTechniqueOverridesAsync(organizationId, cancellationToken));
        overrides.AddRange(await DescribeReferenceMaterialOverridesAsync(organizationId, cancellationToken));

        return staleOnly
            ? overrides.Where(entry => entry.IsStale).ToList()
            : overrides;
    }

    public async Task<ContentOverrideReviewDto?> GetReviewAsync(
        string kind,
        Guid overrideId,
        CancellationToken cancellationToken = default)
    {
        if (databaseContext.TenantContext.OrganizationId is not { } organizationId)
        {
            return null;
        }

        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        return kind switch
        {
            ContentOverrideKinds.Lesson => await BuildLessonReviewAsync(overrideId, organizationId, cancellationToken),
            ContentOverrideKinds.Technique => await BuildTechniqueReviewAsync(overrideId, organizationId, cancellationToken),
            ContentOverrideKinds.ReferenceMaterial =>
                await BuildReferenceMaterialReviewAsync(overrideId, organizationId, cancellationToken),
            _ => null,
        };
    }

    public async Task<bool> AcceptBaseAsync(
        string kind,
        Guid overrideId,
        Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        if (databaseContext.TenantContext.OrganizationId is not { } organizationId)
        {
            return false;
        }

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var retired = kind switch
        {
            ContentOverrideKinds.Lesson => await RetireAsync(
                await FindOwnLessonOverrideAsync(overrideId, organizationId, cancellationToken),
                lesson => lesson.IsArchived = true,
                cancellationToken),
            ContentOverrideKinds.Technique => await RetireAsync(
                await FindOwnTechniqueOverrideAsync(overrideId, organizationId, cancellationToken),
                technique => technique.IsArchived = true,
                cancellationToken),
            ContentOverrideKinds.ReferenceMaterial => await RetireAsync(
                await FindOwnReferenceMaterialOverrideAsync(overrideId, organizationId, cancellationToken),
                material => material.IsArchived = true,
                cancellationToken),
            _ => false,
        };

        if (!retired)
        {
            return false;
        }

        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Content override retired in favour of the base Kind={Kind} OverrideId={OverrideId} OrganizationId={OrganizationId} ActorId={ActorId}",
            kind, overrideId, organizationId, actorId);

        return true;
    }

    public async Task<bool> KeepOverrideAsync(
        string kind,
        Guid overrideId,
        Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        if (databaseContext.TenantContext.OrganizationId is not { } organizationId)
        {
            return false;
        }

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var rebased = kind switch
        {
            ContentOverrideKinds.Lesson => await RebaseLessonOverrideAsync(overrideId, organizationId, cancellationToken),
            ContentOverrideKinds.Technique => await RebaseTechniqueOverrideAsync(overrideId, organizationId, cancellationToken),
            ContentOverrideKinds.ReferenceMaterial =>
                await RebaseReferenceMaterialOverrideAsync(overrideId, organizationId, cancellationToken),
            _ => false,
        };

        if (!rebased)
        {
            return false;
        }

        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Content override kept and re-pointed at the current base Kind={Kind} OverrideId={OverrideId} OrganizationId={OrganizationId} ActorId={ActorId}",
            kind, overrideId, organizationId, actorId);

        return true;
    }

    /// <summary>
    /// Forks a global lesson into an organization-owned copy.
    ///
    /// <para>
    /// <b>A retired override is revived, not duplicated.</b> <c>UNIQUE (OrganizationId, Slug)</c>
    /// makes a second copy impossible anyway, and reviving is the honest reading of the sequence: the
    /// organization took the base back, so its old text was already discarded. Pressing "edit" again
    /// is therefore a fresh fork, which is why the body below is re-derived from the base rather than
    /// recovered from the retired row.
    /// </para>
    ///
    /// <para>
    /// <b>Opening the draft at the end is what records the fork point.</b>
    /// <c>LessonVersionService</c> resolves <c>BaseVersionId</c> from the parent lesson's latest
    /// published version, so the draft is not a convenience — without it the copy has no recorded
    /// base and <see cref="DescribeLessonOverrideAsync"/> can only report it as stale. It is also the
    /// state the authoring screens expect, so "press edit" lands the administrator in an editable
    /// lesson rather than in a copy nobody has opened.
    /// </para>
    /// </summary>
    private async Task<ContentOverrideResult> CreateLessonOverrideAsync(
        Guid baseLessonId,
        Guid organizationId,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        var baseLesson = await databaseContext.Lessons
            .FirstOrDefaultAsync(lesson => lesson.Id == baseLessonId, cancellationToken);
        if (baseLesson is null)
        {
            return new ContentOverrideResult(ContentOverrideOutcome.SourceNotFound, null);
        }

        if (baseLesson.OrganizationId is not null)
        {
            return new ContentOverrideResult(ContentOverrideOutcome.SourceNotGlobal, null);
        }

        var existing = await databaseContext.Lessons
            .FirstOrDefaultAsync(
                lesson => lesson.ParentLessonId == baseLessonId && lesson.OrganizationId == organizationId,
                cancellationToken);

        if (existing is { IsArchived: false })
        {
            return new ContentOverrideResult(
                ContentOverrideOutcome.AlreadyExists,
                await DescribeLessonOverrideAsync(existing, cancellationToken));
        }

        var overrideLesson = existing ?? new Lesson { Id = Guid.NewGuid() };

        overrideLesson.OrganizationId = organizationId;
        overrideLesson.ParentLessonId = baseLesson.Id;
        overrideLesson.TopicId = baseLesson.TopicId;
        overrideLesson.OrderInTopic = baseLesson.OrderInTopic;
        overrideLesson.Title = baseLesson.Title;
        overrideLesson.Slug = baseLesson.Slug;
        overrideLesson.IsArchived = false;

        if (existing is null)
        {
            databaseContext.Lessons.Add(overrideLesson);
        }
        else
        {
            var staleExercises = await databaseContext.Exercises
                .Where(exercise => exercise.LessonId == overrideLesson.Id)
                .ToListAsync(cancellationToken);
            databaseContext.Exercises.RemoveRange(staleExercises);
        }

        var baseExercises = await databaseContext.Exercises
            .AsNoTracking()
            .Where(exercise => exercise.LessonId == baseLesson.Id)
            .OrderBy(exercise => exercise.OrderInLesson)
            .ThenBy(exercise => exercise.Id)
            .ToListAsync(cancellationToken);

        var copiedAt = DateTime.UtcNow;
        foreach (var baseExercise in baseExercises)
        {
            databaseContext.Exercises.Add(new Exercise
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                LessonId = overrideLesson.Id,
                Type = baseExercise.Type,
                OrderInLesson = baseExercise.OrderInLesson,
                SerializedContent = baseExercise.SerializedContent,
                CustomAiPrompt = baseExercise.CustomAiPrompt,
                CreatedAt = copiedAt,
                UpdatedAt = copiedAt,
            });
        }

        await databaseContext.SaveChangesAsync(cancellationToken);

        await lessonVersionService.EnsureDraftAsync(overrideLesson.Id, actorId, cancellationToken);

        return new ContentOverrideResult(
            ContentOverrideOutcome.Created,
            await DescribeLessonOverrideAsync(overrideLesson, cancellationToken));
    }

    private async Task<IReadOnlyList<ContentOverrideDto>> DescribeLessonOverridesAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var overrides = await databaseContext.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.OrganizationId == organizationId
                             && lesson.ParentLessonId != null
                             && !lesson.IsArchived)
            .OrderBy(lesson => lesson.Title)
            .ToListAsync(cancellationToken);

        var described = new List<ContentOverrideDto>(overrides.Count);
        foreach (var overrideLesson in overrides)
        {
            described.Add(await DescribeLessonOverrideAsync(overrideLesson, cancellationToken));
        }

        return described;
    }

    /// <summary>
    /// Describes one lesson override, including whether the base has moved under it.
    ///
    /// <para>
    /// Staleness compares two version pointers, and the two null cases mean different things. A base
    /// with nothing published cannot have moved — there is no version to have moved from — so it is
    /// never stale. A missing fork point, by contrast, <b>counts as stale</b>: 40.15 left
    /// <c>BaseVersionId</c> nullable precisely so that "unknown base, needs review" is a state the
    /// review queue can show rather than a silent assumption that the copy is current.
    /// </para>
    ///
    /// <para>
    /// Note this is the version-identifier mechanism, which only notices a base change when someone
    /// explicitly publishes. Techniques and reference materials hash the base live instead and so
    /// catch ordinary edits. The difference is deliberate and is recorded as an open owner-level gap
    /// in <c>docs/DONT_FORGET.md</c>.
    /// </para>
    /// </summary>
    private async Task<ContentOverrideDto> DescribeLessonOverrideAsync(
        Lesson overrideLesson,
        CancellationToken cancellationToken)
    {
        var forkedFromVersionId = await ReadNewestVersionBaseAsync(overrideLesson.Id, cancellationToken);
        var currentBaseVersionId = await ReadLatestPublishedVersionIdAsync(
            overrideLesson.ParentLessonId!.Value, cancellationToken);

        var isStale = currentBaseVersionId is not null && currentBaseVersionId != forkedFromVersionId;

        return new ContentOverrideDto(
            ContentOverrideKinds.Lesson,
            overrideLesson.Id,
            overrideLesson.ParentLessonId.Value,
            overrideLesson.Title,
            isStale,
            forkedFromVersionId?.ToString(),
            currentBaseVersionId?.ToString());
    }

    private async Task<ContentOverrideReviewDto?> BuildLessonReviewAsync(
        Guid overrideId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var overrideLesson = await FindOwnLessonOverrideAsync(overrideId, organizationId, cancellationToken);
        if (overrideLesson is null)
        {
            return null;
        }

        var summary = await DescribeLessonOverrideAsync(overrideLesson, cancellationToken);

        var overrideContent = await ReadNewestVersionContentAsync(overrideLesson.Id, cancellationToken);
        var baseAtForkContent = summary.ForkedFrom is not null && Guid.TryParse(summary.ForkedFrom, out var forkId)
            ? await ReadVersionContentAsync(forkId, cancellationToken)
            : null;
        var baseCurrentContent = summary.BaseCurrent is not null && Guid.TryParse(summary.BaseCurrent, out var baseId)
            ? await ReadVersionContentAsync(baseId, cancellationToken)
            : null;

        return new ContentOverrideReviewDto(summary, overrideContent, baseAtForkContent, baseCurrentContent);
    }

    private async Task<bool> RebaseLessonOverrideAsync(
        Guid overrideId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var overrideLesson = await FindOwnLessonOverrideAsync(overrideId, organizationId, cancellationToken);
        if (overrideLesson is null)
        {
            return false;
        }

        var currentBaseVersionId = await ReadLatestPublishedVersionIdAsync(
            overrideLesson.ParentLessonId!.Value, cancellationToken);
        if (currentBaseVersionId is null)
        {
            return false;
        }

        var newestVersion = await databaseContext.LessonVersions
            .Where(version => version.LessonId == overrideLesson.Id)
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (newestVersion is null)
        {
            return false;
        }

        newestVersion.BaseVersionId = currentBaseVersionId;
        await databaseContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private Task<Lesson?> FindOwnLessonOverrideAsync(
        Guid overrideId,
        Guid organizationId,
        CancellationToken cancellationToken)
        => databaseContext.Lessons.FirstOrDefaultAsync(
            lesson => lesson.Id == overrideId
                      && lesson.OrganizationId == organizationId
                      && lesson.ParentLessonId != null,
            cancellationToken);

    private Task<Guid?> ReadNewestVersionBaseAsync(Guid lessonId, CancellationToken cancellationToken)
        => databaseContext.LessonVersions
            .Where(version => version.LessonId == lessonId)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => version.BaseVersionId)
            .FirstOrDefaultAsync(cancellationToken);

    private Task<Guid?> ReadLatestPublishedVersionIdAsync(Guid lessonId, CancellationToken cancellationToken)
        => databaseContext.LessonVersions
            .Where(version => version.LessonId == lessonId && version.Status == LessonVersionStatuses.Published)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => (Guid?)version.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<JsonElement?> ReadNewestVersionContentAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        var content = await databaseContext.LessonVersions
            .AsNoTracking()
            .Where(version => version.LessonId == lessonId)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => version.Content)
            .FirstOrDefaultAsync(cancellationToken);

        return Parse(content);
    }

    private async Task<JsonElement?> ReadVersionContentAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var content = await databaseContext.LessonVersions
            .AsNoTracking()
            .Where(version => version.Id == versionId)
            .Select(version => version.Content)
            .FirstOrDefaultAsync(cancellationToken);

        return Parse(content);
    }

    private async Task<ContentOverrideResult> CreateTechniqueOverrideAsync(
        Guid baseTechniqueId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var baseTechnique = await LoadTechniqueAsync(baseTechniqueId, cancellationToken);
        if (baseTechnique is null)
        {
            return new ContentOverrideResult(ContentOverrideOutcome.SourceNotFound, null);
        }

        if (baseTechnique.OrganizationId is not null)
        {
            return new ContentOverrideResult(ContentOverrideOutcome.SourceNotGlobal, null);
        }

        var existing = await databaseContext.Techniques
            .Include(technique => technique.Coach)
            .Include(technique => technique.AdditionalSkills)
            .FirstOrDefaultAsync(
                technique => technique.ParentTechniqueId == baseTechniqueId
                             && technique.OrganizationId == organizationId,
                cancellationToken);

        if (existing is { IsArchived: false })
        {
            return new ContentOverrideResult(
                ContentOverrideOutcome.AlreadyExists,
                await DescribeTechniqueOverrideAsync(existing, cancellationToken));
        }

        var overrideTechnique = existing ?? new Technique { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };

        overrideTechnique.OrganizationId = organizationId;
        overrideTechnique.ParentTechniqueId = baseTechnique.Id;
        overrideTechnique.IsArchived = false;
        overrideTechnique.Slug = baseTechnique.Slug;
        overrideTechnique.Name = baseTechnique.Name;
        overrideTechnique.Summary = baseTechnique.Summary;
        overrideTechnique.Body = baseTechnique.Body;
        overrideTechnique.Tags = [.. baseTechnique.Tags];
        overrideTechnique.PrimarySkillId = baseTechnique.PrimarySkillId;
        overrideTechnique.Difficulty = baseTechnique.Difficulty;
        overrideTechnique.DialogJson = baseTechnique.DialogJson;
        overrideTechnique.CaseJson = baseTechnique.CaseJson;
        overrideTechnique.SortOrder = baseTechnique.SortOrder;
        overrideTechnique.UpdatedAt = DateTime.UtcNow;
        overrideTechnique.BaseContentHash = ComputeTechniqueHash(baseTechnique);

        overrideTechnique.AdditionalSkills.Clear();
        foreach (var link in baseTechnique.AdditionalSkills)
        {
            overrideTechnique.AdditionalSkills.Add(new TechniqueSkill
            {
                TechniqueId = overrideTechnique.Id,
                SkillId = link.SkillId,
            });
        }

        overrideTechnique.Coach = baseTechnique.Coach is null
            ? null
            : new TechniqueCoach
            {
                Id = Guid.NewGuid(),
                TechniqueId = overrideTechnique.Id,
                AvatarSeed = baseTechnique.Coach.AvatarSeed,
                Name = baseTechnique.Coach.Name,
                Role = baseTechnique.Coach.Role,
                Quote = baseTechnique.Coach.Quote,
                ChallengesJson = baseTechnique.Coach.ChallengesJson,
            };

        if (existing is null)
        {
            databaseContext.Techniques.Add(overrideTechnique);
        }

        await databaseContext.SaveChangesAsync(cancellationToken);

        return new ContentOverrideResult(
            ContentOverrideOutcome.Created,
            await DescribeTechniqueOverrideAsync(overrideTechnique, cancellationToken));
    }

    private async Task<IReadOnlyList<ContentOverrideDto>> DescribeTechniqueOverridesAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var overrides = await databaseContext.Techniques
            .AsNoTracking()
            .Include(technique => technique.Coach)
            .Include(technique => technique.AdditionalSkills)
            .Where(technique => technique.OrganizationId == organizationId
                                && technique.ParentTechniqueId != null
                                && !technique.IsArchived)
            .OrderBy(technique => technique.Name)
            .ToListAsync(cancellationToken);

        var described = new List<ContentOverrideDto>(overrides.Count);
        foreach (var overrideTechnique in overrides)
        {
            described.Add(await DescribeTechniqueOverrideAsync(overrideTechnique, cancellationToken));
        }

        return described;
    }

    private async Task<ContentOverrideDto> DescribeTechniqueOverrideAsync(
        Technique overrideTechnique,
        CancellationToken cancellationToken)
    {
        var baseTechnique = await LoadTechniqueAsync(overrideTechnique.ParentTechniqueId!.Value, cancellationToken);
        var currentBaseHash = baseTechnique is null ? null : ComputeTechniqueHash(baseTechnique);

        return new ContentOverrideDto(
            ContentOverrideKinds.Technique,
            overrideTechnique.Id,
            overrideTechnique.ParentTechniqueId.Value,
            overrideTechnique.Name,
            currentBaseHash is not null && currentBaseHash != overrideTechnique.BaseContentHash,
            overrideTechnique.BaseContentHash,
            currentBaseHash);
    }

    /// <summary>
    /// Builds the review payload for a technique override: the organization's current text, the base
    /// as it stands now, and — deliberately — <see langword="null"/> for the before-image.
    ///
    /// <para>
    /// A technique's fork point is a content <i>fingerprint</i>, not a stored copy, and the text that
    /// fingerprint described was overwritten in place by whoever edited the base. So there is nothing
    /// to show as "the base as it was when you forked", and inventing one would be a guess presented
    /// as a record.
    /// </para>
    /// </summary>
    private async Task<ContentOverrideReviewDto?> BuildTechniqueReviewAsync(
        Guid overrideId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var overrideTechnique = await FindOwnTechniqueOverrideAsync(overrideId, organizationId, cancellationToken);
        if (overrideTechnique is null)
        {
            return null;
        }

        var summary = await DescribeTechniqueOverrideAsync(overrideTechnique, cancellationToken);
        var baseTechnique = await LoadTechniqueAsync(overrideTechnique.ParentTechniqueId!.Value, cancellationToken);

        return new ContentOverrideReviewDto(
            summary,
            Parse(ContentSnapshotSerializer.BuildCanonicalContent(
                overrideTechnique,
                overrideTechnique.Coach,
                [.. overrideTechnique.AdditionalSkills.Select(link => link.SkillId)])),
            null,
            baseTechnique is null
                ? null
                : Parse(ContentSnapshotSerializer.BuildCanonicalContent(
                    baseTechnique,
                    baseTechnique.Coach,
                    [.. baseTechnique.AdditionalSkills.Select(link => link.SkillId)])));
    }

    private async Task<bool> RebaseTechniqueOverrideAsync(
        Guid overrideId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var overrideTechnique = await FindOwnTechniqueOverrideAsync(overrideId, organizationId, cancellationToken);
        if (overrideTechnique is null)
        {
            return false;
        }

        var baseTechnique = await LoadTechniqueAsync(overrideTechnique.ParentTechniqueId!.Value, cancellationToken);
        if (baseTechnique is null)
        {
            return false;
        }

        overrideTechnique.BaseContentHash = ComputeTechniqueHash(baseTechnique);
        await databaseContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private Task<Technique?> FindOwnTechniqueOverrideAsync(
        Guid overrideId,
        Guid organizationId,
        CancellationToken cancellationToken)
        => databaseContext.Techniques
            .Include(technique => technique.Coach)
            .Include(technique => technique.AdditionalSkills)
            .FirstOrDefaultAsync(
                technique => technique.Id == overrideId
                             && technique.OrganizationId == organizationId
                             && technique.ParentTechniqueId != null,
                cancellationToken);

    private Task<Technique?> LoadTechniqueAsync(Guid techniqueId, CancellationToken cancellationToken)
        => databaseContext.Techniques
            .AsNoTracking()
            .Include(technique => technique.Coach)
            .Include(technique => technique.AdditionalSkills)
            .FirstOrDefaultAsync(technique => technique.Id == techniqueId, cancellationToken);

    private static string ComputeTechniqueHash(Technique technique)
        => ContentSnapshotSerializer.ComputeContentHash(
            ContentSnapshotSerializer.BuildCanonicalContent(
                technique,
                technique.Coach,
                [.. technique.AdditionalSkills.Select(link => link.SkillId)]));

    private async Task<ContentOverrideResult> CreateReferenceMaterialOverrideAsync(
        Guid baseMaterialId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var baseMaterial = await databaseContext.ReferenceMaterials
            .AsNoTracking()
            .FirstOrDefaultAsync(material => material.Id == baseMaterialId, cancellationToken);
        if (baseMaterial is null)
        {
            return new ContentOverrideResult(ContentOverrideOutcome.SourceNotFound, null);
        }

        if (baseMaterial.OrganizationId is not null)
        {
            return new ContentOverrideResult(ContentOverrideOutcome.SourceNotGlobal, null);
        }

        var existing = await databaseContext.ReferenceMaterials
            .FirstOrDefaultAsync(
                material => material.ParentMaterialId == baseMaterialId
                            && material.OrganizationId == organizationId,
                cancellationToken);

        if (existing is { IsArchived: false })
        {
            return new ContentOverrideResult(
                ContentOverrideOutcome.AlreadyExists,
                await DescribeReferenceMaterialOverrideAsync(existing, cancellationToken));
        }

        var overrideMaterial = existing ?? new ReferenceMaterial { Id = Guid.NewGuid() };

        overrideMaterial.OrganizationId = organizationId;
        overrideMaterial.ParentMaterialId = baseMaterial.Id;
        overrideMaterial.IsArchived = false;
        overrideMaterial.SkillId = baseMaterial.SkillId;
        overrideMaterial.Title = baseMaterial.Title;
        overrideMaterial.MarkdownContent = baseMaterial.MarkdownContent;
        overrideMaterial.SortOrder = baseMaterial.SortOrder;
        overrideMaterial.Category = baseMaterial.Category;
        overrideMaterial.Tags = baseMaterial.Tags;
        overrideMaterial.BaseContentHash = ComputeReferenceMaterialHash(baseMaterial);

        if (existing is null)
        {
            databaseContext.ReferenceMaterials.Add(overrideMaterial);
        }

        await databaseContext.SaveChangesAsync(cancellationToken);

        return new ContentOverrideResult(
            ContentOverrideOutcome.Created,
            await DescribeReferenceMaterialOverrideAsync(overrideMaterial, cancellationToken));
    }

    private async Task<IReadOnlyList<ContentOverrideDto>> DescribeReferenceMaterialOverridesAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var overrides = await databaseContext.ReferenceMaterials
            .AsNoTracking()
            .Where(material => material.OrganizationId == organizationId
                               && material.ParentMaterialId != null
                               && !material.IsArchived)
            .OrderBy(material => material.Title)
            .ToListAsync(cancellationToken);

        var described = new List<ContentOverrideDto>(overrides.Count);
        foreach (var overrideMaterial in overrides)
        {
            described.Add(await DescribeReferenceMaterialOverrideAsync(overrideMaterial, cancellationToken));
        }

        return described;
    }

    private async Task<ContentOverrideDto> DescribeReferenceMaterialOverrideAsync(
        ReferenceMaterial overrideMaterial,
        CancellationToken cancellationToken)
    {
        var baseMaterial = await LoadReferenceMaterialAsync(
            overrideMaterial.ParentMaterialId!.Value, cancellationToken);
        var currentBaseHash = baseMaterial is null ? null : ComputeReferenceMaterialHash(baseMaterial);

        return new ContentOverrideDto(
            ContentOverrideKinds.ReferenceMaterial,
            overrideMaterial.Id,
            overrideMaterial.ParentMaterialId.Value,
            overrideMaterial.Title,
            currentBaseHash is not null && currentBaseHash != overrideMaterial.BaseContentHash,
            overrideMaterial.BaseContentHash,
            currentBaseHash);
    }

    private async Task<ContentOverrideReviewDto?> BuildReferenceMaterialReviewAsync(
        Guid overrideId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var overrideMaterial = await FindOwnReferenceMaterialOverrideAsync(overrideId, organizationId, cancellationToken);
        if (overrideMaterial is null)
        {
            return null;
        }

        var summary = await DescribeReferenceMaterialOverrideAsync(overrideMaterial, cancellationToken);
        var baseMaterial = await LoadReferenceMaterialAsync(overrideMaterial.ParentMaterialId!.Value, cancellationToken);

        return new ContentOverrideReviewDto(
            summary,
            Parse(ContentSnapshotSerializer.BuildCanonicalContent(overrideMaterial)),
            null,
            baseMaterial is null ? null : Parse(ContentSnapshotSerializer.BuildCanonicalContent(baseMaterial)));
    }

    private async Task<bool> RebaseReferenceMaterialOverrideAsync(
        Guid overrideId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var overrideMaterial = await FindOwnReferenceMaterialOverrideAsync(overrideId, organizationId, cancellationToken);
        if (overrideMaterial is null)
        {
            return false;
        }

        var baseMaterial = await LoadReferenceMaterialAsync(overrideMaterial.ParentMaterialId!.Value, cancellationToken);
        if (baseMaterial is null)
        {
            return false;
        }

        overrideMaterial.BaseContentHash = ComputeReferenceMaterialHash(baseMaterial);
        await databaseContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private Task<ReferenceMaterial?> FindOwnReferenceMaterialOverrideAsync(
        Guid overrideId,
        Guid organizationId,
        CancellationToken cancellationToken)
        => databaseContext.ReferenceMaterials.FirstOrDefaultAsync(
            material => material.Id == overrideId
                        && material.OrganizationId == organizationId
                        && material.ParentMaterialId != null,
            cancellationToken);

    private Task<ReferenceMaterial?> LoadReferenceMaterialAsync(Guid materialId, CancellationToken cancellationToken)
        => databaseContext.ReferenceMaterials
            .AsNoTracking()
            .FirstOrDefaultAsync(material => material.Id == materialId, cancellationToken);

    private static string ComputeReferenceMaterialHash(ReferenceMaterial material)
        => ContentSnapshotSerializer.ComputeContentHash(
            ContentSnapshotSerializer.BuildCanonicalContent(material));

    private async Task<bool> RetireAsync<TEntity>(
        TEntity? entity,
        Action<TEntity> retire,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        if (entity is null)
        {
            return false;
        }

        retire(entity);
        await databaseContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static JsonElement? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
