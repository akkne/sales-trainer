using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.Lessons.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Lessons.Services.Implementation;

/// <summary>
/// Phase 40.15. Implements docs/TENANCY/CONTENT_MODEL.md §2: the draft is mutable, publishing
/// freezes it forever, and the next edit starts a new draft.
///
/// <para>
/// One thing worth stating because it looks like an omission: the draft's snapshot is re-derived
/// from the live <c>Lesson</c> and <c>Exercise</c> rows on every call rather than edited in place.
/// That is the model CONTENT_MODEL.md §2.2 describes — the exercise rows are the working
/// representation the admin panel edits, and the version table is what history reads — and it means
/// the draft can never drift out of sync with what the admin is actually looking at. What the draft
/// row carries that the working rows cannot is the things a snapshot has and live rows do not: who
/// started editing, when, which base version this was forked from, and the plain fact that this
/// lesson has unpublished changes, which is what 40.18's stale-override queue reads.
/// </para>
/// </summary>
internal sealed class LessonVersionService(LearningDbContext databaseContext) : ILessonVersionService
{
    public async Task<IReadOnlyList<LessonVersionSummaryDto>?> GetVersionsAsync(
        Guid lessonId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var lessonExists = await databaseContext.Lessons
            .AnyAsync(lesson => lesson.Id == lessonId, cancellationToken);
        if (!lessonExists)
        {
            return null;
        }

        return await databaseContext.LessonVersions
            .Where(version => version.LessonId == lessonId)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => new LessonVersionSummaryDto(
                version.Id,
                version.LessonId,
                version.VersionNumber,
                version.Status,
                version.ContentHash,
                version.BaseVersionId,
                version.IsBreaking,
                version.CreatedBy,
                version.CreatedAt,
                version.PublishedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<LessonVersionDto?> GetVersionAsync(
        Guid lessonId,
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var version = await databaseContext.LessonVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == versionId && candidate.LessonId == lessonId,
                cancellationToken);

        return version is null ? null : ToDto(version);
    }

    public async Task<LessonVersionDto?> EnsureDraftAsync(
        Guid lessonId,
        Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var lesson = await databaseContext.Lessons
            .FirstOrDefaultAsync(candidate => candidate.Id == lessonId, cancellationToken);
        if (lesson is null)
        {
            return null;
        }

        var canonicalContent = await BuildCanonicalContentAsync(lesson, cancellationToken);
        var draft = await FindDraftAsync(lessonId, cancellationToken);

        if (draft is null)
        {
            draft = new LessonVersion
            {
                Id = Guid.NewGuid(),
                OrganizationId = lesson.OrganizationId,
                LessonId = lesson.Id,
                VersionNumber = await ResolveNextVersionNumberAsync(lessonId, cancellationToken),
                Status = LessonVersionStatuses.Draft,
                BaseVersionId = await ResolveBaseVersionIdAsync(lesson, cancellationToken),
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow,
            };

            databaseContext.LessonVersions.Add(draft);
        }

        draft.Content = canonicalContent;
        draft.ContentHash = LessonSnapshotSerializer.ComputeContentHash(canonicalContent);

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        return ToDto(draft);
    }

    public async Task<PublishLessonVersionResultDto?> PublishAsync(
        Guid lessonId,
        bool isBreaking,
        Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var lesson = await databaseContext.Lessons
            .FirstOrDefaultAsync(candidate => candidate.Id == lessonId, cancellationToken);
        if (lesson is null)
        {
            return null;
        }

        var canonicalContent = await BuildCanonicalContentAsync(lesson, cancellationToken);
        var contentHash = LessonSnapshotSerializer.ComputeContentHash(canonicalContent);

        var latestPublishedVersion = await databaseContext.LessonVersions
            .Where(version => version.LessonId == lessonId && version.Status == LessonVersionStatuses.Published)
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var draft = await FindDraftAsync(lessonId, cancellationToken);

        // Publishing something identical to what is already published would add a version number
        // that means nothing and a break in every metric series joined on version — the exact
        // failure content_hash exists to prevent (CONTENT_MODEL.md §2.1). The open draft goes away
        // with it: it described changes that turned out not to exist.
        if (latestPublishedVersion is not null && latestPublishedVersion.ContentHash == contentHash)
        {
            if (draft is not null)
            {
                databaseContext.LessonVersions.Remove(draft);
                await databaseContext.SaveChangesAsync(cancellationToken);
            }

            await tenantScope.CommitAsync(cancellationToken);
            return new PublishLessonVersionResultDto(ToDto(latestPublishedVersion), CreatedNewVersion: false);
        }

        if (draft is null)
        {
            draft = new LessonVersion
            {
                Id = Guid.NewGuid(),
                OrganizationId = lesson.OrganizationId,
                LessonId = lesson.Id,
                Status = LessonVersionStatuses.Draft,
                BaseVersionId = await ResolveBaseVersionIdAsync(lesson, cancellationToken),
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow,
            };

            databaseContext.LessonVersions.Add(draft);
        }

        // Re-derived rather than trusted from draft creation time: another lesson version could
        // have been published in between, and a version number is not something to guess at.
        draft.VersionNumber = await ResolveNextVersionNumberAsync(lessonId, cancellationToken);
        draft.Content = canonicalContent;
        draft.ContentHash = contentHash;
        draft.IsBreaking = isBreaking;
        draft.Status = LessonVersionStatuses.Published;
        draft.PublishedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        return new PublishLessonVersionResultDto(ToDto(draft), CreatedNewVersion: true);
    }

    /// <remarks>
    /// Three separate transactions on purpose, and it must be called with none of its own already
    /// open (see the note on the caller in <c>ExerciseService</c>). A unique-index violation aborts
    /// the whole Postgres transaction it happens in, so the "somebody else minted it first" recovery
    /// read cannot live inside the transaction that lost the race — it would fail with
    /// "current transaction is aborted" and hide the real answer.
    /// </remarks>
    public async Task<Guid?> EnsurePublishedVersionIdAsync(
        Guid lessonId,
        CancellationToken cancellationToken = default)
    {
        var latestPublishedVersionId = await ReadLatestPublishedVersionIdAsync(lessonId, cancellationToken);
        if (latestPublishedVersionId is not null)
        {
            return latestPublishedVersionId;
        }

        try
        {
            return await MintInitialPublishedVersionAsync(lessonId, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two learners answering the first exercise of a never-published lesson at the same
            // moment both reach this point and both insert version 1; the unique index on
            // (LessonId, VersionNumber) lets exactly one of them through. Losing that race is not an
            // error — the other transaction produced the very row this one wanted — so the loser
            // adopts it instead of failing a learner's submission over a bookkeeping collision.
            databaseContext.ChangeTracker.Clear();
            return await ReadLatestPublishedVersionIdAsync(lessonId, cancellationToken);
        }
    }

    private async Task<Guid?> MintInitialPublishedVersionAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var lesson = await databaseContext.Lessons
            .FirstOrDefaultAsync(candidate => candidate.Id == lessonId, cancellationToken);
        if (lesson is null)
        {
            return null;
        }

        var canonicalContent = await BuildCanonicalContentAsync(lesson, cancellationToken);
        var mintedVersion = new LessonVersion
        {
            Id = Guid.NewGuid(),
            OrganizationId = lesson.OrganizationId,
            LessonId = lesson.Id,
            VersionNumber = await ResolveNextVersionNumberAsync(lessonId, cancellationToken),
            Content = canonicalContent,
            ContentHash = LessonSnapshotSerializer.ComputeContentHash(canonicalContent),
            Status = LessonVersionStatuses.Published,
            BaseVersionId = await ResolveBaseVersionIdAsync(lesson, cancellationToken),
            IsBreaking = false,
            CreatedBy = null,
            CreatedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow,
        };

        databaseContext.LessonVersions.Add(mintedVersion);
        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        return mintedVersion.Id;
    }

    private async Task<Guid?> ReadLatestPublishedVersionIdAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        return await databaseContext.LessonVersions
            .Where(version => version.LessonId == lessonId && version.Status == LessonVersionStatuses.Published)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => (Guid?)version.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Task<LessonVersion?> FindDraftAsync(Guid lessonId, CancellationToken cancellationToken)
        => databaseContext.LessonVersions
            .FirstOrDefaultAsync(
                version => version.LessonId == lessonId && version.Status == LessonVersionStatuses.Draft,
                cancellationToken);

    private async Task<int> ResolveNextVersionNumberAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        var highestVersionNumber = await databaseContext.LessonVersions
            .Where(version => version.LessonId == lessonId)
            .Select(version => (int?)version.VersionNumber)
            .MaxAsync(cancellationToken);

        return (highestVersionNumber ?? 0) + 1;
    }

    /// <summary>
    /// The version of the parent lesson this snapshot was forked from, for a lesson that is an
    /// override. 40.18 compares it against the parent's latest published version to decide whether
    /// the override has gone stale; 40.15 only has to record it truthfully at the moment the draft
    /// opens.
    /// </summary>
    private async Task<Guid?> ResolveBaseVersionIdAsync(Lesson lesson, CancellationToken cancellationToken)
    {
        if (lesson.ParentLessonId is null)
        {
            return null;
        }

        return await databaseContext.LessonVersions
            .Where(version => version.LessonId == lesson.ParentLessonId
                              && version.Status == LessonVersionStatuses.Published)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => (Guid?)version.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string> BuildCanonicalContentAsync(Lesson lesson, CancellationToken cancellationToken)
    {
        var orderedExercises = await databaseContext.Exercises
            .AsNoTracking()
            .Where(exercise => exercise.LessonId == lesson.Id)
            .OrderBy(exercise => exercise.OrderInLesson)
            .ThenBy(exercise => exercise.Id)
            .ToListAsync(cancellationToken);

        return LessonSnapshotSerializer.BuildCanonicalContent(lesson, orderedExercises);
    }

    private static LessonVersionDto ToDto(LessonVersion version)
    {
        using var contentDocument = JsonDocument.Parse(version.Content);

        return new LessonVersionDto(
            version.Id,
            version.LessonId,
            version.VersionNumber,
            version.Status,
            contentDocument.RootElement.Clone(),
            version.ContentHash,
            version.BaseVersionId,
            version.IsBreaking,
            version.CreatedBy,
            version.CreatedAt,
            version.PublishedAt);
    }
}
