using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Assignments.Models;
using Sellevate.Learning.Features.Assignments.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Assignments.Services.Implementation;

/// <summary>
/// Phase 40.23. The manager's side of an assignment (docs/TENANCY/ASSIGNMENTS.md §1).
///
/// <para>
/// <b>The query is over progress rows, not over assignments.</b> "Which assignments am I on" is
/// literally "which progress rows are mine", because 40.23 writes exactly one row per person at
/// issue time and that row is the record that they were asked. An assignment nobody issued to this
/// person cannot appear here however active it is, and no extra authorization check is needed to
/// make that true.
/// </para>
///
/// <para>
/// <b>Nothing here writes.</b> It would be natural to mark a row <c>in_progress</c> when its owner
/// first looks at it — and that would make this service the second writer of a column
/// <c>AssignmentThresholdEvaluator</c> owns (40.22), with a different idea of what "started" means:
/// opening a screen versus doing a piece of graded work. One writer per column is what keeps
/// <c>AttemptCount</c> and <c>FirstOpenedAt</c> worth reading, so the read stays a read.
/// </para>
/// </summary>
internal sealed class MyAssignmentService(LearningDbContext databaseContext) : IMyAssignmentService
{
    public async Task<IReadOnlyList<ActiveAssignmentDto>> GetActiveForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var now = DateTime.UtcNow;

        var rows = await (
                from record in databaseContext.AssignmentProgressRecords.AsNoTracking()
                join assignment in databaseContext.Assignments.AsNoTracking()
                    on record.AssignmentId equals assignment.Id
                where record.UserId == userId
                      && assignment.Status == AssignmentStatuses.Active
                      // "пока не выполнено": a completed assignment stops taking the top of the
                      // screen. failed_threshold stays — the work is finished and the bar was not
                      // met, and hiding it would leave the person who most needs another attempt
                      // with no way back to it.
                      && record.Status != AssignmentProgressStatuses.Completed
                      // An assignment scheduled to open later is not yet this person's problem.
                      && (assignment.OpensAt == null || assignment.OpensAt <= now)
                select new { Record = record, Assignment = assignment })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        var contentByAssignment = rows.ToDictionary(
            row => row.Assignment.Id,
            row => AssignmentDocumentSerializer.DeserializeContent(row.Assignment.Content));

        var titles = await ResolveItemTitlesAsync(contentByAssignment.Values, cancellationToken);

        return rows
            // Soonest deadline first, and an open-ended assignment last rather than first: null
            // sorts before every date in Postgres' default ordering, which would put the one
            // assignment with no urgency at the top of a screen whose entire job is urgency. Done
            // in memory because the set is one person's open assignments — single digits.
            .OrderBy(row => row.Assignment.Deadline ?? DateTime.MaxValue)
            .ThenByDescending(row => row.Assignment.ActivatedAt ?? row.Assignment.CreatedAt)
            .Select(row => new ActiveAssignmentDto(
                row.Assignment.Id,
                row.Assignment.Title,
                row.Assignment.Goal,
                row.Assignment.OpensAt,
                row.Assignment.Deadline,
                AssignmentDocumentSerializer.DeserializeRule(row.Assignment.CompletionRule),
                contentByAssignment[row.Assignment.Id]
                    .OrderBy(item => item.OrderIndex)
                    .Select(item => ToItemDto(item, titles))
                    .ToList(),
                row.Record.Status,
                row.Record.BestScore,
                row.Record.AttemptCount,
                row.Record.FirstOpenedAt,
                row.Record.CompletedAt))
            .ToList();
    }

    private static ActiveAssignmentItemDto ToItemDto(
        AssignmentContentItemDto item,
        IReadOnlyDictionary<string, ResolvedItemTitle> titles)
    {
        var resolved = titles.GetValueOrDefault(BuildTitleKey(item.Kind, item.Reference));

        return new ActiveAssignmentItemDto(
            item.Kind,
            item.Reference,
            item.OrderIndex,
            resolved?.Title,
            resolved?.LessonId);
    }

    /// <summary>
    /// Resolves the display title of every referenced row in two queries rather than one per item.
    ///
    /// <para>
    /// A <c>dialog_scenario</c> reference is deliberately not resolved: it names an ai-service mode
    /// key, and learning-service asking ai-service for a title on every home-screen load would put
    /// a second service in the path of the first screen a manager sees. The key travels raw and the
    /// client — which already lists dialog modes to draw the practice screen — supplies the name.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyDictionary<string, ResolvedItemTitle>> ResolveItemTitlesAsync(
        IEnumerable<IReadOnlyList<AssignmentContentItemDto>> contentSets,
        CancellationToken cancellationToken)
    {
        var allItems = contentSets.SelectMany(items => items).ToList();

        var lessonVersionIds = ParseReferenceIds(allItems, AssignmentContentItemKinds.LessonVersion);
        var referenceMaterialIds = ParseReferenceIds(allItems, AssignmentContentItemKinds.ReferenceMaterial);

        var titles = new Dictionary<string, ResolvedItemTitle>(StringComparer.Ordinal);

        if (lessonVersionIds.Count > 0)
        {
            var lessonVersions = await (
                    from version in databaseContext.LessonVersions.AsNoTracking()
                    join lesson in databaseContext.Lessons.AsNoTracking() on version.LessonId equals lesson.Id
                    where lessonVersionIds.Contains(version.Id)
                    select new { version.Id, version.LessonId, lesson.Title })
                .ToListAsync(cancellationToken);

            foreach (var lessonVersion in lessonVersions)
            {
                titles[BuildTitleKey(AssignmentContentItemKinds.LessonVersion, lessonVersion.Id.ToString())] =
                    new ResolvedItemTitle(lessonVersion.Title, lessonVersion.LessonId);
            }
        }

        if (referenceMaterialIds.Count > 0)
        {
            var materials = await databaseContext.ReferenceMaterials
                .AsNoTracking()
                .Where(material => referenceMaterialIds.Contains(material.Id))
                .Select(material => new { material.Id, material.Title })
                .ToListAsync(cancellationToken);

            foreach (var material in materials)
            {
                titles[BuildTitleKey(AssignmentContentItemKinds.ReferenceMaterial, material.Id.ToString())] =
                    new ResolvedItemTitle(material.Title, null);
            }
        }

        return titles;
    }

    private static List<Guid> ParseReferenceIds(IEnumerable<AssignmentContentItemDto> items, string kind)
        => items
            .Where(item => item.Kind == kind)
            .Select(item => Guid.TryParse(item.Reference, out var parsed) ? parsed : Guid.Empty)
            .Where(parsed => parsed != Guid.Empty)
            .Distinct()
            .ToList();

    /// <summary>
    /// Kind and reference together, because a reference is only unique inside its kind — the same
    /// uuid could in principle be a lesson version and a reference material.
    /// </summary>
    private static string BuildTitleKey(string kind, string reference) => $"{kind}|{reference}";

    private sealed record ResolvedItemTitle(string Title, Guid? LessonId);
}
