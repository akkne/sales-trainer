using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Lessons.Services.Abstract;
using Sellevate.Learning.Features.Programs.Models;
using Sellevate.Learning.Features.Programs.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Programs.Services.Implementation;

/// <summary>
/// Phase 40.17. Implements docs/TENANCY/CONTENT_MODEL.md §2.5: the draft is mutable, publishing
/// freezes it, and a frozen programme is what a learner is pinned to.
///
/// <para>
/// <b>The one thing to keep true while changing this file.</b> Not a single write here touches
/// <c>Lessons</c>, <c>Exercises</c> or <c>LessonVersions</c>. A programme is an ordered list of
/// references, and reordering it must remain provably free of content edits — that is what lets an
/// organization rearrange its curriculum without forking the library (CONTENT_MODEL.md §1). The one
/// call that reaches into lesson versioning at all,
/// <c>ILessonVersionService.EnsurePublishedVersionIdAsync</c>, only reads — except on a lesson that
/// has never been published at all, where it mints the version 1 that already should have existed,
/// exactly as an attempt on that lesson would.
/// </para>
/// </summary>
internal sealed class ProgramVersionService(
    LearningDbContext databaseContext,
    ILessonVersionService lessonVersionService,
    ILogger<ProgramVersionService> logger) : IProgramVersionService
{
    public async Task<IReadOnlyList<ProgramVersionSummaryDto>> GetVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        return await databaseContext.ProgramVersions
            .AsNoTracking()
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => new ProgramVersionSummaryDto(
                version.Id,
                version.VersionNumber,
                version.Status,
                databaseContext.ProgramItems.Count(item => item.ProgramVersionId == version.Id),
                databaseContext.ProgramEnrollments.Count(enrollment => enrollment.ProgramVersionId == version.Id),
                version.CreatedBy,
                version.CreatedAt,
                version.PublishedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProgramVersionDto?> GetVersionAsync(
        Guid programVersionId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var version = await databaseContext.ProgramVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == programVersionId, cancellationToken);

        return version is null ? null : await BuildVersionDtoAsync(version, cancellationToken);
    }

    public async Task<ProgramVersionDto> EnsureDraftAsync(
        Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        // Three phases, and the order is load-bearing. The live tree is read first, the lesson
        // version pins are resolved second with no transaction of ours open, and only then does the
        // write scope open. EnsurePublishedVersionIdAsync mints a lesson's first version and can
        // lose a unique-index race doing it; a unique-index violation aborts the whole Postgres
        // transaction it happens in, so calling it inside the write scope below would take the
        // programme draft down with it. Same reasoning, and the same comment, as the call site in
        // ExerciseService.
        var plannedLessons = await ReadLiveCurriculumAsync(cancellationToken);
        var pinnedLessons = await ResolvePinsAsync(plannedLessons, cancellationToken);

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var draft = await databaseContext.ProgramVersions
            .FirstOrDefaultAsync(version => version.Status == ProgramVersionStatuses.Draft, cancellationToken);

        if (draft is null)
        {
            draft = new ProgramVersion
            {
                Id = Guid.NewGuid(),
                VersionNumber = await ResolveNextVersionNumberAsync(cancellationToken),
                Status = ProgramVersionStatuses.Draft,
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow,
            };

            databaseContext.ProgramVersions.Add(draft);
        }
        else
        {
            var staleItems = await databaseContext.ProgramItems
                .Where(item => item.ProgramVersionId == draft.Id)
                .ToListAsync(cancellationToken);

            databaseContext.ProgramItems.RemoveRange(staleItems);
        }

        var orderIndex = 0;
        foreach (var pinnedLesson in pinnedLessons)
        {
            databaseContext.ProgramItems.Add(new ProgramItem
            {
                Id = Guid.NewGuid(),
                ProgramVersionId = draft.Id,
                SkillId = pinnedLesson.SkillId,
                LessonId = pinnedLesson.LessonId,
                LessonVersionId = pinnedLesson.LessonVersionId,
                OrderIndex = orderIndex++,
            });
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        return await BuildVersionDtoAsync(draft, cancellationToken);
    }

    public async Task<PublishProgramVersionResultDto?> PublishAsync(
        Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var draft = await databaseContext.ProgramVersions
            .FirstOrDefaultAsync(version => version.Status == ProgramVersionStatuses.Draft, cancellationToken);
        if (draft is null)
        {
            return null;
        }

        var latestPublishedVersion = await databaseContext.ProgramVersions
            .Where(version => version.Status == ProgramVersionStatuses.Published)
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var draftItems = await ReadOrderedItemsAsync(draft.Id, cancellationToken);

        // The programme equivalent of 40.15's content hash. There is no body to hash — a programme
        // is references — so the comparison is over the reference tuples themselves, in order.
        // Publishing an unchanged programme would tell every enrolled learner that a new version is
        // waiting and then show them an empty diff, which is how a switch notice stops being read.
        if (latestPublishedVersion is not null)
        {
            var publishedItems = await ReadOrderedItemsAsync(latestPublishedVersion.Id, cancellationToken);
            if (AreStructurallyEqual(draftItems, publishedItems))
            {
                databaseContext.ProgramItems.RemoveRange(draftItems);
                databaseContext.ProgramVersions.Remove(draft);
                await databaseContext.SaveChangesAsync(cancellationToken);
                await tenantScope.CommitAsync(cancellationToken);

                return new PublishProgramVersionResultDto(
                    await BuildVersionDtoAsync(latestPublishedVersion, cancellationToken),
                    CreatedNewVersion: false);
            }
        }

        // Re-derived rather than trusted from draft creation time: another version could have been
        // published in between, and a version number is not something to guess at.
        draft.VersionNumber = await ResolveNextVersionNumberAsync(cancellationToken);
        draft.Status = ProgramVersionStatuses.Published;
        draft.PublishedAt = DateTime.UtcNow;
        if (draft.CreatedBy is null)
        {
            draft.CreatedBy = actorId;
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Program version published ProgramVersionId={ProgramVersionId} VersionNumber={VersionNumber} ItemCount={ItemCount}",
            draft.Id, draft.VersionNumber, draftItems.Count);

        return new PublishProgramVersionResultDto(
            await BuildVersionDtoAsync(draft, cancellationToken),
            CreatedNewVersion: true);
    }

    public async Task<ProgramDiffDto?> GetDiffAsync(
        Guid fromProgramVersionId,
        Guid toProgramVersionId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var endpoints = await databaseContext.ProgramVersions
            .AsNoTracking()
            .Where(version => version.Id == fromProgramVersionId || version.Id == toProgramVersionId)
            .Select(version => new { version.Id, version.VersionNumber })
            .ToListAsync(cancellationToken);

        var fromVersion = endpoints.FirstOrDefault(version => version.Id == fromProgramVersionId);
        var toVersion = endpoints.FirstOrDefault(version => version.Id == toProgramVersionId);
        if (fromVersion is null || toVersion is null)
        {
            return null;
        }

        var fromItems = await ReadOrderedItemsAsync(fromProgramVersionId, cancellationToken);
        var toItems = await ReadOrderedItemsAsync(toProgramVersionId, cancellationToken);

        var snapshots = await ReadSnapshotFactsAsync(
            fromItems.Concat(toItems).Select(item => item.LessonVersionId), cancellationToken);

        var fromByLesson = fromItems.ToDictionary(item => item.LessonId);
        var toByLesson = toItems.ToDictionary(item => item.LessonId);

        var added = toItems
            .Where(item => !fromByLesson.ContainsKey(item.LessonId))
            .Select(item => ToDiffLesson(item, snapshots))
            .ToList();

        var removed = fromItems
            .Where(item => !toByLesson.ContainsKey(item.LessonId))
            .Select(item => ToDiffLesson(item, snapshots))
            .ToList();

        var changed = new List<ProgramDiffVersionChangeDto>();
        var moved = new List<ProgramDiffMoveDto>();

        foreach (var toItem in toItems)
        {
            if (!fromByLesson.TryGetValue(toItem.LessonId, out var fromItem))
            {
                continue;
            }

            if (fromItem.LessonVersionId != toItem.LessonVersionId)
            {
                changed.Add(await BuildVersionChangeAsync(fromItem, toItem, snapshots, cancellationToken));
            }
            else if (fromItem.OrderIndex != toItem.OrderIndex || fromItem.SkillId != toItem.SkillId)
            {
                moved.Add(new ProgramDiffMoveDto(
                    toItem.LessonId,
                    ResolveTitle(toItem.LessonVersionId, snapshots),
                    fromItem.SkillId,
                    toItem.SkillId,
                    fromItem.OrderIndex,
                    toItem.OrderIndex));
            }
        }

        return new ProgramDiffDto(
            fromProgramVersionId,
            fromVersion.VersionNumber,
            toProgramVersionId,
            toVersion.VersionNumber,
            added,
            removed,
            changed,
            moved,
            changed.Any(change => change.IsBreaking));
    }

    /// <summary>
    /// The live skill tree flattened into the order a learner walks it: skills by their position in
    /// the tree, topics by their position in the skill, lessons by their position in the topic, with
    /// the row id as the final tie-break so that two rows sharing a position never swap places
    /// between two calls. Archived lessons are left out — that is what archiving is for.
    /// </summary>
    private async Task<IReadOnlyList<PlannedLesson>> ReadLiveCurriculumAsync(CancellationToken cancellationToken)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var rows = await databaseContext.Lessons
            .AsNoTracking()
            .Where(lesson => !lesson.IsArchived)
            .Join(
                databaseContext.Topics,
                lesson => lesson.TopicId,
                topic => topic.Id,
                (lesson, topic) => new { Lesson = lesson, Topic = topic })
            .Join(
                databaseContext.Skills,
                pair => pair.Topic.SkillId,
                skill => skill.Id,
                (pair, skill) => new
                {
                    SkillId = skill.Id,
                    SkillOrder = skill.OrderInTree,
                    TopicOrder = pair.Topic.OrderInSkill,
                    TopicId = pair.Topic.Id,
                    LessonOrder = pair.Lesson.OrderInTopic,
                    LessonId = pair.Lesson.Id,
                })
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(row => row.SkillOrder)
            .ThenBy(row => row.SkillId)
            .ThenBy(row => row.TopicOrder)
            .ThenBy(row => row.TopicId)
            .ThenBy(row => row.LessonOrder)
            .ThenBy(row => row.LessonId)
            .Select(row => new PlannedLesson(row.SkillId, row.LessonId))
            .ToList();
    }

    /// <summary>
    /// One resolver call per lesson, each opening its own short transaction. That is more round
    /// trips than a single query would take, and it is the price of going through the same resolver
    /// an attempt goes through: a programme and the progress recorded against it must never disagree
    /// about which snapshot a lesson currently is. The call happens once, when an administrator
    /// opens the programme editor, over a curriculum of at most a few hundred lessons.
    /// </summary>
    private async Task<IReadOnlyList<PinnedLesson>> ResolvePinsAsync(
        IReadOnlyList<PlannedLesson> plannedLessons,
        CancellationToken cancellationToken)
    {
        var pinnedLessons = new List<PinnedLesson>(plannedLessons.Count);

        foreach (var plannedLesson in plannedLessons)
        {
            var lessonVersionId = await lessonVersionService.EnsurePublishedVersionIdAsync(
                plannedLesson.LessonId, cancellationToken);

            if (lessonVersionId is null)
            {
                logger.LogWarning(
                    "Program draft skipped a lesson with no resolvable version LessonId={LessonId}",
                    plannedLesson.LessonId);
                continue;
            }

            pinnedLessons.Add(new PinnedLesson(plannedLesson.SkillId, plannedLesson.LessonId, lessonVersionId.Value));
        }

        return pinnedLessons;
    }

    private Task<List<ProgramItem>> ReadOrderedItemsAsync(Guid programVersionId, CancellationToken cancellationToken)
        => databaseContext.ProgramItems
            .Where(item => item.ProgramVersionId == programVersionId)
            .OrderBy(item => item.OrderIndex)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

    private async Task<int> ResolveNextVersionNumberAsync(CancellationToken cancellationToken)
    {
        var highestVersionNumber = await databaseContext.ProgramVersions
            .Select(version => (int?)version.VersionNumber)
            .MaxAsync(cancellationToken);

        return (highestVersionNumber ?? 0) + 1;
    }

    private static bool AreStructurallyEqual(IReadOnlyList<ProgramItem> left, IReadOnlyList<ProgramItem> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        return !left.Where((item, index) =>
            item.LessonId != right[index].LessonId
            || item.LessonVersionId != right[index].LessonVersionId
            || item.SkillId != right[index].SkillId
            || item.OrderIndex != right[index].OrderIndex).Any();
    }

    private async Task<ProgramVersionDto> BuildVersionDtoAsync(
        ProgramVersion version,
        CancellationToken cancellationToken)
    {
        var items = await ReadOrderedItemsAsync(version.Id, cancellationToken);
        var snapshots = await ReadSnapshotFactsAsync(items.Select(item => item.LessonVersionId), cancellationToken);

        return new ProgramVersionDto(
            version.Id,
            version.VersionNumber,
            version.Status,
            version.CreatedBy,
            version.CreatedAt,
            version.PublishedAt,
            items.Select(item => ToItemDto(item, snapshots)).ToList());
    }

    private static ProgramItemDto ToItemDto(ProgramItem item, IReadOnlyDictionary<Guid, SnapshotFacts> snapshots)
    {
        snapshots.TryGetValue(item.LessonVersionId, out var facts);

        return new ProgramItemDto(
            item.Id,
            item.SkillId,
            item.LessonId,
            item.LessonVersionId,
            facts?.VersionNumber,
            facts?.Title,
            item.OrderIndex);
    }

    private static ProgramDiffLessonDto ToDiffLesson(
        ProgramItem item,
        IReadOnlyDictionary<Guid, SnapshotFacts> snapshots)
    {
        snapshots.TryGetValue(item.LessonVersionId, out var facts);

        return new ProgramDiffLessonDto(
            item.LessonId,
            item.SkillId,
            item.LessonVersionId,
            facts?.VersionNumber,
            facts?.Title,
            item.OrderIndex);
    }

    private async Task<ProgramDiffVersionChangeDto> BuildVersionChangeAsync(
        ProgramItem fromItem,
        ProgramItem toItem,
        IReadOnlyDictionary<Guid, SnapshotFacts> snapshots,
        CancellationToken cancellationToken)
    {
        snapshots.TryGetValue(fromItem.LessonVersionId, out var fromFacts);
        snapshots.TryGetValue(toItem.LessonVersionId, out var toFacts);

        var isBreaking = await IsBreakingBetweenAsync(
            toItem.LessonId, fromFacts?.VersionNumber, toFacts?.VersionNumber, cancellationToken);

        return new ProgramDiffVersionChangeDto(
            toItem.LessonId,
            toItem.SkillId,
            toFacts?.Title ?? fromFacts?.Title,
            fromItem.LessonVersionId,
            fromFacts?.VersionNumber,
            toItem.LessonVersionId,
            toFacts?.VersionNumber,
            isBreaking);
    }

    /// <summary>
    /// Whether any published version of the lesson strictly between the two pins declared itself
    /// breaking. Reading the target version's own flag would be the obvious shortcut and is wrong: a
    /// programme can skip several lesson versions at once, so a changed correct answer in version 4
    /// would be hidden behind a cosmetic version 5 (docs/TENANCY/CONTENT_MODEL.md §2.4). The
    /// interval is expressed with min/max so that a deliberate move back to an older programme is
    /// reported just as loudly as a move forward — the learner crosses the same edit either way.
    ///
    /// <para>
    /// When either pin's version number is unknown — the snapshot is gone or invisible — the answer
    /// is <see langword="true"/>. "The content changed and nobody can say how" is a breaking change.
    /// </para>
    /// </summary>
    private async Task<bool> IsBreakingBetweenAsync(
        Guid lessonId,
        int? fromVersionNumber,
        int? toVersionNumber,
        CancellationToken cancellationToken)
    {
        if (fromVersionNumber is null || toVersionNumber is null)
        {
            return true;
        }

        var lowerBound = Math.Min(fromVersionNumber.Value, toVersionNumber.Value);
        var upperBound = Math.Max(fromVersionNumber.Value, toVersionNumber.Value);

        return await databaseContext.LessonVersions
            .AsNoTracking()
            .AnyAsync(
                version => version.LessonId == lessonId
                           && version.IsBreaking
                           && version.VersionNumber > lowerBound
                           && version.VersionNumber <= upperBound,
                cancellationToken);
    }

    /// <summary>
    /// The two facts a programme needs about a pinned snapshot: its number, and the lesson title as
    /// it stood inside that snapshot. The title comes out of the frozen document rather than off the
    /// live <c>Lessons</c> row on purpose — showing the current title next to an old pin is exactly
    /// the retroactive substitution this phase exists to stop.
    ///
    /// <para>
    /// It costs loading <c>Content</c> for every pinned version, which is the largest column in the
    /// table. Acceptable at programme size (tens to low hundreds of lessons) and worth revisiting
    /// with a generated column if a curriculum ever gets big enough to feel it.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, SnapshotFacts>> ReadSnapshotFactsAsync(
        IEnumerable<Guid> lessonVersionIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = lessonVersionIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return new Dictionary<Guid, SnapshotFacts>();
        }

        var rows = await databaseContext.LessonVersions
            .AsNoTracking()
            .Where(version => distinctIds.Contains(version.Id))
            .Select(version => new { version.Id, version.VersionNumber, version.Content })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.Id,
            row => new SnapshotFacts(row.VersionNumber, ReadSnapshotTitle(row.Content)));
    }

    private static string? ResolveTitle(Guid lessonVersionId, IReadOnlyDictionary<Guid, SnapshotFacts> snapshots)
        => snapshots.TryGetValue(lessonVersionId, out var facts) ? facts.Title : null;

    private static string? ReadSnapshotTitle(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);

            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("title", out var titleElement)
                   && titleElement.ValueKind == JsonValueKind.String
                ? titleElement.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record PlannedLesson(Guid SkillId, Guid LessonId);

    private sealed record PinnedLesson(Guid SkillId, Guid LessonId, Guid LessonVersionId);

    private sealed record SnapshotFacts(int VersionNumber, string? Title);
}
